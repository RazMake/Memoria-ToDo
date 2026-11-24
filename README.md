# Memoria-ToDo

Memoria-ToDo will be a Windows desktop app and an Android phone app for capturing tasks fast, kept in sync through a git repository. Its [purpose](docs/architecture/purpose.md) and design are decided. The desktop app adds, views, and syncs tasks from its tray icon. The [delivery backlog](docs/architecture/desktop/README.md#delivery-backlog) tracks the remaining desktop work and the Android app. The repository also holds the rules every change follows and the AI agents that design, build, test, and review it.

| If you want to | Read |
| --- | --- |
| Learn the rules every change follows, human or AI | [`AGENTS.md`](AGENTS.md) |
| See the design and the decisions still to confirm | [Architecture](docs/architecture/README.md) |
| Follow a change from request to review | [Process](docs/contributing/process.md) |
| Write or change documentation | [Writing docs](docs/contributing/writing-docs.md) |
| See what changed in each version, or add an entry | [Changelog](CHANGELOG.md), and [how to write it](docs/contributing/changelog.md) |
| Let an AI assistant use the desktop app | [Connect an AI assistant](docs/user/connect-an-ai-assistant.md) |
| Run the desktop app with F5 | [What you need](#what-you-need) |
| Publish a version with one guided command | [Release a version](docs/architecture/releasing.md) |
| Clear a version tag | [How a version tag is made and cleared](docs/architecture/version-tags.md) |

## What you need

- **Node.js**, to run the AI tooling in [.agents/](.agents/): the repository map, the agent file generator, and the [version tag script](docs/architecture/version-tags.md).
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

- **Git on `PATH`**, for [Sync's integration tests](docs/architecture/technology-stack.md#syncs-integration-tests-run-an-ssh-server-inside-the-test-process), which run it behind their own SSH server, and for the version tag script. Its version isn't pinned.
- **GitHub CLI (`gh`)**, signed in with `gh auth login`, to [release a version](docs/architecture/releasing.md). The guided command also explains missing sign-in.
- **Visual Studio Code with the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)** (`ms-dotnettools.csharp`). F5 builds and runs the desktop app with the debugger, and Ctrl+F5 runs it without. Both use the pinned SDK. Code completion needs Visual Studio Code to find that SDK on `PATH`, so start it from the repository root like this ([why](docs/architecture/technology-stack.md#visual-studio-code-runs-the-desktop-app-with-f5-on-the-pinned-sdk)):

  ```powershell
  $env:PATH = "$env:LOCALAPPDATA\Memoria-ToDo\dotnet;$env:PATH"; code .
  ```
