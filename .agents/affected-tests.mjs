#!/usr/bin/env node
// Prints the commands that final validation runs for a session's changes, as docs/architecture/test-selection.md
// ("A script turns the changed files into the commands to run") describes. It only prints; the agent runs the commands.
// Exit code 0: it printed a plan, possibly an empty one. 1: it printed one error line and no plan.
// It needs only Node.js's own modules and git.
// Usage: node .agents/affected-tests.mjs [--base <ref>]
import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync } from 'node:fs';
import { join, posix } from 'node:path';
import { pathToFileURL } from 'node:url';

const usage = 'usage: node .agents/affected-tests.mjs [--base <ref>]';
const ROOT_NAMESPACE = 'Memoria.ToDo';
const ARCHITECTURE_NAMESPACE = `${ROOT_NAMESPACE}.Architecture`;
const TESTING_PART = 'Testing';
const DESKTOP_TESTS_FOLDER = 'tests/Desktop/';
const commands = {
  node: 'node --test ".agents/*.test.mjs"',
  generate: 'node .agents/generate.mjs --check',
  everyTest: 'dotnet test -p:MemoriaAllTestKinds=true',
};
const ignoredFolders = new Set(['bin', 'obj', 'node_modules']);

export class PlanError extends Error {
  constructor(message) {
    super(message);
    this.name = 'PlanError';
  }
}

const oneLine = (message) => String(message).trim().replace(/\s*\n\s*/g, ' ');
const inside = (namespace, base) => namespace === base || (namespace ?? '').startsWith(`${base}.`);
const moduleNamespace = (part, module) => `${ROOT_NAMESPACE}.${part}.${module}.Tests`;
const compare = (a, b) => (a < b ? -1 : a > b ? 1 : 0);

// ---------- what the repository looks like: the project graph and the test folders ----------

/** Follows reverse edges from `start`, through the projects `accept` allows. Never includes `start`. */
function closure(reverse, start, accept) {
  const found = new Set();
  const pending = [start];
  while (pending.length) {
    for (const referrer of reverse.get(pending.pop()) ?? []) {
      if (referrer !== start && accept(referrer) && !found.has(referrer)) {
        found.add(referrer);
        pending.push(referrer);
      }
    }
  }
  return found;
}

const productProject = /^(src\/[^/]+\/[^/]+)\/[^/]+\.csproj$/;
const testProject = /^tests\/([^/]+)\/Tests\/[^/]+\.csproj$/;

/** Reads the project graph, checking that every reference names a project. */
function readProjectGraph(projects) {
  const known = new Set(projects.map((p) => p.path));
  const reverse = new Map();
  const linkers = new Map(); // a file a test project links -> the test projects that link it
  for (const { path, references, linkedFiles = [] } of projects) {
    for (const file of linkedFiles) {
      if (!linkers.has(file)) linkers.set(file, []);
      linkers.get(file).push(path);
    }
    for (const reference of references) {
      if (!known.has(reference)) throw new PlanError(`${path} references ${reference}, which does not exist`);
      if (!reverse.has(reference)) reverse.set(reference, []);
      reverse.get(reference).push(path);
    }
  }
  const productModules = new Map(); // "src/<Part>/<Module>" -> its project
  const testProjects = new Map(); // part -> its test project
  const projectByFolder = new Map();
  for (const { path } of projects) {
    projectByFolder.set(posix.dirname(path), path);
    const product = productProject.exec(path);
    if (product) productModules.set(product[1], path);
    const tests = testProject.exec(path);
    if (tests) testProjects.set(tests[1], path);
  }
  const moduleOfProject = new Map([...productModules].map(([module, path]) => [path, module]));
  return {
    productModules,
    projectInFolder: (folder) => projectByFolder.get(folder),
    /** The part's test project, or undefined when it has none (for example, it was deleted). */
    testProject: (part) => testProjects.get(part),
    /** The projects that link `file` into their build with a `Compile` item. */
    projectsLinking: (file) => linkers.get(file) ?? [],
    /** The product modules that depend on `modulePath`, through any number of steps. Test projects never count. */
    dependentsOf: (modulePath) => [...closure(reverse, productModules.get(modulePath), (p) => moduleOfProject.has(p))].map((p) => moduleOfProject.get(p)),
    /** The test projects that reference `project`, through any number of steps. */
    testProjectsUsing: (project) => [...closure(reverse, project, () => true)].filter((p) => testProject.test(p)),
  };
}

