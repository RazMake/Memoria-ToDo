// Tests for affected-tests.mjs. Unit tests give `planTests`, `formatPlan` and `run` made-up data and fake ports: no git, no files.
// Integration tests run the script as a real process on temporary git repositories, never on this repository.
// The rules under test are in docs/architecture/test-selection.md.
//
// Contract the tests rely on:
//   export class PlanError extends Error        // name "PlanError"; message is ONE line naming the problem
//   export function planTests({ changes, projects, testFiles }): Plan      // throws PlanError; touches no git or files
//   export function formatPlan(plan): string                               // lines end with "\n"
//   export async function run(argv, ports): Promise<{ code: 0 | 1, stdout, stderr }>
//   Importing the module runs nothing; the script runs only when node starts it directly, on the repository
//   that contains the current directory.
//
//   changes:   [{ path, status: 'added' | 'modified' | 'deleted' }, { path, oldPath, status: 'renamed' }]
//              Paths are relative to the repository root and use "/".
//   projects:  [{ path, references: [path], linkedFiles?: [path] }]    every .csproj under src/ and tests/; references already
//              resolved to such paths; linkedFiles are the repository-relative files its <Compile Include> items take from outside its folder
//   testFiles: [{ path, namespace, usedNamespaces: [string] }]   every .cs file under tests/ that is not in bin/ or obj/;
//              usedNamespaces are the dotted names the file names in `using` directives or qualified names, exactly as written:
//              a name may be relative, and planTests resolves it against the file's namespace and its enclosing namespaces.
//   ports:     { changes(base: string | null), projects(), testFiles() }, each sync or async, each may throw (any Error).
//   argv:      [] or ['--base', '<ref>']
//
//   Plan = {
//     selections: [{ kind: 'module' | 'screen' | 'project', project, namespace | null, reasons: [string] }],
//         // kind 'module': filter "<ns>*"; 'screen': filters "<ns>" and "<ns>.*"; 'project': the whole test project, no filter.
//         // Architecture tests are a 'module' selection of Memoria.ToDo.Architecture in the Testing test project.
//     extras: [{ command, reasons }],     // node tests, generate check, and the every-test command
//     noTests: [{ module, reasons }],     // a selected module ("src/<Part>/<Module>") that has no test folder
//     cloud: [path],                      // changed workflow files, which only the cloud checks
//     desktopCheck: boolean,
//     commands: [string],                 // one `dotnet test --project ...` per test project (sorted by project path, filters
//                                         // sorted by namespace), then the extras' commands
//   }
//   Reasons start with "changed: <paths>", "narrowed: <paths>", "dependent: <module path>" or "uses test code of: <namespace>".
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { PlanError, formatPlan, planTests, run } from './affected-tests.mjs';

const script = join(dirname(fileURLToPath(import.meta.url)), 'affected-tests.mjs');

// ---------- unit fixture: a small made-up repository ----------

const src = (part, name) => `src/${part}/${name}/Memoria.ToDo.${part}.${name}.csproj`;
const sharedPlatform = src('Shared', 'Platform');
const sharedTasks = src('Shared', 'Tasks');
const sharedSync = src('Shared', 'Sync');
const desktopPlatform = src('Desktop', 'Platform');
const taskEditor = src('Desktop', 'TaskEditor');
const taskList = src('Desktop', 'TaskList');
const logs = src('Desktop', 'Logs');
const updates = src('Desktop', 'Updates'); // has no test folder
const shell = src('Desktop', 'Shell');
const app = src('Desktop', 'App');
const desktopFakes = 'tests/Desktop/Fakes/Memoria.ToDo.Desktop.Fakes.csproj';
const testHost = 'tests/Desktop/PlatformTestHost/Memoria.ToDo.Desktop.PlatformTestHost.csproj';
const desktopTests = 'tests/Desktop/Tests/Memoria.ToDo.Desktop.Tests.csproj';
const sharedFakes = 'tests/Shared/Fakes/Memoria.ToDo.Shared.Fakes.csproj';
const sharedTests = 'tests/Shared/Tests/Memoria.ToDo.Shared.Tests.csproj';
const testingTests = 'tests/Testing/Tests/Memoria.ToDo.Testing.Tests.csproj';

const project = (path, ...references) => ({ path, references });
const projects = [
  project(sharedPlatform),
  project(sharedTasks, sharedPlatform),
  project(sharedSync, sharedTasks),
  project(desktopPlatform, sharedPlatform),
  project(taskEditor, desktopPlatform, sharedTasks),
  project(taskList, taskEditor),
  project(logs, desktopPlatform),
  project(updates, desktopPlatform),
  project(shell, taskList, logs, updates),
  project(app, shell),
  project(desktopFakes, desktopPlatform),
  project(testHost),
  project(desktopTests, desktopPlatform, taskEditor, taskList, logs, shell, app, desktopFakes, testHost),
  project(sharedFakes, sharedPlatform),
  project(sharedTests, sharedPlatform, sharedTasks, sharedSync, sharedFakes),
  project(testingTests),
  project('tests/Testing/Analyzers/Memoria.ToDo.Testing.Analyzers.csproj'),
  project('tests/Testing/Kinds/Memoria.ToDo.Testing.Kinds.csproj'),
  project('tests/Testing/UnitRunTiming/Memoria.ToDo.Testing.UnitRunTiming.csproj'),
];

