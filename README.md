# Memoria-ToDo

Memoria-ToDo will be a Windows desktop app and an Android phone app for capturing tasks fast, kept in sync through a git repository. Its [purpose](docs/architecture/purpose.md) and design are decided. The desktop app's first steps are built: a tray icon whose windows say what's coming, an installer, and automatic updates. The repository also holds the rules every change follows and the AI agents that design, build, test, and review it.

| If you want to | Read |
| --- | --- |
| Learn the rules every change follows, human or AI | [`AGENTS.md`](AGENTS.md) |
| See the design and the decisions still to confirm | [Architecture](docs/architecture/README.md) |
| Follow a change from request to review | [Process](docs/contributing/process.md) |
| Write or change documentation | [Writing docs](docs/contributing/writing-docs.md) |
| Run the desktop app with F5 | [What you need](#what-you-need) |
| Publish a new version: push a tag such as `v1.2.3` | [The release workflow](docs/architecture/technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) |

## What you need

- **Node.js**, to run the AI tooling in [.agents/](.agents/): the repository map and the agent file generator.
- **An AI coding tool**: GitHub Copilot, OpenAI Codex, or Claude Code v2.1.284 or later ([required for Sonnet 5.5](https://code.claude.com/docs/en/model-config)). Each one reads [`AGENTS.md`](AGENTS.md) and the agents generated from [.agents/subagents/](.agents/subagents/).
- **The .NET 11 SDK**, the version [`global.json`](global.json) pins. It installs beside any other SDK, in a folder just for this repository, without administrator rights. From the repository root, in PowerShell:

  ```powershell
  Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1"
  & "$env:TEMP\dotnet-install.ps1" -JSonFile global.json -InstallDir "$env:LOCALAPPDATA\Memoria-ToDo\dotnet"
  ```

  Each PowerShell session that builds or tests puts that folder first, so `dotnet` is the pinned one ([why](docs/architecture/technology-stack.md#three-files-pin-every-version)). Visual Studio Code's terminals in this workspace already do, once you trust the workspace; open a new terminal after the first setup. Other sessions run:

  ```powershell
  $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Memoria-ToDo\dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
  ```

- **Visual Studio Code with the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)** (`ms-dotnettools.csharp`). F5 builds and runs the desktop app with the debugger, and Ctrl+F5 runs it without. Both use the pinned SDK. Code completion needs Visual Studio Code to find that SDK on `PATH`, so start it from the repository root like this ([why](docs/architecture/technology-stack.md#visual-studio-code-runs-the-desktop-app-with-f5-on-the-pinned-sdk)):

  ```powershell
  $env:PATH = "$env:LOCALAPPDATA\Memoria-ToDo\dotnet;$env:PATH"; code .
  ```
