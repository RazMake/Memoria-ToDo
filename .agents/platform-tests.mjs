import { pathToFileURL } from 'node:url';
import { androidDeviceTestScript, createPlan, createPorts, formatPlan } from './affected-tests.mjs';
import { createCommands, messageOf } from './github-runs.mjs';
import { extractPlatform } from './release-platforms.mjs';

function argumentsOf(command) {
  const tokens = command.match(/"[^"]*"|[^\s"]+/g) ?? [];
  if (tokens.join(' ') !== command.trim().replace(/\s+/g, ' ')) throw new Error(`Invalid test command: ${command}`);
  return tokens.map(token => token.startsWith('"') ? token.slice(1, -1) : token);
}

export async function runPlan(plan, pcOnly, ports) {
  if (plan.desktopCheck) await ports.execute('node', ['.agents/desktop-check.mjs']);
  const extra = new Set(plan.extras.map(item => item.command));
  const commands = [...extra, ...plan.commands.filter(command => !extra.has(command))];
  for (const command of commands) {
    if (pcOnly && command.startsWith(`node ${androidDeviceTestScript} `)) continue;
    ports.write(`Running ${command}\n`);
    const [file, ...args] = argumentsOf(command);
    if (file === 'dotnet' && args[0] === 'test') args.push('--report-xunit-junit', '--results-directory', 'test-results/pc', '--results-directory-layout', 'per-module');
    await ports.execute(file, args, { live: true });
  }
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  try {
    const args = process.argv.slice(2);
    if (!args.includes('--platform')) throw new Error('--platform desktop|android is required.');
    const extracted = extractPlatform(args);
    if (extracted.args.length > 1 || extracted.args.some(arg => arg !== '--pc-only')) throw new Error('Only --pc-only may accompany --platform.');
    const plan = await createPlan({ platform: extracted.platform.name }, createPorts(process.cwd()));
    process.stdout.write(formatPlan(plan));
    const execute = createCommands(process.cwd());
    await runPlan(plan, extracted.args.includes('--pc-only'), {
      execute: (file, args, options) => process.platform === 'win32' && file === 'npm'
        ? execute(process.env.ComSpec, ['/d', '/c', 'npm.cmd', ...args], options) : execute(file, args, options),
      write: text => process.stdout.write(text),
    });
  } catch (error) {
    process.stderr.write(`${messageOf(error)}\n`);
    process.exitCode = 1;
  }
}
