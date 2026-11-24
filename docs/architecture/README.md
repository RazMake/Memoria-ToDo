# Architecture

This chapter holds Memoria-ToDo's design: what each part does, how parts depend on each other, and the decisions still to confirm. The [purpose and scope](purpose.md), [how the apps fit together](overview.md), the [module rules and map](modules.md), the [Android app](android/README.md), the [desktop app](desktop/README.md), the [Editing](editing.md), [Rendering](rendering.md), [Tasks](tasks.md), [Contacts](contacts.md), and [Sync through git](sync.md) modules, [version tags](version-tags.md), [releasing a version](releasing.md), [how each AI tool finds a skill](agent-skills.md), and the [technology stack](technology-stack.md) are decided. The desktop app and the shared modules it uses have code, for the built steps of the desktop [delivery backlog](desktop/README.md#delivery-backlog); the Android app has none yet.

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
| Preview or clear a version tag | [Version tags](version-tags.md) |
| Publish a version, or run every test in the cloud, with one guided command | [Release a version](releasing.md) |
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
| 11 | Technology stack: how the code is laid out; test tools; the one command that builds the code and runs every test; whether the desktop app carries its own .NET runtime; the release step that publishes the desktop installer and the Android package to GitHub Releases, as the desktop [Shell](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date) and the [Android app](android/README.md#later-designs-answer-what-these-files-leave-open) need; the build of libgit2 for Android, and where the key that signs the Android package is kept; and an architecture test that compares project references with the [module map](modules.md#module-map). The language, .NET version, and main libraries are in [How the apps fit together](overview.md). Must be **Decided** before any product code is written. The throwaway proofs are exempt and run on a branch that's never merged, as the [technology stack](technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided) says. | [Technology stack](technology-stack.md) | Decided |
| 12 | Version tags: the script that previews the next version tag and, in a release run, makes and pushes it; the skill that clears one; how the release run checks a tag; and how their tests run. Designed together with Agent skills. | [Version tags](version-tags.md) | Decided |
| 13 | Agent skills: how each AI tool finds a skill, the pointer `generate.mjs` writes for Claude Code, and the rules a skill must meet. Designed together with Version tags. | [Agent skills](agent-skills.md) | Decided |
| 14 | Guided release: the two workflows you start by hand, Run All tests and Test then Tag and Release; the commands and VS Code tasks that start and follow them, download the installer, and guide manual checks; and the check that lets a release reuse a passed Run All tests run. | [Release a version](releasing.md) | Decided |
| 15 | Contacts: a shared module for the contact files, how contacts relate, how they sync and are searched, the data types key fields choose from, and how a task mentions one through a template. Designed together with the desktop [Contact list](desktop/contact-list.md). | [Contacts](contacts.md) | Decided |
| 16 | Logging: Serilog's logger in every module, what each module logs, the desktop's log files and their limits, keeping secrets out, errors no module handles, and the Serilog packages. Designed together with the desktop [Logs window](desktop/logs.md). | [Logging](logging.md) | Decided |
| 17 | Test selection: which tests final validation runs for a change, narrowed to changed screens, the script that picks them, the full suite in the cloud's Run All tests, which each release runs or reuses, and the limit of 10 UX tests per screen with the plan that shrinks today's UX tests | [Test selection](test-selection.md) | Decided |
