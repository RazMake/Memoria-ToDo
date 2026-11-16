#!/usr/bin/env node
// Checks that this PC's desktop can run screen tests, as docs/architecture/technology-stack.md
// ("Agents check the desktop before they start screen tests") describes. It prints one line.
// Exit code 0: the desktop is usable. 1: it isn't, and the line says why and how to fix it. 2: the check itself failed.
// It needs only Node.js's own modules and Windows PowerShell.
// Usage: node .agents/desktop-check.mjs
import { execFile } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const usage = 'usage: node .agents/desktop-check.mjs (it takes no arguments)';
const fix = 'Unlock the PC, or reconnect the Remote Desktop session and keep its window open, then run the tests again.';
const probeTimeoutMs = 30_000;

const fields = { connectState: 'number', connectError: 'number', inputDesktop: 'string', inputDesktopError: 'number', cursorError: 'number' };
const isWellFormed = (report) => report !== null && typeof report === 'object'
  && Object.entries(fields).every(([name, type]) => report[name] === null || typeof report[name] === type);

const WTS_ACTIVE = 0;

/** Decides from what Windows reported, in the order connection, desktop, pointer. Returns the exit code and the one line to print. */
export function decide(report) {
  if (!isWellFormed(report)) return { code: 2, message: 'The check got a malformed report from Windows.' };
  if (report.connectError !== null) return { code: 2, message: `Windows could not report the session's connect state (error ${report.connectError}).` };
  if (report.connectState !== WTS_ACTIVE) return { code: 1, message: `The session is not connected. ${fix}` };
  if (report.inputDesktopError !== null || report.inputDesktop !== 'Default') return { code: 1, message: `The PC is locked, or a secure prompt is showing. ${fix}` };
  if (report.cursorError !== null) return { code: 1, message: `Windows does not give the pointer's position (error ${report.cursorError}). ${fix}` };
  return { code: 0, message: 'The desktop is usable for screen tests.' };
}

/** Runs the check: `args` are the command-line arguments, `platform` is process.platform, and `probe` returns Windows's report. */
export async function run({ args, platform, probe }) {
  if (args.length) return { code: 2, message: usage };
  if (platform !== 'win32') return { code: 1, message: 'Screen tests run only on Windows, and this is not Windows.' };
  try {
    return decide(await probe());
  } catch (error) {
    return { code: 2, message: `The desktop check failed: ${String(error.message).trim().replace(/\s*\n\s*/g, ' ')}` };
  }
}

// Reports raw facts only. A failed call leaves its value null and sets its error number; decide() reads them.
// When the input desktop opens but its name can't be read, the name stays null and that call's error goes in inputDesktopError.
const probeScript = `
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class DesktopReport {
  public int? connectState; public int? connectError;
  public string inputDesktop; public int? inputDesktopError;
  public int? cursorError;
}
public static class DesktopCheck {
  [DllImport("user32.dll", SetLastError=true)] static extern IntPtr OpenInputDesktop(uint f, bool i, uint a);
  [DllImport("user32.dll", SetLastError=true)] static extern bool GetUserObjectInformation(IntPtr h, int n, StringBuilder b, int l, out int needed);
  [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] struct P { public int X, Y; }
  [DllImport("user32.dll", SetLastError=true)] static extern bool GetCursorPos(out P p);
  [DllImport("wtsapi32.dll", SetLastError=true)] static extern bool WTSQuerySessionInformation(IntPtr s, int id, int c, out IntPtr buf, out int bytes);
  [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr p);
  public static DesktopReport Probe() {
    var r = new DesktopReport();
    IntPtr buf; int bytes;
    if (WTSQuerySessionInformation(IntPtr.Zero, -1, 8, out buf, out bytes)) {
      r.connectState = Marshal.ReadInt32(buf);
      WTSFreeMemory(buf);
    } else {
      r.connectError = Marshal.GetLastWin32Error();
    }
    IntPtr desktop = OpenInputDesktop(0, false, 0x0001);
    if (desktop == IntPtr.Zero) {
      r.inputDesktopError = Marshal.GetLastWin32Error();
    } else {
      var name = new StringBuilder(256); int needed;
      if (GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out needed)) r.inputDesktop = name.ToString();
      else r.inputDesktopError = Marshal.GetLastWin32Error();
      CloseDesktop(desktop);
    }
    P p;
    if (!GetCursorPos(out p)) r.cursorError = Marshal.GetLastWin32Error();
    return r;
  }
}
'@
[DesktopCheck]::Probe() | ConvertTo-Json -Compress
`;

/** Asks Windows through Windows PowerShell, which compiles a small C# class, and returns its report. Rejects when it fails or takes over 30 seconds. */
export function probeWindows() {
  const encoded = Buffer.from(probeScript, 'utf16le').toString('base64');
  return new Promise((resolve, reject) => {
    const child = execFile('powershell.exe', ['-NoProfile', '-NonInteractive', '-EncodedCommand', encoded],
      { encoding: 'utf8', timeout: probeTimeoutMs, windowsHide: true }, (error, stdout, stderr) => {
        if (error?.killed) return reject(new Error(`Windows PowerShell took over ${probeTimeoutMs / 1000} seconds`));
        if (error) return reject(new Error(stderr.trim() || error.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          reject(new Error(`Windows PowerShell printed something that is not a report: ${stdout.trim()}`));
        }
      });
    child.stdin.end();
  });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const { code, message } = await run({ args: process.argv.slice(2), platform: process.platform, probe: probeWindows });
  (code === 0 ? process.stdout : process.stderr).write(`${message}\n`);
  process.exitCode = code;
}
