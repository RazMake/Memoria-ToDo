# Architecture

This chapter holds Memoria-ToDo's design: what each part does, how parts depend on each other, and the decisions still to confirm. The [purpose and scope](purpose.md), [how the apps fit together](overview.md), the [module rules and map](modules.md), the [desktop app](desktop/README.md), the [Editing](editing.md), [Rendering](rendering.md), [Tasks](tasks.md), [Contacts](contacts.md), and [Sync through git](sync.md) modules, [version tags](version-tags.md), [releasing a version](releasing.md), [how each AI tool finds a skill](agent-skills.md), and the [local API](local-api.md) are decided. The [Android app](android/README.md) is decided too. The desktop's [empty-clone examples](desktop/configure.md#cloning-an-empty-repository-adds-examples-on-the-desktop-only) and [hand-edit synchronization](desktop/README.md#hand-edits-to-the-data-type-files-sync-after-30-quiet-seconds) are decided, not implemented. The [technology stack](technology-stack.md) is drafting again: one question, which test runner runs the Android test app, blocks the Android app's second build step. The desktop app and the shared modules it uses have code, for the built steps of the desktop [delivery backlog](desktop/README.md#delivery-backlog); the Android app's own [delivery backlog](android/README.md#delivery-backlog) lists the steps that build it, and which are built.

| If you want to | Read |
| --- | --- |
| Learn what the apps do, and for whom | [Purpose and scope](purpose.md) |
| See the apps' parts, their technology, and how they connect | [How the apps fit together](overview.md) |
| Learn how code is split into modules, and what a change may touch | [Modules](modules.md) |
| Learn how the desktop app is built | [Desktop app](desktop/README.md) |
| Learn how the Android app is built | [Android app](android/README.md) |
| Learn how the code is built, tested, and released | [Technology stack](technology-stack.md) |
| Learn which tests a change runs, and how many UX tests a screen keeps | [Test selection](test-selection.md) |
| Learn what the app logs, and where the logs are kept | [Logging](logging.md) |
| Reach your tasks from your own programs on this PC | [Local API](local-api.md), and [what programs can call](local-api-contract.md) |
| Preview or clear a version tag | [Version tags](version-tags.md) |
| Publish a version, or run every test in the cloud, from GitHub's Actions page | [Release a version](releasing.md) |
| Add a skill for AI tools, or learn how each tool finds one | [Agent skills](agent-skills.md) |
| Start a design file for a new area | [Design file template](template.md) |
| Learn how a design decision gets made | [Changing the design](../contributing/process.md#changing-the-design) |

## Design backlog

Each row is one area to design, owned by one design file. An app row is owned instead by the app's design folder, which holds one design file per module of that app. Work on the first row that isn't **Decided**; a row marked **Drafting** resumes where its design file's open questions left off. Rows designed together are designed in one conversation, each into its own design file.

Status is one of **Open**, **Drafting**, **Decided**, or **Implemented**, as [the process](../contributing/process.md) defines.

Proposed: the technology stack row's place and scope. This is a decision to confirm.

| # | Area | Design file | Status |
| --- | --- | --- | --- |
| 1 | Purpose and scope: what the application does, and for whom | [Purpose and scope](purpose.md) | Decided |
| 2 | High-level architecture: the apps, the shared parts, and how they connect | [How the apps fit together](overview.md) | Decided |
| 3 | How each module marks its public interface, so a reviewer finds it fast | [Modules](modules.md) | Decided |
| 4 | The first modules and the [module map](modules.md#module-map) | [Modules](modules.md) | Decided |
| 5 | Desktop app: its own Shell, Task editor, Task list, Contact list, Formatted view, Configure, and Updates modules. Designed together with Editing and Rendering. | [Desktop app](desktop/README.md), folder `desktop/` | Decided |
| 6 | Editing: the formatting, snippet, and list commands. Designed together with the desktop app. | [Editing](editing.md) | Decided |
| 7 | Rendering: Markdown to HTML, which Markdig extensions, and how images and links render. Designed together with the desktop app. | [Rendering](rendering.md) | Decided |
| 8 | Tasks: the task file format, task IDs, rank, merge rules, automatic deletion, and images. Designed together with sync. | [Tasks](tasks.md) | Decided |
| 9 | Sync through git: when and how changes travel, and the device's SSH key. Designed together with Tasks. | [Sync through git](sync.md) | Decided |
| 10 | Android app: its own Shell, Task editor, Task list, and Configure modules | [Android app](android/README.md), folder `android/` | Decided |
| 11 | Technology stack: how the code is laid out; test tools; the one command that builds the code and runs every test; whether the desktop app carries its own .NET runtime; the release step that publishes the desktop installer and the Android package to GitHub Releases, as the desktop [Shell](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date) and the [Android app](android/README.md#later-designs-answer-what-these-files-leave-open) need; the build of libgit2 for Android, and where the key that signs the Android package is kept; and an architecture test that compares project references with the [module map](modules.md#module-map). The language, .NET version, and main libraries are in [How the apps fit together](overview.md). Must be **Decided** before any product code is written. The throwaway proofs are exempt and run on a branch that's never merged, as the [technology stack](technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided) says. | [Technology stack](technology-stack.md) | Drafting |
| 12 | Version tags: the script that previews the next version tag and, in a release run, makes and pushes it; the skill that clears one; how the release run checks a tag; and how their tests run. Designed together with Agent skills. | [Version tags](version-tags.md) | Decided |
| 13 | Agent skills: how each AI tool finds a skill, the pointer `generate.mjs` writes for Claude Code, and the rules a skill must meet. Designed together with Version tags. | [Agent skills](agent-skills.md) | Decided |
| 14 | Releasing: automatic full tests, browser-started releases, their VS Code shortcut and optional terminal commands, and exact-commit test reuse. | [Release a version](releasing.md) | Decided |
| 15 | Contacts: a shared module for the contact files, how contacts relate, how they sync and are searched, the data types key fields choose from, and how a task mentions one through a template. Designed together with the desktop [Contact list](desktop/contact-list.md). | [Contacts](contacts.md) | Decided |
| 16 | Logging: Serilog's logger in every module, what each module logs, the desktop's log files and their limits, keeping secrets out, errors no module handles, and the Serilog packages. Designed together with the desktop [Logs window](desktop/logs.md). | [Logging](logging.md) | Decided |
| 17 | Test selection: which tests final validation runs for a change, narrowed to changed screens, the script that picks them, the full suite in the cloud's Run All tests, which each release runs or reuses, and the limit of 10 UX tests per screen with the plan that shrinks today's UX tests | [Test selection](test-selection.md) | Decided |
| 18 | Local API: gRPC over a named pipe only your Windows account can open, hosted by the desktop app; the one contract; the generated .NET and Node.js clients; and their release files | [Local API](local-api.md), with its [contract](local-api-contract.md) | Decided |
| 19 | MCP server: a desktop module that lets an AI assistant do the user's operations through the Model Context Protocol while the app runs; which operations, how assistants connect, and who may call it. Designed together with the changes it brings to [Tasks](tasks.md#direct-task-changes-are-a-capability-of-their-own), [Shell](desktop/shell.md#the-mcp-server-starts-once-the-clone-has-loaded), and [Configure](desktop/configure.md#the-ai-assistants-row-lets-assistants-in-and-examples-shows-how-to-connect-them). | [MCP server](desktop/mcp-server.md) | Decided |
| 21 | Image move rollback: a task's images move before its file is written, so a failed write leaves the images moved with nothing pointing to them ([`TaskService.cs`](../../src/Shared/Tasks/TaskService.cs)). Decide what a failed write undoes. | [Tasks](tasks.md) | Implemented |
| 22 | Tag order cleanup: remove the bounds checks in the tag control's `TagOrder`, which repeat the errors `List.RemoveAt` and `Insert` already raise. Behavior stays the same. | [Task editor](desktop/task-editor.md) | Implemented |
| 23 | Keep awake: a desktop module that keeps the screen on while Prevent screen saver is on, pauses while the PC is locked, and shows its state in Task View's status bar. Designed together with row 24. | [Keep awake](desktop/keep-awake.md) | Implemented |
| 24 | Settings and status bar changes: Configure's Behavior section, Check for updates, and the AI assistants Examples dialog; What is new's scrollbar and Update Now; the wider gap before the status bar's update text; and Task View remembering its size and place on this device. Designed together with row 23. | [Desktop app](desktop/README.md), folder `desktop/`: [Configure](desktop/configure.md), [Updates](desktop/updates.md), [Task list](desktop/task-list.md), [MCP server](desktop/mcp-server.md), and the [saved place](desktop/README.md#a-saved-place-reopens-a-window-where-it-was) | Implemented |
| 25 | Saved changes whose notices fail: a store that saved a change, and then had one or more notices or its local save report fail, throws one type, so Add, Task View, Contacts, Configure, the MCP server, and the Local API show and return the change as saved, and a sync pass carries on. Applies to Tasks and Contacts. | [Tasks](tasks.md#failure-modes), with the platform's [rule](modules.md#steps-that-must-all-run-throw-their-failures-together), [Contacts](contacts.md#failure-modes), and the desktop's [store failures](desktop/README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them) | Implemented |
| 26 | Examples for an empty repository | [Configure](desktop/configure.md#cloning-an-empty-repository-adds-examples-on-the-desktop-only) | Decided |
| 27 | Hand-edited data type files sync | [Desktop app](desktop/README.md#hand-edits-to-the-data-type-files-sync-after-30-quiet-seconds) | Decided |