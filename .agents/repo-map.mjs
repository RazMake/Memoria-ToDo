#!/usr/bin/env node
// Prints a compact map of the repository so agents can read exact line ranges instead of whole files:
// the file tree with line counts, plus the symbols and Markdown headings in each file with their line numbers.
// Read-only and dependency-free. Honors .gitignore and the root .ignore, and shrinks the output to a budget.

import { execFileSync } from 'node:child_process';
import { existsSync, lstatSync, readFileSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const DEFAULT_BUDGET = 6000; // Characters, about 1,600 tokens: cheap to read and inside every agent tool's output limit.
const MAX_SYMBOLS_PER_FILE = 40; // Applies when more than one file is mapped.
const MAX_PARSED_BYTES = 1_000_000; // Larger files are counted but not parsed for symbols.
const MAX_NAME_LENGTH = 80;
const BINARY_SNIFF_BYTES = 8000;

// Symbols are lines indented at most maxIndent columns that match a pattern; the first matched group is the name.
const languages = [
  {
    exts: ['.js', '.mjs', '.cjs', '.jsx', '.ts', '.mts', '.cts', '.tsx'],
    maxIndent: 0,
    patterns: [
      /^(?:export\s+(?:default\s+)?)?(?:declare\s+)?(?:abstract\s+)?(?:async\s+)?(?:function(?:\s*\*\s*|\s+)|(?:class|interface|type|enum|namespace)\s+)([A-Za-z_$][\w$]*)/,
      /^(?:export\s+)?(?:const|let|var)\s+([A-Za-z_$][\w$]*)/,
    ],
  },
  { exts: ['.py', '.pyi'], maxIndent: 4, patterns: [/^(?:async\s+)?def\s+(\w+)/, /^class\s+(\w+)/] },
  { exts: ['.go'], maxIndent: 0, patterns: [/^func\s+(?:\([^)]*\)\s*)?(\w+)/, /^type\s+(\w+)/, /^(?:var|const)\s+(\w+)/] },
  {
    exts: ['.rs'],
    maxIndent: 4,
    patterns: [
      /^(?:pub(?:\([^)]*\))?\s+)?(?:(?:async|const|unsafe|extern\s+"[^"]*")\s+)*(?:fn|struct|enum|union|trait|type|mod)\s+(\w+)/,
      /^(?:pub(?:\([^)]*\))?\s+)?(?:const|static)\s+(?:mut\s+)?(\w+)\s*:/,
      /^impl\b(?:\s*<[^>]*>)?\s+(?:[\w:<>, ]+?\s+for\s+)?([\w:]+)/,
      /^macro_rules!\s*(\w+)/,
    ],
  },
  {
    exts: ['.cs', '.java'],
    maxIndent: 8,
    patterns: [
      /^(?:(?:public|private|protected|internal|static|abstract|sealed|partial|readonly|final|unsafe|file|new|ref)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?|namespace|@interface)\s+([\w.]+)/,
      /^(?:(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|extern|new|unsafe|partial|final|synchronized|native|default)\s+)+(?:[\w<>[\],.?]+(?:\s*,\s*[\w<>[\].?]+)*\s+)??(\w+)\s*(?:<[^>()]*>)?\s*\(/,
    ],
  },
  {
    exts: ['.kt', '.kts', '.swift'],
    maxIndent: 4,
    patterns: [
      /^(?:(?:@\w+|public|private|protected|internal|fileprivate|open|final|abstract|override|static|data|sealed|enum|inner|inline|suspend|operator|infix|tailrec|mutating|async)\s+)*(?:fun|func|class|struct|enum|protocol|extension|actor|object|interface|typealias)\s+(?:<[^>]*>\s*)?(?:[\w.]+\.)?(\w+)/,
    ],
  },
  {
    exts: ['.c', '.h', '.cc', '.cpp', '.cxx', '.hh', '.hpp', '.hxx'],
    maxIndent: 0,
    patterns: [
      /^(?:template\s*<.*>\s*)?(?:class|struct|union|enum(?:\s+class)?|namespace)\s+(\w+)\s*(?:[:{]|$)/,
      /^(?!(?:if|for|while|switch|return|else|do|case|typedef|using)\b)[A-Za-z_][\w\s*&:<>,~]*?[\s*&]\**([A-Za-z_~][\w:~]*)\s*\([^;]*$/,
    ],
  },
  { exts: ['.rb'], maxIndent: 4, patterns: [/^def\s+((?:self\.)?[\w.?!=]+)/, /^(?:class|module)\s+([\w:]+)/] },
  {
    exts: ['.php'],
    maxIndent: 4,
    patterns: [/^(?:(?:abstract|final|public|private|protected|static|readonly)\s+)*(?:function|class|interface|trait|enum)\s+&?(\w+)/],
  },
  { exts: ['.sh', '.bash', '.zsh'], maxIndent: 0, patterns: [/^function\s+([\w.:-]+)/, /^([\w.:-]+)\s*\(\)/] },
  { exts: ['.ps1', '.psm1'], maxIndent: 4, patterns: [/^(?:function|filter|class|enum)\s+(?:(?:global|script|local|private):)?([\w-]+)/i] },
  { exts: ['.toml'], maxIndent: 0, patterns: [/^\[\[?\s*([^\]]+?)\s*\]\]?\s*(?:#.*)?$/] },
  { exts: ['.md', '.mdx', '.markdown'], markdown: true },
];
const languageByExt = new Map(languages.flatMap((language) => language.exts.map((ext) => [ext, language])));

const command = `node ${path.relative(process.cwd(), fileURLToPath(import.meta.url)).split(path.sep).join('/')}`;
const usage = `Usage: ${command} [path ...] [--budget <chars>]
Prints the file tree with line counts and each file's symbols and headings with line numbers.
Pass directories or files to zoom in. The default budget is ${DEFAULT_BUDGET} characters.`;

main();

function main() {
  const { scopes, budget } = parseArgs(process.argv.slice(2));
  const root = git(process.cwd(), ['rev-parse', '--show-toplevel']).trim();
  const scopePaths = scopes.map((scope) => toRepoPath(root, scope));
  const files = listFiles(root).filter((file) => scopePaths.length === 0 || scopePaths.some((scope) => isWithin(file, scope)));
  if (files.length === 0) {
    fail(scopePaths.length === 0 ? 'No files to map.' : `No mapped files under ${scopePaths.join(', ')}. Paths excluded by .gitignore or .ignore aren't mapped; read them directly.`);
  }
  const entries = files.map((file) => describe(root, file, files.length === 1)).filter(Boolean);
  const map = renderMap(entries, scopePaths, budget);
  process.stdout.write(map);
}

function parseArgs(args) {
  const scopes = [];
  let budget = DEFAULT_BUDGET;
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '-h' || arg === '--help') {
      process.stdout.write(`${usage}\n`);
      process.exit(0);
    } else if (arg === '--budget' || arg.startsWith('--budget=')) {
      const value = arg === '--budget' ? args[++i] : arg.slice('--budget='.length);
      budget = Number(value);
      if (!Number.isInteger(budget) || budget < 1000) fail(`--budget needs a whole number of characters, at least 1000.\n${usage}`);
    } else if (arg.startsWith('-')) {
      fail(`Unknown option ${arg}.\n${usage}`);
    } else {
      scopes.push(arg);
    }
  }
  return { scopes, budget };
}

function git(cwd, args) {
  try {
    return execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (error) {
    fail(`git ${args[0]} failed; run this inside a Git work tree.\n${error.stderr || error.message}`);
  }
}

// Tracked files plus untracked files that .gitignore doesn't exclude, minus anything the root .ignore excludes.
function listFiles(root) {
  const excludes = ['--exclude-standard', ...(existsSync(path.join(root, '.ignore')) ? ['--exclude-from=.ignore'] : [])];
  const split = (output) => output.split('\0').filter(Boolean);
  const excluded = new Set(split(git(root, ['ls-files', '-z', '--cached', '--ignored', ...excludes])));
  const files = split(git(root, ['ls-files', '-z', '--cached', '--others', ...excludes]));
  return [...new Set(files)].filter((file) => !excluded.has(file));
}

function toRepoPath(root, scope) {
  const relative = path.relative(root, path.resolve(scope)).split(path.sep).join('/');
  if (relative.startsWith('../') || relative === '..' || path.isAbsolute(relative)) fail(`${scope} is outside the repository.`);
  return relative;
}

function isWithin(file, scope) {
  return scope === '' || file === scope || file.startsWith(`${scope}/`);
}

function describe(root, file, allSymbols) {
  const absolute = path.join(root, file);
  let stats;
  try {
    stats = lstatSync(absolute);
  } catch {
    return null; // Tracked but deleted from the work tree.
  }
  const entry = { path: file, name: path.posix.basename(file), symbols: [], symbolTotal: 0 };
  if (stats.isSymbolicLink()) {
    entry.link = readlinkSync(absolute).split(path.sep).join('/');
    return entry;
  }
  if (stats.isDirectory()) {
    entry.info = 'submodule';
    return entry;
  }
  const content = readFileSync(absolute);
  if (content.subarray(0, BINARY_SNIFF_BYTES).includes(0)) {
    entry.info = `binary, ${formatNumber(Math.ceil(content.length / 1024))} KB`;
    return entry;
  }
  entry.lines = countLines(content);
  entry.info = count(entry.lines, 'line');
  const language = languageByExt.get(path.posix.extname(file).toLowerCase());
  if (language && content.length <= MAX_PARSED_BYTES) {
    const symbols = findSymbols(content.toString('utf8'), language);
    entry.symbolTotal = symbols.length;
    entry.symbols = allSymbols ? symbols : symbols.slice(0, MAX_SYMBOLS_PER_FILE);
  }
  return entry;
}

function countLines(content) {
  let lines = 0;
  for (let i = content.indexOf(10); i !== -1; i = content.indexOf(10, i + 1)) lines++;
  return content.length > 0 && content[content.length - 1] !== 10 ? lines + 1 : lines;
}

function findSymbols(text, language) {
  const symbols = [];
  let inFence = false;
  text.split(/\r?\n/).forEach((line, index) => {
    if (language.markdown) {
      if (/^ {0,3}(?:```|~~~)/.test(line)) inFence = !inFence;
      const heading = !inFence && /^(#{1,3})\s+(.+?)(?:\s+#+)?\s*$/.exec(line);
      if (heading) symbols.push({ line: index + 1, name: truncate(`${heading[1]} ${heading[2]}`) });
      return;
    }
    const trimmed = line.trimStart();
    const indent = line.slice(0, line.length - trimmed.length).replace(/\t/g, '    ').length;
    if (indent > language.maxIndent || trimmed === '') return;
    for (const pattern of language.patterns) {
      const match = pattern.exec(trimmed);
      const name = match?.slice(1).find((group) => group !== undefined);
      if (name) {
        symbols.push({ line: index + 1, name: truncate(name) });
        break;
      }
    }
  });
  return symbols;
}

// Renders the richest map that fits: every symbol; then symbols hidden file by file, longest lists first; then
// the bare tree with directories collapsed into totals. Truncates as a last resort.
function renderMap(entries, scopePaths, budget) {
  const tree = buildTree(entries);
  const totalLines = entries.reduce((sum, entry) => sum + (entry.lines ?? 0), 0);
  const scope = scopePaths.every((scopePath) => scopePath === '') ? 'the repository' : scopePaths.join(', ');
  const summary = `Map of ${scope}: ${count(entries.length, 'file')}, ${count(totalLines, 'line')}, excluding .gitignore and .ignore matches. Zoom in with: ${command} <dir-or-file> [--budget <chars>]`;
  const legend = 'Files show their line count; the "L<n> <name>" lines under a file are its symbols and headings.';

  const detailed = [summary, legend, ...renderTree(tree, { symbols: true, collapsed: new Set() })];
  if (size(detailed) <= budget) return finish(detailed);

  const partialLegend = `${legend} Files showing a symbol count have theirs hidden to fit; map such a file to list them.`;
  let total = size([summary, partialLegend, ...detailed.slice(2)]);
  const byCost = entries.filter((entry) => entry.symbols.length > 0).map((entry) => ({ entry, cost: size(symbolLines(entry)) }));
  for (const { entry, cost } of byCost.sort((a, b) => b.cost - a.cost)) {
    if (total <= budget) break;
    entry.symbolsHidden = true;
    total -= cost - (fileLabel(entry, true).length - fileLabel(entry, false).length);
  }
  if (total <= budget) return finish([summary, partialLegend, ...renderTree(tree, { symbols: true, collapsed: new Set() })]);

  const omitted = 'Symbols are omitted to fit the budget; zoom in to see them.';
  const collapsedNote = `${omitted} Directories showing totals are collapsed.`;
  const collapsed = collapseToFit(tree, budget - size([summary, collapsedNote]));
  const outline = [summary, collapsed.size > 0 ? collapsedNote : omitted, ...renderTree(tree, { symbols: false, collapsed })];
  if (size(outline) <= budget) return finish(outline);

  const tailReserve = 100;
  const kept = [];
  let chars = 0;
  for (const line of outline) {
    if (chars + line.length + 1 > budget - tailReserve) break;
    kept.push(line);
    chars += line.length + 1;
  }
  kept.push(`... ${formatNumber(outline.length - kept.length)} more lines omitted to fit the budget. Zoom into a directory.`);
  return finish(kept);
}

// The number of characters the lines take in the output.
function size(lines) {
  return lines.reduce((sum, line) => sum + line.length + 1, 0);
}

function finish(lines) {
  return `${lines.join('\n')}\n`;
}

function buildTree(entries) {
  const root = newDirectory('');
  for (const entry of entries) {
    let directory = root;
    const parts = entry.path.split('/');
    for (const part of parts.slice(0, -1)) {
      if (!directory.directories.has(part)) directory.directories.set(part, newDirectory(part));
      directory = directory.directories.get(part);
    }
    directory.files.push(entry);
  }
  summarize(root);
  for (const child of root.directories.values()) mergeSingleChildChains(child);
  place(root, null, -1);
  return root;
}

// Records each directory's parent and the depth at which every directory and file is rendered.
function place(directory, parent, depth) {
  Object.assign(directory, { parent, depth });
  for (const file of directory.files) file.depth = depth + 1;
  for (const child of directory.directories.values()) place(child, directory, depth + 1);
}

function newDirectory(name) {
  return { name, directories: new Map(), files: [], fileCount: 0, lineCount: 0 };
}

function summarize(directory) {
  directory.fileCount = directory.files.length;
  directory.lineCount = directory.files.reduce((sum, file) => sum + (file.lines ?? 0), 0);
  for (const child of directory.directories.values()) {
    summarize(child);
    directory.fileCount += child.fileCount;
    directory.lineCount += child.lineCount;
  }
}

// Shows a/b/c/ as one entry when a and b hold nothing but the next directory.
function mergeSingleChildChains(directory) {
  while (directory.files.length === 0 && directory.directories.size === 1) {
    const [only] = directory.directories.values();
    directory.name = `${directory.name}/${only.name}`;
    directory.directories = only.directories;
    directory.files = only.files;
  }
  for (const child of directory.directories.values()) mergeSingleChildChains(child);
}

function sortedChildren(directory) {
  const byName = (a, b) => a.name.localeCompare(b.name, 'en', { sensitivity: 'base' });
  return { directories: [...directory.directories.values()].sort(byName), files: [...directory.files].sort(byName) };
}

function indent(depth) {
  return '  '.repeat(depth);
}

function lineSize(depth, text) {
  return size([indent(depth) + text]);
}

function directoryLabel(directory) {
  return `${directory.name}/`;
}

function collapsedLabel(directory) {
  return `${directory.name}/ (${count(directory.fileCount, 'file')}, ${count(directory.lineCount, 'line')})`;
}

function fileLabel(file, withSymbolCount) {
  if (file.link) return `${file.name} -> ${file.link}`;
  return `${file.name} (${file.info}${withSymbolCount ? `, ${count(file.symbolTotal, 'symbol')}` : ''})`;
}

function symbolLines(file) {
  const lines = file.symbols.map((symbol) => `${indent(file.depth + 1)}L${symbol.line} ${symbol.name}`);
  const more = file.symbolTotal - file.symbols.length;
  if (more > 0) lines.push(`${indent(file.depth + 1)}... ${count(more, 'more symbol')}; map this file to list them all`);
  return lines;
}

function renderTree(root, options) {
  const out = [];
  const visit = (directory) => {
    const { directories, files } = sortedChildren(directory);
    for (const child of directories) {
      const collapsed = options.collapsed.has(child);
      out.push(indent(child.depth) + (collapsed ? collapsedLabel(child) : directoryLabel(child)));
      if (!collapsed) visit(child);
    }
    for (const file of files) {
      const hidden = options.symbols && file.symbolsHidden;
      out.push(indent(file.depth) + fileLabel(file, hidden));
      if (options.symbols && !hidden) out.push(...symbolLines(file));
    }
  };
  visit(root);
  return out;
}

// Collapses directories until the tree fits the budget: deepest first so the outline keeps an even depth, and the
// largest first within a depth so cheap detail survives. Only fully collapsed subtrees qualify. Returns the set.
function collapseToFit(root, budget) {
  const candidates = [];
  const measure = (directory) => {
    directory.chars = 0;
    if (directory.parent) {
      directory.chars += lineSize(directory.depth, directoryLabel(directory));
      candidates.push(directory);
    }
    for (const file of directory.files) directory.chars += lineSize(file.depth, fileLabel(file, false));
    for (const child of directory.directories.values()) {
      measure(child);
      directory.chars += child.chars;
    }
  };
  measure(root);
  const collapsed = new Set();
  // Collapsing the only top-level entry would leave nothing to see.
  const keepTop = root.files.length === 0 && root.directories.size === 1;
  while (root.chars > budget) {
    let pick = null;
    for (const directory of candidates) {
      if (collapsed.has(directory) || (keepTop && directory.parent === root)) continue;
      if ([...directory.directories.values()].some((child) => !collapsed.has(child))) continue;
      if (!pick || directory.depth > pick.depth || (directory.depth === pick.depth && directory.chars > pick.chars)) pick = directory;
    }
    if (!pick) break;
    const charsSaved = pick.chars - lineSize(pick.depth, collapsedLabel(pick));
    collapsed.add(pick);
    for (let directory = pick; directory; directory = directory.parent) directory.chars -= charsSaved;
  }
  return collapsed;
}

function truncate(text) {
  return text.length > MAX_NAME_LENGTH ? `${text.slice(0, MAX_NAME_LENGTH - 3)}...` : text;
}

function formatNumber(value) {
  return value.toLocaleString('en-US');
}

function count(value, noun) {
  return `${formatNumber(value)} ${noun}${value === 1 ? '' : 's'}`;
}

function fail(message) {
  process.stderr.write(`${message}\n`);
  process.exit(1);
}