const testFileLocation = /^tests\/([^/]+)\/Tests\/(.+)$/;

/** Reads the test folders: one unit per module folder and per screen folder, with the unit names each one's files use. Checks the namespace rule. */
function readTestUnits(testFiles, graph) {
  const modules = new Map(); // "<Part>/<Module>" -> { part, module, files, folders: Map(folder -> files) }
  for (const file of testFiles) {
    const location = testFileLocation.exec(file.path);
    if (!location || location[1] === TESTING_PART) continue;
    const [, part, rest] = location;
    const segments = rest.split('/');
    if (segments.length === 1) {
      const allowed = [`${ROOT_NAMESPACE}.${part}`, `${ROOT_NAMESPACE}.${part}.Tests`];
      if (!allowed.includes(file.namespace)) throw new PlanError(`${file.path} declares ${describe(file.namespace)}, expected ${allowed.join(' or ')}`);
      continue;
    }
    const expected = moduleNamespace(part, segments[0]);
    if (!inside(file.namespace, expected)) throw new PlanError(`${file.path} declares ${describe(file.namespace)}, expected ${expected} or a child namespace`);
    const key = `${part}/${segments[0]}`;
    if (!modules.has(key)) modules.set(key, { part, module: segments[0], files: [], folders: new Map() });
    const entry = modules.get(key);
    if (segments.length === 2) {
      entry.files.push(file);
    } else {
      if (!entry.folders.has(segments[1])) entry.folders.set(segments[1], []);
      entry.folders.get(segments[1]).push(file);
    }
  }

  const byNamespace = new Map();
  const moduleUnits = new Map();
  const makeUnit = (kind, namespace, project, parent, files) => {
    const unit = { kind, namespace, project, parent, screens: [], files };
    byNamespace.set(namespace, unit);
    return unit;
  };
  for (const [key, { part, module, files, folders }] of modules) {
    const namespace = moduleNamespace(part, module);
    const project = graph.testProject(part);
    if (!project) continue; // the part's test project is gone: a change to its files selects every test, see `unresolved`
    const unit = makeUnit('module', namespace, project, null, []);
    moduleUnits.set(key, unit);
    const own = [...files];
    for (const [folder, folderFiles] of folders) {
      const screenNamespace = `${namespace}.${folder}`;
      if (!folderFiles.some((f) => inside(f.namespace, screenNamespace))) {
        own.push(...folderFiles); // a helper folder: its files use the module's namespace
        continue;
      }
      for (const f of folderFiles) {
        if (!inside(f.namespace, screenNamespace)) throw new PlanError(`${f.path} declares ${describe(f.namespace)}, expected ${screenNamespace} or a child namespace`);
      }
      unit.screens.push(makeUnit('screen', screenNamespace, project, unit, folderFiles));
    }
    unit.files = own;
  }

  // The longest unit namespace that `name` starts with and that is longer than `floor` characters.
  const unitNamed = (name, floor) => {
    for (let prefix = name; prefix.length > floor; prefix = prefix.slice(0, Math.max(prefix.lastIndexOf('.'), 0))) {
      if (byNamespace.has(prefix)) return byNamespace.get(prefix);
    }
    return undefined;
  };
  // A name as written may be relative: try it under the file's namespace and each enclosing one, longest first, then as written.
  // A match must go beyond the namespace the name was tried under, or every name would match the file's own unit.
  const resolved = new Map(); // namespace -> Map(name -> unit or null)
  const unitUsedBy = (namespace, name) => {
    const key = namespace ?? '';
    if (!resolved.has(key)) resolved.set(key, new Map());
    const cache = resolved.get(key);
    if (cache.has(name)) return cache.get(name);
    let found;
    for (let enclosing = key; enclosing && !found; enclosing = enclosing.slice(0, Math.max(enclosing.lastIndexOf('.'), 0))) {
      found = unitNamed(`${enclosing}.${name}`, enclosing.length);
    }
    found ??= unitNamed(name, 0) ?? null;
    cache.set(name, found);
    return found;
  };
  // A unit uses another's test code when its files name the other's namespace.
  const users = new Map();
  for (const user of byNamespace.values()) {
    for (const file of user.files) {
      for (const name of file.usedNamespaces) {
        const used = unitUsedBy(file.namespace, name);
        if (!used || used === user) continue;
        if (!users.has(used)) users.set(used, new Set());
        users.get(used).add(user);
      }
    }
  }
  return {
    moduleUnit: (part, module) => moduleUnits.get(`${part}/${module}`),
    screenUnit: (part, module, name) => {
      const unit = byNamespace.get(`${moduleNamespace(part, module)}.${name}`);
      return unit?.kind === 'screen' ? unit : undefined;
    },
    usersOf: (unit) => users.get(unit) ?? [],
  };
}