const testFile = (path, namespace, usedNamespaces = []) => ({ path, namespace, usedNamespaces });
const moduleTests = (part, name) => testFile(`tests/${part}/Tests/${name}/${name}Tests.cs`, `Memoria.ToDo.${part}.${name}.Tests`);
const editorNs = 'Memoria.ToDo.Desktop.TaskEditor.Tests';
const testFiles = [
  ...['Platform', 'TaskEditor', 'TaskList', 'Logs', 'Shell', 'App'].map((name) => moduleTests('Desktop', name)),
  ...['Platform', 'Tasks', 'Sync'].map((name) => moduleTests('Shared', name)),
  // TaskEditor's screens: TagControl (with a deeper folder) and Tag, whose name is a prefix of TagControl's; Helpers is not a screen.
  testFile('tests/Desktop/Tests/TaskEditor/TagControl/TagViewTests.cs', `${editorNs}.TagControl`),
  testFile('tests/Desktop/Tests/TaskEditor/TagControl/Deep/DeepTests.cs', `${editorNs}.TagControl.Deep`),
  testFile('tests/Desktop/Tests/TaskEditor/Tag/TagTests.cs', `${editorNs}.Tag`),
  testFile('tests/Desktop/Tests/TaskEditor/Helpers/Helper.cs', editorNs),
  testFile('tests/Desktop/Tests/ScreenTests.cs', 'Memoria.ToDo.Desktop'),
  testFile('tests/Testing/Tests/Architecture/ModuleMapTests.cs', 'Memoria.ToDo.Architecture'),
  testFile('tests/Testing/Tests/Analyzers/KindTests.cs', 'Memoria.ToDo.Testing.Analyzers'),
];

const modified = (...paths) => paths.map((path) => ({ path, status: 'modified' }));
const plan = (changes, extraTestFiles = []) => planTests({ changes, projects, testFiles: [...testFiles, ...extraTestFiles] });

const moduleFilter = (part, name) => [`Memoria.ToDo.${part}.${name}.Tests*`];
const screenFilter = (name, screen) => [`Memoria.ToDo.Desktop.${name}.Tests.${screen}`, `Memoria.ToDo.Desktop.${name}.Tests.${screen}.*`];
const architecture = ['Memoria.ToDo.Architecture*'];