const describe = (namespace) => (namespace ? `namespace ${namespace}` : 'no namespace');

// ---------- the rule table: the only home of what a changed path selects ----------

const rule = (pattern, apply, accepts = () => true) => ({ pattern, apply, accepts });
const modulePathOf = (match) => `src/${match[1]}/${match[2]}`;

/** Each row's `apply(context, change, match)` records what its path selects. The first row that matches decides. */
const rules = [
  rule(/^(?:Directory\.Build\.props|Directory\.Packages\.props|global\.json|\.config\/dotnet-tools\.json|\.editorconfig|tests\/xunit\.runner\.json|[^/]+\.slnx?|tests\/Testing\/(?:Kinds|Analyzers)\/.+)$/,
    (c, { path }) => c.everyTest.push(path)),
  rule(/^tests\/Testing\/(?:Tests|UnitRunTiming)\//,
    (c, change) => c.wholeTestProject(TESTING_PART, change)),
  rule(/^(tests\/[^/]+\/(?:Fakes|PlatformTestHost))\//, (c, change, match) => {
    const project = c.repo.graph.projectInFolder(match[1]);
    if (!project) return c.unresolved(change);
    for (const user of c.repo.graph.testProjectsUsing(project)) c.wholeProject(user, change.path);
  }),
  rule(/^tests\/([^/]+)\/Tests\/([^/]+)\/([^/]+)\//,
    (c, { path }, match) => c.changedUnit(c.repo.units.screenUnit(match[1], match[2], match[3]), path),
    (match, repo) => repo.units.screenUnit(match[1], match[2], match[3]) !== undefined),
  rule(/^tests\/([^/]+)\/Tests\/([^/]+)\/.+/, (c, change, match) => {
    const unit = c.repo.units.moduleUnit(match[1], match[2]);
    return unit ? c.changedUnit(unit, change.path) : c.unresolved(change);
  }),
  rule(/^tests\/([^/]+)\/Tests\/[^/]+$/, (c, change, match) => c.wholeTestProject(match[1], change)),
  rule(/^src\/([^/]+)\/([^/]+)\/([^/]+)\/.+/, (c, change, match) => {
    const record = c.changedModule(change, modulePathOf(match));
    if (!record) return undefined;
    const unit = c.repo.units.screenUnit(match[1], match[2], match[3]);
    if (!record.screens.has(unit)) record.screens.set(unit, []);
    record.screens.get(unit).push(change.path);
    return undefined;
  }, (match, repo) => repo.units.screenUnit(match[1], match[2], match[3]) !== undefined),
  rule(/^src\/([^/]+)\/([^/]+)\/.+/, (c, change, match) => c.changedModule(change, modulePathOf(match))?.top.push(change.path)),
  rule(/^\.agents\/[^/]+\.mjs$/, (c, { path }) => c.extra(commands.node, path)),
  rule(/^(?:\.agents\/(?:subagents|skills)|\.claude\/(?:agents|skills)|\.codex\/agents|\.github\/agents)\//, (c, { path }) => c.extra(commands.generate, path)),
  rule(/^\.github\/workflows\//, (c, { path }) => c.cloud.push(path)),
  rule(/^(?:docs\/|\.vscode\/|\.gitignore$|\.ignore$|\.gitattributes$)|\.md$/, () => undefined),
];

/** Rules that add to the row that matched. */
const addOns = [
  { applies: (path) => path.endsWith('.csproj') || path === 'docs/architecture/modules.md', apply: (c, path) => c.architecture.push(path) },
  { applies: (path, repo) => repo.graph.projectsLinking(path).length > 0, apply: (c, path) => { for (const project of c.repo.graph.projectsLinking(path)) c.wholeProject(project, path); } },
];

function findRule(path, repo) {
  for (const candidate of rules) {
    const match = candidate.pattern.exec(path);
    if (match && candidate.accepts(match, repo)) return { candidate, match };
  }
  throw new PlanError(`no rule matches the changed path ${path}`);
}

/** Collects what every changed path selects. */
function collectSelections(changes, repo) {
  const context = {
    repo,
    everyTest: [],
    extras: new Map(), // command -> changed paths
    wholeProjects: new Map(), // test project -> changed paths
    units: new Map(), // test folder -> changed paths
    modules: new Map(), // module path -> { top: paths, screens: Map(screen unit -> paths) }
    architecture: [],
    cloud: [],
    extra: (command, path) => pushTo(context.extras, command, path),
    wholeProject: (project, path) => pushTo(context.wholeProjects, project, path),
    /** Every test of the part's test project; a deleted path whose test project is gone selects every test instead. */
    wholeTestProject(part, change) {
      const project = repo.graph.testProject(part);
      if (project) context.wholeProject(project, change.path);
      else context.unresolved(change);
    },
    changedUnit: (unit, path) => pushTo(context.units, unit, path),
    changedModule(change, modulePath) {
      if (!repo.graph.productModules.has(modulePath)) return context.unresolved(change);
      if (!context.modules.has(modulePath)) context.modules.set(modulePath, { top: [], screens: new Map() });
      return context.modules.get(modulePath);
    },
    /** A path whose project is gone can't say who used it, so a deleted one selects every test; any other is a mistake. */
    unresolved({ path, deleted }) {
      if (!deleted) throw new PlanError(`${path} is in a folder that has no project or test folder`);
      context.everyTest.push(path);
      return undefined;
    },
  };
  for (const change of changes) {
    const { candidate, match } = findRule(change.path, repo);
    candidate.apply(context, change, match);
    for (const addOn of addOns) if (addOn.applies(change.path, repo)) addOn.apply(context, change.path);
  }
  return context;
}

function pushTo(map, key, value) {
  if (!map.has(key)) map.set(key, []);
  map.get(key).push(value);
}

/** Every changed path once; a renamed file counts at its old path, as a deleted one, and its new path. */
function changedPaths(changes) {
  const seen = new Map();
  const add = (path, deleted) => {
    if (!seen.has(path)) seen.set(path, { path, deleted });
  };
  for (const change of changes) {
    if (change.status === 'renamed') add(change.oldPath, true);
    add(change.path, change.status === 'deleted');
  }
  return [...seen.values()];
}

// ---------- the plan ----------

const changedReason = (paths) => `changed: ${paths.join(', ')}`;
const filtersOf = {
  module: (namespace) => [`${namespace}*`],
  screen: (namespace) => [namespace, `${namespace}.*`],
  project: () => [],
};

/** Decides what to run for `changes`. Touches no git and no files; throws PlanError for any problem it finds. */
export function planTests({ changes, projects, testFiles }) {
  const graph = readProjectGraph(projects);
  const repo = { graph, units: readTestUnits(testFiles, graph) };
  const found = collectSelections(changedPaths(changes), repo);

  const extras = [];
  for (const command of [commands.node, commands.generate]) {
    if (found.extras.has(command)) extras.push({ command, reasons: [changedReason(found.extras.get(command))] });
  }
  if (found.everyTest.length) {
    extras.push({ command: commands.everyTest, reasons: [changedReason(found.everyTest)] });
    return finish({ selections: [], extras, noTests: [], cloud: found.cloud });
  }

  const selected = new Map(); // test folder -> reasons
  const noTests = new Map(); // module path -> reasons
  const select = (unit, reason) => {
    const isNew = !selected.has(unit);
    if (isNew) selected.set(unit, new Set());
    selected.get(unit).add(reason);
    return isNew;
  };
  const selectModule = (modulePath, reason) => {
    const unit = repo.units.moduleUnit(...modulePath.split('/').slice(1));
    if (unit) return select(unit, reason);
    if (!noTests.has(modulePath)) noTests.set(modulePath, new Set());
    noTests.get(modulePath).add(reason);
    return false;
  };

  for (const [unit, paths] of found.units) select(unit, changedReason(paths));
  for (const [modulePath, { top, screens }] of found.modules) {
    if (top.length) {
      selectModule(modulePath, changedReason([...top, ...[...screens.values()].flat()]));
    } else {
      for (const [unit, paths] of screens) select(unit, `narrowed: ${paths.join(', ')}`);
    }
    for (const dependent of graph.dependentsOf(modulePath)) selectModule(dependent, `dependent: ${modulePath}`);
  }

  // A module's tests include its screens' tests, so the users of either are selected.
  const pending = [...selected.keys()];
  for (let i = 0; i < pending.length; i++) {
    for (const source of [pending[i], ...pending[i].screens]) {
      for (const user of repo.units.usersOf(source)) {
        if (select(user, `uses test code of: ${source.namespace}`)) pending.push(user);
      }
    }
  }

  const selections = [];
  for (const [unit, reasons] of selected) {
    if (unit.kind === 'screen' && selected.has(unit.parent)) continue; // the module's filter covers it
    selections.push({ kind: unit.kind, project: unit.project, namespace: unit.namespace, reasons: [...reasons] });
  }
  if (found.architecture.length) {
    const testing = graph.testProject(TESTING_PART);
    if (!testing) throw new PlanError(`tests/${TESTING_PART}/Tests has no test project`);
    selections.push({ kind: 'module', project: testing, namespace: ARCHITECTURE_NAMESPACE, reasons: [changedReason(found.architecture)] });
  }
  for (const [project, paths] of found.wholeProjects) {
    selections.push({ kind: 'project', project, namespace: null, reasons: [changedReason(paths)] });
  }
  return finish({
    selections: selections.filter((s) => s.kind === 'project' || !found.wholeProjects.has(s.project)),
    extras,
    noTests: [...noTests].map(([module, reasons]) => ({ module, reasons: [...reasons] })).sort((a, b) => compare(a.module, b.module)),
    cloud: found.cloud,
  });
}

/** Sorts the selections and writes the commands. */
function finish({ selections, extras, noTests, cloud }) {
  selections.sort((a, b) => compare(a.project, b.project) || compare(a.namespace ?? '', b.namespace ?? ''));
  const byProject = new Map();
  for (const selection of selections) {
    if (!byProject.has(selection.project)) byProject.set(selection.project, []);
    byProject.get(selection.project).push(...filtersOf[selection.kind](selection.namespace));
  }
  const dotnet = [...byProject].map(([project, filters]) =>
    `dotnet test --project ${project} -p:MemoriaAllTestKinds=true${filters.map((f) => ` --filter-namespace "${f}"`).join('')}`);
  return {
    selections,
    extras,
    noTests,
    cloud: [...cloud].sort(),
    desktopCheck: selections.some((s) => s.project.startsWith(DESKTOP_TESTS_FOLDER)) || extras.some((e) => e.command === commands.everyTest),
    commands: [...dotnet, ...extras.map((e) => e.command)],
  };
}

/** Writes the plan as text, every line ending with a newline. */
export function formatPlan(plan) {
  const lines = [];
  const why = [
    ...plan.selections.map((s) => [s.namespace ?? `every test in ${s.project}`, s.reasons]),
    ...plan.extras.map((e) => [e.command, e.reasons]),
    ...plan.noTests.map((n) => [`${n.module} has no test folder, so nothing runs for it`, n.reasons]),
  ];
  if (why.length) lines.push('Selected because:', ...why.map(([what, reasons]) => `- ${what}: ${reasons.join('; ')}`));
  lines.push(`Desktop check needed: ${plan.desktopCheck ? 'yes' : 'no'}`);
  if (plan.commands.length) lines.push('Commands:', ...plan.commands);
  else lines.push('No tests to run');
  if (plan.cloud.length) {
    lines.push(`Only the cloud checks: ${plan.cloud.join(', ')}`, 'Push the branch, then start Run All tests with node .agents/run-all-tests.mjs, or the run all tests task, to check it.');
  }
  return `${lines.join('\n')}\n`;
}

// ---------- running it ----------

function parseArguments(argv) {
  let base = null;
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] !== '--base') throw new PlanError(`unknown argument "${argv[i]}"; ${usage}`);
    if (base !== null) throw new PlanError(`--base given twice; ${usage}`);
    const value = argv[++i];
    if (value === undefined || value.startsWith('-')) throw new PlanError(`--base needs a commit; ${usage}`);
    base = value;
  }
  return { base };
}

/** Runs the script: `argv` are the command-line arguments, `ports` read the changes, the projects and the test files. */
export async function run(argv, ports) {
  try {
    const { base } = parseArguments(argv);
    const changes = await ports.changes(base);
    const projects = await ports.projects();
    const testFiles = await ports.testFiles();
    return { code: 0, stdout: formatPlan(planTests({ changes, projects, testFiles })), stderr: '' };
  } catch (error) {
    return { code: 1, stdout: '', stderr: `${oneLine(error?.message ?? error)}\n` };
  }
}

// ---------- the real ports: git and the file system ----------

function git(cwd, args) {
  try {
    return execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
  } catch (error) {
    if (error.code === 'ENOENT') throw new Error('git is not installed or is not on the PATH');
    throw new Error(`git ${args[0]} failed: ${String(error.stderr ?? '').trim() || error.message}`);
  }
}

const statusNames = { A: 'added', M: 'modified', D: 'deleted', R: 'renamed', C: 'added', T: 'modified' };

/** The working tree's changes against `base` (HEAD when null), staged or not, plus untracked files that git doesn't ignore. */
function readChanges(root, base) {
  const ref = base ?? 'HEAD';
  try {
    git(root, ['rev-parse', '--verify', '--quiet', `${ref}^{commit}`]);
  } catch {
    throw new Error(base === null ? 'HEAD names no commit' : `--base ${base} names no commit`);
  }
  const tokens = git(root, ['diff', ref, '--name-status', '-z', '-M', '--']).split('\0');
  const changes = [];
  for (let i = 0; i < tokens.length && tokens[i] !== '';) {
    const kind = tokens[i++][0];
    const status = statusNames[kind] ?? 'modified';
    if (kind === 'R') {
      const oldPath = tokens[i++];
      changes.push({ status, oldPath, path: tokens[i++] });
    } else {
      if (kind === 'C') i++; // a copy's source is unchanged
      changes.push({ status, path: tokens[i++] });
    }
  }
  for (const path of git(root, ['ls-files', '--others', '--exclude-standard', '-z']).split('\0')) {
    if (path !== '') changes.push({ status: 'added', path });
  }
  return changes;
}