// The dotnet commands as { project: sorted filters } (no filter: []), plus every other command in order.
function summarize(result) {
  const byProject = {};
  const other = [];
  for (const command of result.commands) {
    const match = /^dotnet test --project (\S+) -p:MemoriaAllTestKinds=true((?: --filter-namespace "[^"]+")*)$/.exec(command);
    if (!match) {
      other.push(command);
      continue;
    }
    byProject[match[1]] = [...match[2].matchAll(/"([^"]+)"/g)].map((m) => m[1]).sort();
  }
  return { byProject, other };
}
const expected = (byProject, other = []) => ({
  byProject: Object.fromEntries(Object.entries(byProject).map(([key, value]) => [key, [...value].sort()])),
  other,
});
const selects = (changes, byProject, other = [], extraTestFiles = []) =>
  assert.deepEqual(summarize(plan(changes, extraTestFiles)), expected(byProject, other));

const selectionOf = (result, namespace) => result.selections.find((s) => s.namespace === namespace);

function assertPlanError(action, message) {
  assert.throws(action, (error) => {
    assert.ok(error instanceof PlanError, `PlanError expected, got ${error}`);
    assert.ok(!error.message.includes('\n'), 'one line');
    assert.match(error.message, message);
    return true;
  });
}

const everyTestCommand = 'dotnet test -p:MemoriaAllTestKinds=true';
const nodeCommand = 'node --test ".agents/*.test.mjs"';
const generateCommand = 'node .agents/generate.mjs --check';

// ---------- product code ----------

describe('a changed product project (unit)', () => {
  test('selects its own test folder and the whole test folder of every module that depends on it, through any number of steps', () => {
    selects(modified('src/Desktop/Logs/LogView.cs'), {
      [desktopTests]: [...moduleFilter('Desktop', 'Logs'), ...moduleFilter('Desktop', 'Shell'), ...moduleFilter('Desktop', 'App')],
    });
  });

  test('prints the exact dotnet command with the kinds property and one namespace filter per selection', () => {
    assert.deepEqual(plan(modified('src/Desktop/Logs/LogView.cs')).commands, [
      `dotnet test --project ${desktopTests} -p:MemoriaAllTestKinds=true`
      + ' --filter-namespace "Memoria.ToDo.Desktop.App.Tests*"'
      + ' --filter-namespace "Memoria.ToDo.Desktop.Logs.Tests*"'
      + ' --filter-namespace "Memoria.ToDo.Desktop.Shell.Tests*"',
    ]);
  });

  test('prints one command for each test project the selection reaches', () => {
    selects(modified('src/Shared/Tasks/Task.cs'), {
      [sharedTests]: [...moduleFilter('Shared', 'Tasks'), ...moduleFilter('Shared', 'Sync')],
      [desktopTests]: [
        ...moduleFilter('Desktop', 'TaskEditor'), ...moduleFilter('Desktop', 'TaskList'),
        ...moduleFilter('Desktop', 'Shell'), ...moduleFilter('Desktop', 'App'),
      ],
    });
  });

  test('does not select every test of a test project, a fakes project or the test host that references the module', () => {
    const result = plan(modified('src/Desktop/Platform/Window.cs'));
    assert.ok(Object.values(summarize(result).byProject).every((filters) => filters.length > 0));
    assert.ok(!result.commands.some((c) => c.includes('Fakes') || c.includes('PlatformTestHost')));
  });

  test('explains a changed module by its files and a dependent by the module it depends on', () => {
    const result = plan(modified('src/Desktop/Logs/LogView.cs', 'src/Desktop/Logs/LogRow.cs'));
    const changed = selectionOf(result, 'Memoria.ToDo.Desktop.Logs.Tests');
    assert.equal(changed.project, desktopTests);
    assert.ok(changed.reasons.some((r) => r.startsWith('changed: ') && r.includes('src/Desktop/Logs/LogView.cs') && r.includes('src/Desktop/Logs/LogRow.cs')));
    const dependent = selectionOf(result, 'Memoria.ToDo.Desktop.App.Tests');
    assert.ok(dependent.reasons.some((r) => r === 'dependent: src/Desktop/Logs'));
  });

  test('lists a selected module with no test folder, and runs nothing for it', () => {
    const result = plan(modified('src/Desktop/Updates/UpdateView.cs'));
    assert.equal(result.noTests.length, 1);
    assert.equal(result.noTests[0].module, 'src/Desktop/Updates');
    assert.ok(result.noTests[0].reasons.some((r) => r.startsWith('changed: ')));
    assert.deepEqual(summarize(result).byProject, expected({ [desktopTests]: [...moduleFilter('Desktop', 'Shell'), ...moduleFilter('Desktop', 'App')] }).byProject);
  });

  test('selects nothing that depends on the composition root, which nothing depends on', () => {
    selects(modified('src/Desktop/App/Program.cs'), { [desktopTests]: moduleFilter('Desktop', 'App') });
  });
});

// ---------- screens ----------

describe('a change inside screen folders (unit)', () => {
  test('narrows the changed module to the screen, at any depth, and still runs every dependent in full', () => {
    selects(modified('src/Desktop/TaskEditor/TagControl/Deep/Part.cs'), {
      [desktopTests]: [
        ...screenFilter('TaskEditor', 'TagControl'), ...moduleFilter('Desktop', 'TaskList'),
        ...moduleFilter('Desktop', 'Shell'), ...moduleFilter('Desktop', 'App'),
      ],
    });
  });

  test('narrows to several screens when the changed files sit in several screen folders', () => {
    const result = plan(modified('src/Desktop/TaskEditor/TagControl/View.cs', 'src/Desktop/TaskEditor/Tag/Model.cs'));
    assert.ok(selectionOf(result, `${editorNs}.TagControl`));
    assert.ok(selectionOf(result, `${editorNs}.Tag`));
    assert.equal(selectionOf(result, editorNs), undefined);
  });

  test('never lets a screen filter match a sibling screen whose name starts with the same letters', () => {
    const filters = summarize(plan(modified('src/Desktop/TaskEditor/Tag/Model.cs'))).byProject[desktopTests];
    assert.ok(filters.includes(`${editorNs}.Tag`));
    assert.ok(filters.includes(`${editorNs}.Tag.*`));
    assert.ok(!filters.some((f) => f.endsWith('Tag*')), 'a "Tag*" filter would also select TagControl');
  });

  test('explains a narrowed module by the changed files that sit in its screens', () => {
    const screen = selectionOf(plan(modified('src/Desktop/TaskEditor/Tag/Model.cs')), `${editorNs}.Tag`);
    assert.equal(screen.kind, 'screen');
    assert.ok(screen.reasons.some((r) => r.startsWith('narrowed: ') && r.includes('src/Desktop/TaskEditor/Tag/Model.cs')));
  });

  for (const [why, files] of [
    ['a changed file at the module top', ['src/Desktop/TaskEditor/Editor.cs']],
    ['a changed file in a folder that has no test folder', ['src/Desktop/TaskEditor/Common/Shared.cs']],
    ['a changed file in a folder whose test files use the module namespace', ['src/Desktop/TaskEditor/Helpers/Util.cs']],
    ['a changed project file', [taskEditor]],
  ]) {
    test(`widens to the whole module when ${why} sits beside a screen change`, () => {
      const result = plan(modified('src/Desktop/TaskEditor/TagControl/View.cs', ...files));
      assert.equal(selectionOf(result, editorNs).kind, 'module');
      assert.equal(selectionOf(result, `${editorNs}.TagControl`), undefined);
    });
  }

  test('selects only that screen, with no dependents, when only a screen test folder changed', () => {
    selects(modified('tests/Desktop/Tests/TaskEditor/Tag/TagTests.cs'), { [desktopTests]: screenFilter('TaskEditor', 'Tag') });
  });

  test('selects the module test folder with its screens, and no dependents, when a file in it changed', () => {
    selects(modified('tests/Desktop/Tests/TaskEditor/Helpers/Helper.cs'), { [desktopTests]: moduleFilter('Desktop', 'TaskEditor') });
  });
});

// ---------- test code used across folders ----------

describe('test code used by other folders (unit)', () => {
  const logsNs = 'Memoria.ToDo.Desktop.Logs.Tests';
  const configure = (name, used) => testFile(`tests/Desktop/Tests/${name}/${name}Tests.cs`, `Memoria.ToDo.Desktop.${name}.Tests`, used);

  test('adds a folder whose files name a selected folder\'s namespace, and the folders that use that folder, through any number of steps', () => {
    const extra = [configure('Configure', ['Memoria.ToDo.Desktop.ContactList.Tests']), configure('ContactList', [logsNs]), configure('Unrelated', [])];
    const result = plan(modified('tests/Desktop/Tests/Logs/LogsTests.cs'), extra);
    assert.deepEqual(
      summarize(result).byProject[desktopTests],
      [...moduleFilter('Desktop', 'Logs'), ...moduleFilter('Desktop', 'ContactList'), ...moduleFilter('Desktop', 'Configure')].sort(),
    );
    assert.ok(selectionOf(result, 'Memoria.ToDo.Desktop.ContactList.Tests').reasons.some((r) => r === `uses test code of: ${logsNs}`));
  });

  test('follows a qualified name that continues past the namespace', () => {
    const extra = [configure('Configure', [`${logsNs}.Helpers.Builder`])];
    assert.ok(selectionOf(plan(modified('tests/Desktop/Tests/Logs/LogsTests.cs'), extra), 'Memoria.ToDo.Desktop.Configure.Tests'));
  });

  test('resolves a relative qualified name against the namespaces that enclose the file, as C# does', () => {
    const logsFile = testFile('tests/Desktop/Tests/Logs/Screen/Contacts.cs', logsNs, ['TaskEditor.Tests.EditorFields']);
    const result = plan(modified('tests/Desktop/Tests/TaskEditor/Helpers/Helper.cs'), [logsFile]);
    assert.ok(selectionOf(result, logsNs).reasons.some((r) => r === `uses test code of: ${editorNs}`));
    assert.deepEqual(summarize(result).byProject[desktopTests], [...moduleFilter('Desktop', 'TaskEditor'), ...moduleFilter('Desktop', 'Logs')].sort());
  });

  test('resolves a relative qualified name to a screen folder\'s namespace', () => {
    const logsFile = testFile('tests/Desktop/Tests/Logs/Contacts.cs', logsNs, ['TaskEditor.Tests.Tag.TagBuilder']);
    const result = plan(modified('tests/Desktop/Tests/TaskEditor/Tag/TagTests.cs'), [logsFile]);
    assert.ok(selectionOf(result, logsNs).reasons.some((r) => r === `uses test code of: ${editorNs}.Tag`));
  });

  test('ignores a relative qualified name that no enclosing namespace turns into a test namespace', () => {
    const logsFile = testFile('tests/Desktop/Tests/Logs/Contacts.cs', logsNs, ['Nowhere.Tests.Thing', 'System.Linq.Enumerable']);
    selects(modified('tests/Desktop/Tests/TaskEditor/Helpers/Helper.cs'), { [desktopTests]: moduleFilter('Desktop', 'TaskEditor') }, [], [logsFile]);
  });

  test('adds users of a module found through a changed product file as well', () => {
    const extra = [configure('Configure', [logsNs])];
    assert.ok(selectionOf(plan(modified('src/Desktop/Logs/LogView.cs'), extra), 'Memoria.ToDo.Desktop.Configure.Tests'));
  });

  test('does not count a use of a screen\'s namespace as a use of its module\'s code', () => {
    const extra = [configure('Configure', [`${editorNs}.Tag`])];
    const result = plan(modified('tests/Desktop/Tests/TaskEditor/Helpers/Helper.cs'), extra);
    assert.ok(selectionOf(result, 'Memoria.ToDo.Desktop.Configure.Tests'), 'the module folder holds its screens, so the user is selected');
    const screenOnly = plan(modified('tests/Desktop/Tests/TaskEditor/TagControl/TagViewTests.cs'), extra);
    assert.equal(selectionOf(screenOnly, 'Memoria.ToDo.Desktop.Configure.Tests'), undefined);
  });

  test('does not select the users of a module folder when only one of its screen folders changed', () => {
    const extra = [configure('Configure', [editorNs])];
    const result = plan(modified('tests/Desktop/Tests/TaskEditor/Tag/TagTests.cs'), extra);
    assert.equal(selectionOf(result, 'Memoria.ToDo.Desktop.Configure.Tests'), undefined);
  });
});

// ---------- test projects, shared test support and build-wide files ----------

describe('the other changed paths (unit)', () => {
  test('selects every test of a test project, with no filter, when a file at its Tests folder top changes', () => {
    selects(modified('tests/Desktop/Tests/ScreenTests.cs'), { [desktopTests]: [] });
  });

  test('selects every test of a test project, and the architecture tests, when its project file changes', () => {
    selects(modified(sharedTests), { [sharedTests]: [], [testingTests]: architecture });
  });

  test('selects every test of each test project that references a changed fakes project', () => {
    selects(modified('tests/Desktop/Fakes/FakeClock.cs'), { [desktopTests]: [] });
    selects(modified('tests/Shared/Fakes/FakeClock.cs'), { [sharedTests]: [] });
  });

  test('selects every desktop test when the platform test host changes', () => {
    selects(modified('tests/Desktop/PlatformTestHost/Host.cs'), { [desktopTests]: [] });
  });

  for (const path of ['tests/Testing/Tests/Architecture/ModuleMap.cs', 'tests/Testing/UnitRunTiming/Timer.cs']) {
    test(`selects every test of the Testing test project when ${path} changes`, () => {
      selects(modified(path), { [testingTests]: [] });
    });
  }

  for (const path of [
    'Directory.Build.props', 'Directory.Packages.props', 'global.json', '.config/dotnet-tools.json', '.editorconfig',
    'tests/xunit.runner.json', 'Memoria.ToDo.slnx', 'tests/Testing/Kinds/UxAttribute.cs', 'tests/Testing/Analyzers/KindAnalyzer.cs',
  ]) {
    test(`selects every test of every kind when ${path} changes`, () => {
      const result = plan(modified(path));
      assert.deepEqual(result.commands, [everyTestCommand]);
      assert.deepEqual(result.selections, []);
      assert.ok(result.extras[0].reasons.some((r) => r.startsWith('changed: ') && r.includes(path)));
    });
  }

  test('runs the agent script tests, and no dotnet test, when a .mjs file under .agents changes', () => {
    const result = plan(modified('.agents/release.mjs'));
    assert.deepEqual(result.commands, [nodeCommand]);
    assert.equal(result.desktopCheck, false);
  });

  for (const path of ['.agents/subagents/tester.md', '.agents/subagents/subagents.json', '.agents/skills/version-tag/SKILL.md',
    '.claude/agents/tester.md', '.claude/skills/version-tag/SKILL.md', '.codex/agents/tester.toml', '.github/agents/tester.agent.md']) {
    test(`runs only the generator check when ${path} changes`, () => {
      assert.deepEqual(plan(modified(path)).commands, [generateCommand]);
    });
  }

  test('runs nothing locally but names a changed workflow file as checked only by the cloud', () => {
    const result = plan(modified('.github/workflows/ci.yml'));
    assert.deepEqual(result.commands, []);
    assert.deepEqual(result.cloud, ['.github/workflows/ci.yml']);
  });

  for (const path of ['docs/architecture/logging.md', 'README.md', 'AGENTS.md', 'CHANGELOG.md', '.vscode/settings.json', '.gitignore', '.ignore', '.gitattributes']) {
    test(`selects nothing when ${path} changes`, () => {
      const result = plan(modified(path));
      assert.deepEqual(result, { selections: [], extras: [], noTests: [], cloud: [], desktopCheck: false, commands: [] });
    });
  }

  test('adds the architecture tests when a product project file changes', () => {
    const result = plan(modified(logs));
    assert.ok(summarize(result).byProject[testingTests].includes(architecture[0]));
    assert.ok(summarize(result).byProject[desktopTests].includes(moduleFilter('Desktop', 'Logs')[0]));
  });

  test('selects only the architecture tests when the module map document changes', () => {
    selects(modified('docs/architecture/modules.md'), { [testingTests]: architecture });
  });

  test('counts a renamed file at both its old and its new path', () => {
    const result = plan([{ status: 'renamed', oldPath: 'src/Shared/Sync/Old.cs', path: 'src/Desktop/Logs/New.cs' }]);
    assert.ok(selectionOf(result, 'Memoria.ToDo.Shared.Sync.Tests'));
    assert.ok(selectionOf(result, 'Memoria.ToDo.Desktop.Logs.Tests'));
  });

  test('selects every test when a deleted file belonged to a project that no longer exists', () => {
    const result = plan([{ status: 'deleted', path: 'src/Desktop/Gone/Thing.cs' }]);
    assert.deepEqual(result.commands, [everyTestCommand]);
    assert.ok(result.extras[0].reasons.some((r) => r.includes('src/Desktop/Gone/Thing.cs')));
  });

  test('selects by the rules, not everything, when a deleted file\'s project still exists', () => {
    selects([{ status: 'deleted', path: 'src/Desktop/App/Old.cs' }], { [desktopTests]: moduleFilter('Desktop', 'App') });
  });

  describe('a deleted test project', () => {
    const withoutDesktopTests = projects.filter((p) => p.path !== desktopTests);
    const withoutDesktopTestFiles = testFiles.filter((f) => !f.path.startsWith('tests/Desktop/Tests/'));
    const deleted = (...paths) => paths.map((path) => ({ path, status: 'deleted' }));
    const planWithout = (changes) => planTests({ changes, projects: withoutDesktopTests, testFiles: withoutDesktopTestFiles });

    test('selects every test when the whole project is deleted, its project file included', () => {
      const result = planWithout(deleted(desktopTests, 'tests/Desktop/Tests/Logs/LogsTests.cs', 'tests/Desktop/Tests/ScreenTests.cs'));
      assert.deepEqual(result.commands, [everyTestCommand]);
      assert.ok(result.extras[0].reasons.some((r) => r.includes(desktopTests)));
    });

    test('selects every test when only its project file is deleted', () => {
      assert.deepEqual(planWithout(deleted(desktopTests)).commands, [everyTestCommand]);
    });

    test('selects every test when a solution change already asked for the full run', () => {
      const result = planWithout([...modified('Memoria.ToDo.slnx'), ...deleted(desktopTests, 'tests/Desktop/Tests/Logs/LogsTests.cs')]);
      assert.deepEqual(result.commands, [everyTestCommand]);
      assert.deepEqual(result.selections, []);
    });
  });
});

describe('a file that a test project links into its own build (unit)', () => {
  const linked = 'src/Desktop/App/WindowsFileSystem.cs';
  const withLink = projects.map((p) => (p.path === sharedTests ? { ...p, linkedFiles: [linked] } : p));
  const planWithLink = (changes) => planTests({ changes, projects: withLink, testFiles });

  test('selects every test of the test project that links a changed file, besides what the file\'s own row selects', () => {
    assert.deepEqual(summarize(planWithLink(modified(linked))), expected({ [sharedTests]: [], [desktopTests]: moduleFilter('Desktop', 'App') }));
  });

  test('selects every test of the linking test project when the linked file is deleted', () => {
    const byProject = summarize(planWithLink([{ status: 'deleted', path: linked }])).byProject;
    assert.deepEqual(byProject[sharedTests], []);
  });

  test('does not select the linking test project when another file changes', () => {
    const byProject = summarize(planWithLink(modified('src/Desktop/App/Other.cs'))).byProject;
    assert.equal(byProject[sharedTests], undefined);
  });
});

// ---------- the desktop check line ----------

describe('the desktop check flag (unit)', () => {
  test('is needed when a command runs desktop tests, and for the every-test command', () => {
    assert.equal(plan(modified('src/Desktop/Logs/LogView.cs')).desktopCheck, true);
    assert.equal(plan(modified('Directory.Build.props')).desktopCheck, true);
  });

  test('is not needed when no command runs desktop tests', () => {
    for (const path of ['src/Shared/Sync/Sync.cs', 'docs/a.md', '.agents/release.mjs', 'tests/Testing/Tests/Architecture/ModuleMap.cs']) {
      assert.equal(plan(modified(path)).desktopCheck, false, path);
    }
  });
});

// ---------- failing fast ----------

describe('planning problems (unit)', () => {
  test('refuses a changed path that matches no rule, naming it', () => {
    assertPlanError(() => plan(modified('tools/build.ps1')), /tools\/build\.ps1/);
    assertPlanError(() => plan(modified('.agents/notes.txt')), /\.agents\/notes\.txt/);
  });

  test('refuses a project reference to a project that does not exist, naming both projects', () => {
    const broken = [...projects, project('src/Desktop/Broken/Memoria.ToDo.Desktop.Broken.csproj', 'src/Desktop/Missing/Memoria.ToDo.Desktop.Missing.csproj')];
    assertPlanError(() => planTests({ changes: [], projects: broken, testFiles }), /Broken.*Missing|Missing.*Broken/);
  });

  test('refuses a module test file whose namespace does not start with the module\'s test namespace, naming the file and the expected namespace', () => {
    const bad = testFile('tests/Desktop/Tests/Logs/Odd.cs', 'Memoria.ToDo.Desktop.Logs.TestsExtra');
    assertPlanError(() => plan([], [bad]), /tests\/Desktop\/Tests\/Logs\/Odd\.cs.*Memoria\.ToDo\.Desktop\.Logs\.Tests\b/);
  });

  test('accepts a module test file in a deeper namespace and in a folder that is not a screen', () => {
    plan([], [testFile('tests/Desktop/Tests/Logs/Parts/Row.cs', 'Memoria.ToDo.Desktop.Logs.Tests.Parts')]);
  });

  test('refuses a file in a screen folder whose namespace is not the screen\'s, naming the file and the screen namespace', () => {
    const bad = testFile('tests/Desktop/Tests/TaskEditor/Tag/Stray.cs', editorNs);
    assertPlanError(() => plan([], [bad]), new RegExp(`Tag/Stray\\.cs.*${editorNs.replaceAll('.', '\\.')}\\.Tag\\b`));
  });

  test('refuses a test file at a test project top whose namespace is neither Memoria.ToDo.<Part> nor Memoria.ToDo.<Part>.Tests', () => {
    assertPlanError(() => plan([], [testFile('tests/Desktop/Tests/Other.cs', 'Memoria.ToDo.Shared')]), /tests\/Desktop\/Tests\/Other\.cs.*Memoria\.ToDo\.Desktop/);
    plan([], [testFile('tests/Desktop/Tests/Helper.cs', 'Memoria.ToDo.Desktop.Tests')]);
  });

  test('checks the namespace rule on files that did not change', () => {
    assertPlanError(() => plan(modified('docs/a.md'), [testFile('tests/Desktop/Tests/Logs/Odd.cs', 'Wrong')]), /Odd\.cs/);
  });

  test('does not apply the namespace rule to the Testing test project, which is selected as a whole', () => {
    plan(modified('tests/Testing/Tests/Architecture/ModuleMap.cs'));
  });
});

// ---------- formatPlan ----------

describe('the printed plan (unit)', () => {
  const lines = (text) => text.split('\n');

  test('prints each selection with its reasons, then the desktop check line, then one command per line', () => {
    const result = plan(modified('src/Desktop/Logs/LogView.cs'));
    const text = formatPlan(result);
    assert.ok(text.endsWith('\n'));
    const all = lines(text);
    const reasonLine = all.findIndex((l) => l.includes('Memoria.ToDo.Desktop.Logs.Tests') && l.includes('changed: src/Desktop/Logs/LogView.cs'));
    const desktopLine = all.indexOf('Desktop check needed: yes');
    const commandLine = all.indexOf(result.commands[0]);
    assert.ok(reasonLine >= 0, text);
    assert.ok(all.some((l) => l.includes('Memoria.ToDo.Desktop.App.Tests') && l.includes('dependent: src/Desktop/Logs')));
    assert.ok(reasonLine < desktopLine && desktopLine < commandLine, text);
  });

  test('says the desktop check is not needed when no command runs desktop tests', () => {
    assert.ok(lines(formatPlan(plan(modified('src/Shared/Sync/Sync.cs')))).includes('Desktop check needed: no'));
  });

  test('prints "No tests to run" and no command when nothing is selected', () => {
    const text = formatPlan(plan(modified('docs/a.md')));
    assert.ok(lines(text).includes('No tests to run'));
    assert.ok(!text.includes('dotnet test') && !text.includes('node '));
  });

  test('prints the extra commands, and no "No tests to run", when only a node or generator command is selected', () => {
    const text = formatPlan(plan(modified('.agents/release.mjs', '.agents/skills/x/SKILL.md')));
    assert.ok(lines(text).includes(nodeCommand));
    assert.ok(lines(text).includes(generateCommand));
    assert.ok(!text.includes('No tests to run'));
  });

  test('says that only the cloud checks a changed workflow file, and to start Run All tests with the command or the task', () => {
    const text = formatPlan(plan(modified('.github/workflows/ci.yml')));
    assert.ok(text.includes('.github/workflows/ci.yml'));
    assert.ok(text.includes('node .agents/run-all-tests.mjs'), text);
    assert.match(text, /run all tests/);
    assert.doesNotMatch(text, /Start the manual run/);
  });

  test('says that a selected module has no test folder, naming it', () => {
    const text = formatPlan(plan(modified('src/Desktop/Updates/UpdateView.cs')));
    assert.ok(lines(text).some((l) => l.includes('src/Desktop/Updates') && /no test folder/.test(l)));
  });
});

// ---------- run: arguments, ports, exit codes ----------

describe('running the script (unit)', () => {
  const okPorts = (calls = []) => ({
    changes: async (base) => { calls.push(['changes', base]); return modified('src/Desktop/Logs/LogView.cs'); },
    projects: async () => { calls.push(['projects']); return projects; },
    testFiles: async () => { calls.push(['testFiles']); return testFiles; },
  });
  const oneLine = (text) => text.split('\n').filter((l) => l !== '');

  function assertRefusal(result, message) {
    assert.equal(result.code, 1, result.stderr);
    assert.equal(result.stdout, '');
    assert.equal(oneLine(result.stderr).length, 1, `one error line, got: ${result.stderr}`);
    assert.ok(result.stderr.endsWith('\n'));
    if (message) assert.match(result.stderr, message);
  }

  test('prints the formatted plan on stdout and succeeds, comparing against HEAD by default', async () => {
    const calls = [];
    const result = await run([], okPorts(calls));
    assert.deepEqual(result, { code: 0, stdout: formatPlan(plan(modified('src/Desktop/Logs/LogView.cs'))), stderr: '' });
    assert.deepEqual(calls[0], ['changes', null]);
  });

  test('compares against the commit that --base names', async () => {
    const calls = [];
    await run(['--base', 'origin/main'], okPorts(calls));
    assert.deepEqual(calls[0], ['changes', 'origin/main']);
  });

  test('succeeds with "No tests to run" when nothing changed', async () => {
    const ports = { ...okPorts(), changes: async () => [] };
    const result = await run([], ports);
    assert.equal(result.code, 0);
    assert.match(result.stdout, /No tests to run/);
  });

  for (const [why, argv] of [
    ['an unknown option', ['--bogus']],
    ['a word that is not an option', ['everything']],
    ['a repeated --base', ['--base', 'a', '--base', 'b']],
    ['a --base without a value', ['--base']],
    ['a --base followed by another option', ['--base', '--bogus']],
  ]) {
    test(`refuses ${why} with one error line, no plan, and touches no port`, async () => {
      const calls = [];
      assertRefusal(await run(argv, okPorts(calls)));
      assert.deepEqual(calls, []);
    });
  }

  for (const port of ['changes', 'projects', 'testFiles']) {
    test(`refuses with one error line carrying the message when the ${port} port fails`, async () => {
      const ports = { ...okPorts(), [port]: async () => { throw new Error(`${port} exploded`); } };
      assertRefusal(await run([], ports), new RegExp(`${port} exploded`));
    });
  }

  test('refuses with the planning problem as one error line and prints no plan', async () => {
    const ports = { ...okPorts(), changes: async () => modified('tools/build.ps1') };
    assertRefusal(await run([], ports), /tools\/build\.ps1/);
  });
});

// ---------- integration: the real script on temporary git repositories ----------

describe('the script on a temporary git repository (integration)', () => {
  const projectFile = (...references) =>
    `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n${references.map((r) => `    <ProjectReference Include="${r}" />\n`).join('')}  </ItemGroup>\n</Project>\n`;
  const testSource = (name) => `namespace Memoria.ToDo.Desktop.${name}.Tests;\n\npublic class ${name}Tests { }\n`;
  const testsProject = 'tests/Desktop/Tests/Memoria.ToDo.Desktop.Tests.csproj';
  const filter = (name) => `--filter-namespace "Memoria.ToDo.Desktop.${name}.Tests*"`;

  const files = {
    'src/Desktop/Core/Memoria.ToDo.Desktop.Core.csproj': projectFile(),
    'src/Desktop/Core/Engine.cs': 'class Engine { }\n',
    'src/Desktop/Ui/Memoria.ToDo.Desktop.Ui.csproj': projectFile('..\\Core\\Memoria.ToDo.Desktop.Core.csproj'),
    'src/Desktop/Ui/View.cs': 'class View { }\n',
    'src/Desktop/Other/Memoria.ToDo.Desktop.Other.csproj': projectFile(),
    'src/Desktop/Other/Thing.cs': 'class Thing { }\n',
    [testsProject]: projectFile(
      '..\\..\\..\\src\\Desktop\\Core\\Memoria.ToDo.Desktop.Core.csproj',
      '..\\..\\..\\src\\Desktop\\Ui\\Memoria.ToDo.Desktop.Ui.csproj',
      '..\\..\\..\\src\\Desktop\\Other\\Memoria.ToDo.Desktop.Other.csproj',
    ),
    'tests/Desktop/Tests/Core/CoreTests.cs': testSource('Core'),
    'tests/Desktop/Tests/Ui/UiTests.cs': testSource('Ui'),
    'tests/Desktop/Tests/Other/OtherTests.cs': testSource('Other'),
    'docs/readme.md': '# Docs\n',
    '.gitignore': '*.log\n',
  };

  const git = (cwd, ...args) => {
    const result = spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t.test', '-c', 'core.autocrlf=false', ...args], { cwd, encoding: 'utf8' });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout;
  };
  const write = (root, path, content) => {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), content);
  };
  const runScript = (cwd, ...args) => spawnSync(process.execPath, [script, ...args], { cwd, encoding: 'utf8' });

  function withRepository(body) {
    const root = mkdtempSync(join(tmpdir(), 'affected-tests-'));
    try {
      git(root, 'init', '--quiet');
      for (const [path, content] of Object.entries(files)) write(root, path, content);
      git(root, 'add', '--all');
      git(root, 'commit', '--quiet', '-m', 'initial');
      return body(root);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }

  const oneErrorLine = (result) => {
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.equal(result.stdout, '');
    assert.equal(result.stderr.split('\n').filter((l) => l !== '').length, 1, result.stderr);
  };

  test('succeeds with "No tests to run" when nothing changed', () => withRepository((root) => {
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /No tests to run/);
  }));

  test('selects the changed project and its dependents, reading the project references from the project files, for an unstaged change', () => withRepository((root) => {
    write(root, 'src/Desktop/Core/Engine.cs', 'class Engine { int x; }\n');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes(`dotnet test --project ${testsProject} -p:MemoriaAllTestKinds=true ${filter('Core')} ${filter('Ui')}`), result.stdout);
    assert.ok(!result.stdout.includes('Other.Tests'));
    assert.match(result.stdout, /Desktop check needed: yes/);
  }));

  test('sees a staged change', () => withRepository((root) => {
    write(root, 'tests/Desktop/Tests/Ui/UiTests.cs', testSource('Ui') + '// edited\n');
    git(root, 'add', 'tests/Desktop/Tests/Ui/UiTests.cs');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes(filter('Ui')) && !result.stdout.includes(filter('Core')), result.stdout);
  }));

  test('sees an untracked file but not one that git ignores', () => withRepository((root) => {
    write(root, 'src/Desktop/Other/Extra.cs', 'class Extra { }\n');
    write(root, 'src/Desktop/Ui/trace.log', 'noise\n');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes(filter('Other')) && !result.stdout.includes(filter('Ui')), result.stdout);
  }));

  test('counts a renamed file at both its old and its new path', () => withRepository((root) => {
    git(root, 'mv', 'src/Desktop/Other/Thing.cs', 'src/Desktop/Ui/Thing.cs');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes(filter('Other')) && result.stdout.includes(filter('Ui')), result.stdout);
    assert.ok(!result.stdout.includes(filter('Core')), result.stdout);
  }));

  test('compares against the commit that --base names, and finds nothing against HEAD once the change is committed', () => withRepository((root) => {
    write(root, 'src/Desktop/Other/Thing.cs', 'class Thing { int y; }\n');
    git(root, 'commit', '--quiet', '--all', '-m', 'change');
    const withoutBase = runScript(root);
    assert.equal(withoutBase.status, 0, withoutBase.stderr);
    assert.match(withoutBase.stdout, /No tests to run/);
    const withBase = runScript(root, '--base', 'HEAD~1');
    assert.equal(withBase.status, 0, withBase.stderr);
    assert.ok(withBase.stdout.includes(filter('Other')), withBase.stdout);
  }));

  test('refuses a --base that names no commit', () => withRepository((root) => {
    const result = runScript(root, '--base', 'no-such-ref');
    oneErrorLine(result);
    assert.match(result.stderr, /no-such-ref/);
  }));

  test('refuses an unknown option', () => withRepository((root) => oneErrorLine(runScript(root, '--bogus'))));

  test('refuses when the current directory is not in a git repository', () => {
    const root = mkdtempSync(join(tmpdir(), 'affected-tests-nogit-'));
    try {
      oneErrorLine(runScript(root));
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  test('refuses a project reference to a project that does not exist', () => withRepository((root) => {
    write(root, 'src/Desktop/Ui/Memoria.ToDo.Desktop.Ui.csproj', projectFile('..\\Missing\\Memoria.ToDo.Desktop.Missing.csproj'));
    const result = runScript(root);
    oneErrorLine(result);
    assert.match(result.stderr, /Missing/);
  }));

  test('refuses a project file that cannot be read as a project', () => withRepository((root) => {
    write(root, 'src/Desktop/Ui/Memoria.ToDo.Desktop.Ui.csproj', '<Project><ItemGroup>');
    oneErrorLine(runScript(root));
  }));

  test('refuses a changed path that matches no rule, naming it', () => withRepository((root) => {
    write(root, 'tools/build.ps1', 'echo\n');
    const result = runScript(root);
    oneErrorLine(result);
    assert.match(result.stderr, /tools\/build\.ps1/);
  }));

  test('refuses a test file whose namespace breaks the rule, naming the file', () => withRepository((root) => {
    write(root, 'tests/Desktop/Tests/Ui/Odd.cs', 'namespace Somewhere.Else;\n\npublic class Odd { }\n');
    const result = runScript(root);
    oneErrorLine(result);
    assert.match(result.stderr, /Odd\.cs/);
  }));

  test('follows a relative qualified name in a test file to the test folder it names', () => withRepository((root) => {
    write(root, 'tests/Desktop/Tests/Other/UsesCore.cs', 'namespace Memoria.ToDo.Desktop.Other.Tests;\n\npublic class UsesCore { object x = Core.Tests.CoreTests.Make(); }\n');
    git(root, 'add', '--all');
    git(root, 'commit', '--quiet', '-m', 'relative name');
    write(root, 'tests/Desktop/Tests/Core/CoreTests.cs', testSource('Core') + '// edited\n');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes(filter('Core')) && result.stdout.includes(filter('Other')), result.stdout);
    assert.ok(!result.stdout.includes(filter('Ui')), result.stdout);
  }));

  test('selects every test of a test project that links a changed file in a Compile item', () => withRepository((root) => {
    const sharedTestsProject = 'tests/Shared/Tests/Memoria.ToDo.Shared.Tests.csproj';
    write(root, sharedTestsProject,
      '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    <Compile Include="..\\..\\..\\src\\Desktop\\Other\\Thing.cs" Link="Linked\\Thing.cs" />\n  </ItemGroup>\n</Project>\n');
    write(root, 'tests/Shared/Tests/Store/StoreTests.cs', 'namespace Memoria.ToDo.Shared.Store.Tests;\n\npublic class StoreTests { }\n');
    git(root, 'add', '--all');
    git(root, 'commit', '--quiet', '-m', 'shared tests');
    write(root, 'src/Desktop/Other/Thing.cs', 'class Thing { int z; }\n');
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, new RegExp(`^.*dotnet test --project ${sharedTestsProject.replaceAll('.', '\\.')} -p:MemoriaAllTestKinds=true\\s*$`, 'm'));
    assert.ok(result.stdout.includes(filter('Other')), result.stdout);
  }));

  test('selects every test when a whole test project is deleted', () => withRepository((root) => {
    rmSync(join(root, 'tests/Desktop/Tests'), { recursive: true, force: true });
    const result = runScript(root);
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.stdout.includes('dotnet test -p:MemoriaAllTestKinds=true'), result.stdout);
  }));
});