/** Every file under `folder`, as repository-relative paths, skipping build output. */
function listFiles(root, folder, found = []) {
  let entries;
  try {
    entries = readdirSync(join(root, folder), { withFileTypes: true });
  } catch (error) {
    if (error.code === 'ENOENT') return found;
    throw error;
  }
  for (const entry of entries) {
    const path = `${folder}/${entry.name}`;
    if (!entry.isDirectory()) found.push(path);
    else if (!ignoredFolders.has(entry.name)) listFiles(root, path, found);
  }
  return found;
}

function readProject(root, path) {
  const text = readFileSync(join(root, path), 'utf8');
  if (!/<Project\b/.test(text) || !/(?:<\/Project>|\/>)\s*$/.test(text)) throw new Error(`${path} is not a project file`);
  const resolveFromProject = (include) => posix.normalize(posix.join(posix.dirname(path), include.replaceAll('\\', '/')));
  const references = [...text.matchAll(/<ProjectReference\b[^>]*?\bInclude\s*=\s*(["'])(.*?)\1/g)].map((m) => resolveFromProject(m[2]));
  const folder = `${posix.dirname(path)}/`;
  const linkedFiles = [...text.matchAll(/<Compile\b[^>]*?\bInclude\s*=\s*(["'])(.*?)\1/g)]
    .map((m) => resolveFromProject(m[2]))
    .filter((file) => !file.startsWith(folder));
  return { path, references, linkedFiles };
}

function readTestFile(root, path) {
  const text = readFileSync(join(root, path), 'utf8');
  const namespace = /^\s*namespace\s+([\w.]+)/m.exec(text)?.[1] ?? null;
  const usedNamespaces = [...new Set(text.match(/\b[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+/g) ?? [])];
  return { path, namespace, usedNamespaces };
}

/** Ports that read the repository containing `cwd`. */
function createPorts(cwd) {
  let root;
  let files;
  const repositoryRoot = () => (root ??= git(cwd, ['rev-parse', '--show-toplevel']).trim());
  const listed = () => (files ??= [...listFiles(repositoryRoot(), 'src'), ...listFiles(repositoryRoot(), 'tests')]);
  return {
    changes: (base) => readChanges(repositoryRoot(), base),
    projects: () => listed().filter((p) => p.endsWith('.csproj')).map((p) => readProject(repositoryRoot(), p)),
    testFiles: () => listed().filter((p) => p.startsWith('tests/') && p.endsWith('.cs')).map((p) => readTestFile(repositoryRoot(), p)),
  };
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const { code, stdout, stderr } = await run(process.argv.slice(2), createPorts(process.cwd()));
  process.stdout.write(stdout);
  process.stderr.write(stderr);
  process.exitCode = code;
}
