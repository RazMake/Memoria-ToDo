# Which tests a change runs, and how many UX tests a screen keeps

Status: Decided

A session's final validation runs every kind of test, but only for the modules the session changed and the modules that depend on them, and only for the changed screens when a change stays inside them. The full suite runs only in the cloud, once per release. Each screen keeps at most 10 UX tests, each one following a whole common path. All of it cuts the time a change waits for tests, and the number of screen tests that can fail for reasons outside the code.

## Behavior

The user decided these, on November 22, 2026:

1. **Final validation runs every test kind, unit, UX, and integration, for the modules the session changed and every module that depends on them, directly or through others.** The zero-failed, zero-skipped bar of [Run timing](../../AGENTS.md#run-timing) still holds for whatever runs.
2. **UX stays a kind of its own**, marked `[UX("<Screen>")]`, as the [technology stack](technology-stack.md#attributes-mark-each-tests-kind) says. Renaming or merging it into integration saves no time, since UX tests already run only at the session's end.
3. **Each screen has at most 10 UX tests.** Each covers one whole common user path and checks several behaviors on the way. Edge cases and errors belong in unit tests. A UX test is deleted only after a unit test covers its behavior, or after the user agrees its behavior isn't worth a test.
4. **Each UX test names its screen, and our analyzer counts the tests of each screen across every class**, so the limit holds per screen, not per class. Each UX class names its screen in its attribute, `[UX("<Screen>")]`, and the analyzer fails the build when that name differs from the class's screen folder, as [below](#our-analyzer-counts-each-screens-ux-tests).
8. **Each screen of a module with several screens has its own folder, in its code and its tests**, so the script reads a changed file's screen from its path, as [below](#a-change-inside-screens-runs-only-those-screens-tests).
5. **A change that stays inside one screen, or a few, runs only those screens' tests** of the changed module, as [below](#a-change-inside-screens-runs-only-those-screens-tests).
6. **A release runs the full suite once, in its own Tests job or in a passed Run All tests run of the same commit that it reuses**, as [below](#the-full-suite-runs-only-in-the-cloud).
7. **A change to shared test support selects every test in each test project that references it; the script follows one test folder's use of another's test code; and every module that depends on a changed module runs, whether or not its public interface changed.** The [rules below](#each-changed-path-selects-tests-by-one-rule) apply them.

Why: before this design, a full local run was about 6,800 tests, about 1,140 of them UX tests, and most failures in past sessions came from the desktop, not the code: a locked PC, a disconnected Remote Desktop session, a window that lost focus, or a changed display scale. Fewer screen tests, run only where a change can break them, fail less often and finish sooner. The UX tests are now [100 across 18 screens](#existing-ux-tests-shrink-one-screen-at-a-time).

### The full suite runs only in the cloud

Every test, of every kind, runs only in the [Run All tests](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) workflow on GitHub, which you start by hand and each release run calls. No session runs the full suite on your PC, unless a change selects every module, as the [rules below](#each-changed-path-selects-tests-by-one-rule) say for build files.

- **A [release run](releasing.md#a-release-run-plans-tests-tags-then-publishes) tags and publishes only after every test passed on the commit it tags.** The [release command](releasing.md#the-release-command-previews-asks-once-then-follows-the-run) runs no .NET test on your PC.
- **A release run [reuses a passed Run All tests run](releasing.md#a-release-reuses-a-passed-run-all-tests-run-of-its-commit) of its commit, and runs every test itself only when there's none.** So a release is checked by every test, once.
- **Every delivery report says what final validation ran.** When it ran less than every test, the report says the rest runs only in Run All tests, since a test in a module the session didn't select can only fail there.

### A script turns the changed files into the commands to run

`node .agents/affected-tests.mjs` prints the final-validation commands for the session's changes. It sits with the other repository scripts in `.agents/`, and its tests in `affected-tests.test.mjs`, as [AGENTS.md](../../AGENTS.md#checking-the-agents-scripts) says.

1. **It finds the changed files.** By default these are the working tree's changes against `HEAD`, staged or not, plus untracked files that git doesn't ignore, since only you commit and a session's work is usually uncommitted. With `--base <ref>`, it compares against that commit instead, for a session in which you committed. A renamed file counts at both its old and its new path.
2. **It reads the project graph from the project files.** It reads each `ProjectReference` in every `.csproj` under `src/` and `tests/`, so the graph is never a hand-kept list. The [architecture test](technology-stack.md#architecture-tests-check-the-module-map) already holds those references to the [module map](modules.md#module-map).
3. **It maps each changed path to what it selects**, by the [rules below](#each-changed-path-selects-tests-by-one-rule), narrowing a changed module to its [changed screens](#a-change-inside-screens-runs-only-those-screens-tests) when every changed file of the module sits in a screen folder.
4. **It adds every project that depends on a changed product project**, through any number of steps, and selects each one's whole test folder, never narrowed to screens. Only product projects under `src/` count as dependents. A test project, a Fakes project, or the platform test host depends on many modules, but changing a module never selects every test through it.
5. **It adds every test folder that uses a selected folder's test code**, through any number of steps. A test file uses another folder's test code when it names that folder's namespace in a `using` directive or a qualified name. A qualified name may be relative, such as `TaskEditor.Tests.EditorFields` inside `Memoria.ToDo.Desktop.Logs.Tests`; it's resolved against the file's namespace, as C# resolves it. A screen folder always uses its module folder's code, since C# lets a child namespace see its parent's types without a `using`.
6. **It turns each selection into a namespace**: a module's test folder, `Memoria.ToDo.<Part>.<Module>.Tests`, as the [technology stack](technology-stack.md#each-part-keeps-its-tests-in-one-test-project) names it, or a screen's, `Memoria.ToDo.<Part>.<Module>.Tests.<Screen>`. It prints one `dotnet test` command per test project, with `-p:MemoriaAllTestKinds=true` and one namespace filter per selection. A screen's filter is its exact namespace plus `<namespace>.*`, so it never matches a sibling screen whose name starts with the same letters. A run on November 22, 2026, confirmed that several `--filter-namespace` options in one command select every namespace they name: Editing's and Rendering's filters together listed 637 tests, all from those two namespaces.

What it prints, in this order:

- **Why each module or screen is selected**: changed, the files that changed it; narrowed, the screens every changed file sits in; a dependent, the module it depends on; or a user of test code, the folder whose code it uses. A wrong selection is explained by the output alone, as [Log so problems can be traced](../../AGENTS.md#code-design) asks.
- **Whether the desktop check is needed**: yes whenever a command runs desktop tests, so the agent runs the [desktop check](technology-stack.md#agents-check-the-desktop-before-they-start-screen-tests) first, as [Run timing](../../AGENTS.md#run-timing) asks.
- **The commands**, one per line, each runnable from the repository root, or "No tests to run" when nothing is selected.
- **What only the cloud checks**: a changed workflow file is checked only by a workflow run, so the output says to start [Run All tests](releasing.md#the-run-all-tests-command-tests-the-commit-you-pushed) on a pushed branch, with `node .agents/run-all-tests.mjs` or the **run all tests** task.

Proposed, a decision to confirm: the script only prints. The agent runs each command, so the [hang bound](technology-stack.md#a-hang-dump-stops-a-test-application-that-hangs-despite-its-bounds) and the desktop check stay with the agent, as today. A `--run` option that runs them too was left out as code nobody needs yet.

### Each changed path selects tests by one rule

The script holds these rules in code, their only home; this table explains them. The first matching row decides, and a path can match one row only:

| A changed path under | Selects |
| --- | --- |
| A screen folder of a product project, `src/<Part>/<Module>/<Screen>/` | That screen's test folder, every module that depends on the module, and the test folders that use those folders' test code. Another changed file of the module outside its screen folders widens this to the next row. |
| A product project, `src/<Part>/<Module>/` | That module and every module that depends on it, each module's own test folder, and the test folders that use those folders' test code |
| A screen's test folder, `tests/<Part>/Tests/<Module>/<Screen>/` | That folder, and the test folders that use its test code |
| A module's test folder, `tests/<Part>/Tests/<Module>/` | That folder, its screen folders included, and the test folders that use its test code |
| A test project's own files: its `.csproj`, or a file at the `Tests/` folder's top, such as `ScreenTests.cs` | Every test in that test project |
| Shared test support: `tests/<Part>/Fakes/`, or `tests/Desktop/PlatformTestHost/` | Every test in each test project that references it. You decided this: a change to a desktop fake runs every desktop test, but never misses one. |
| `tests/Testing/Tests/`, or `tests/Testing/UnitRunTiming/` | Every test in `Memoria.ToDo.Testing.Tests` |
| Files that every build reads: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.config/dotnet-tools.json`, `.editorconfig`, `tests/xunit.runner.json`, the solution file, the [kind attributes](technology-stack.md#attributes-mark-each-tests-kind), and [our analyzer](technology-stack.md#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two) | Every test, of every kind |
| `.agents/` scripts, `*.mjs` | `node --test ".agents/*.test.mjs"`, every kind |
| `.agents/subagents/`, `.agents/skills/`, or the files the generator writes: `.claude/agents/`, `.claude/skills/`, `.codex/agents/`, `.github/agents/` | `node .agents/generate.mjs --check` |
| `.github/workflows/` | No local run; the cloud note above |
| Documentation and editor settings: `docs/`, every other `*.md`, `.vscode/`, `.gitignore`, `.ignore`, `.gitattributes` | Nothing |

Two rules add to the row that matched:

- **Any `.csproj`, or `docs/architecture/modules.md`, also selects the [architecture tests](technology-stack.md#architecture-tests-check-the-module-map)**, since they read both.
- **A deleted file whose project no longer exists selects every test**, since the graph can't say who used it. This holds for a deleted test project too.
- **A changed file that a test project compiles through a linked `Compile` item selects every test in that test project**, besides what its own row selects. Today the Shared tests link the desktop composition root's Windows file system, which Sync's integration tests run. Decided by the session, a decision to confirm: following which tests call the linked code would need C# analysis.

What this means for each kind of change:

| Change | What runs |
| --- | --- |
| A leaf desktop module, such as Logs | Logs, Shell, and the composition root's tests |
| Only the tag control's folder in the Task editor | The tag control's tests, then the Task list, Shell, and the composition root, which depend on the Task editor |
| A shared module, such as Tasks | Tasks, Sync, every desktop module that uses either, Shell, and the composition root |
| A platform | Every module of its part, and for the shared platform, every desktop module too |
| The composition root, `src/Desktop/App/` | Only its own tests; nothing depends on it |
| Tests only, in one module's folder | That folder, and the folders that use its test code |
| Documentation only | Nothing |

Every module that depends on a changed module runs in full, even when the change stays inside one screen or behind an unchanged public interface. You decided this: a dependent's tests use the module's real code, so an internal change can break them. So the composition root's tests, about 136 integration tests, run for every desktop or shared change.

### A change inside screens runs only those screens' tests

When every file a session changed in a module sits in that module's screen folders, the module's own tests narrow to those screens' test folders. A [screen](#each-screen-has-at-most-10-ux-tests) is a window, a dialog, or a part a module publishes for windows to host. You decided that screens get folders:

- **A module with two or more screens gives each its own folder**, in its code and its tests: `src/<Part>/<Module>/<Screen>/` and `tests/<Part>/Tests/<Module>/<Screen>/`, such as `src/Desktop/TaskEditor/TagControl/`. A module with one screen, such as the Task list, needs no screen folder: it is its own screen, and its narrowing would select the whole module anyway.
- **A screen folder holds the code only that screen uses.** Code two screens of the module share, and code no screen uses, stays at the module's top, so changing it selects the whole module.
- **A screen's test files use the namespace `Memoria.ToDo.<Part>.<Module>.Tests.<Screen>`**, which names the screen once, in its folder. Code files keep their module's namespace, as the files in `src/Desktop/App/DeviceKeys/` do today, so moving them changes no code.
- **Narrowing runs every kind of test in the screen's test folder**: its UX tests, the unit tests of its view model and parts, and its integration tests. A UX-only narrowing would skip the unit tests that check the changed code most closely.
- **The script knows a screen folder from its tests.** A folder under `src/<Part>/<Module>/` is a screen folder when the module's test folder has a folder of the same name whose files use that screen's namespace. Any other folder, such as `src/Desktop/App/DeviceKeys/`, counts as the module's top, so a missing test folder selects more, never less.
- **Dependents still run in full**, as the [rules above](#each-changed-path-selects-tests-by-one-rule) say. A change to a published part, such as the tag control, saves only the rest of its own module's tests.

About 60 to 90 code files and 90 to 100 test files would move. The code files sit in the five desktop areas with several screens: about 30 of the Task editor's 75, 10 of the platform's 45, 22 of the Contact list's 24, 3 of Updates' 18, and 5 of the Formatted view's 9. The test files are the 54 UX classes in those areas, plus the unit tests of their screens' code. Each moved test file changes one namespace line. The moves go with each screen's [consolidation](#existing-ux-tests-shrink-one-screen-at-a-time), which rewrites its UX tests anyway.

### The script checks the namespace rule it relies on

A namespace filter finds a test only when the test's namespace names its folder. So the script fails before printing anything when a test file breaks the rule, naming the file and the namespace it expects. Proposed, a decision to confirm:

- **A test file in `tests/<Part>/Tests/<Module>/` uses a namespace that starts with `Memoria.ToDo.<Part>.<Module>.Tests`.** Every module folder follows this today.
- **A test file in a screen folder uses `Memoria.ToDo.<Part>.<Module>.Tests.<Screen>`, where `<Screen>` is the folder's name**, at any depth inside it. Every file in a folder directly under a module's test folder uses one namespace: the screen's, or the module's for a helper folder such as `tests/Desktop/Tests/TaskList/Screen/`.
- **A test file at a test project's top uses `Memoria.ToDo.<Part>` or `Memoria.ToDo.<Part>.Tests`**, and runs whenever its whole test project runs. Today that's the screen collection, two helpers, and `SimulatedKeyTests`, which checks a fake against the real keyboard state. It is an integration test, not a UX test, since a UX class there would sit above every screen's namespace.
- **The script, not an architecture test, owns the rule**, so it has one home, and a wrong namespace fails at the session's end instead of only in the cloud.

### The script fails fast and names the problem

The script checks everything before it prints a plan. On any problem it prints one line that names it, prints no plan, and exits with code 1. Exit code 0 means it printed a plan, even an empty one. Proposed, a decision to confirm.

- **Bad arguments**: an unknown, repeated, or valueless option.
- **Git can't answer**: no git, not a repository, or a `--base` that names no commit.
- **A project file can't be read**, or a reference names a project that doesn't exist.
- **A changed path matches no rule.** The script never guesses: a new kind of file gets a rule first. Selecting every test instead was ruled out, since it would hide the gap.
- **A test file breaks the [namespace rule](#the-script-checks-the-namespace-rule-it-relies-on).**

### Its tests are mostly unit tests

Proposed, a decision to confirm:

- **Unit tests give the deciding code made-up inputs**: changed paths, a project graph, test folders and what each uses. They check each rule in the table, narrowing to one screen and to several, widening when a file outside the screen folders changes, the dependents through several steps, the printed reasons, the desktop check line, an empty plan, and each failure, with no git and no files.
- **A few integration tests run the script on a temporary git repository** holding a handful of tiny project files and test folders. They check what git reports, staged, unstaged, untracked, renamed, and with `--base`; reading real project files; and the exit codes. They never read this repository's files.

### Each screen has at most 10 UX tests

A UX test follows one path a user commonly takes through a screen, such as "add a task with a tag and a mention, then save it", and checks what the user sees at each step. Edge cases, errors, and each key's exact effect belong in unit tests of the screen's view model or parts, which need no desktop. This rule lives in [AGENTS.md](../../AGENTS.md#test-design); this section says what a screen is and how the limit is checked.

You decided that a screen is a window or dialog the app opens, or a part a module publishes for windows to host, such as the tag control, the formatted title, the search band, or the contact card. That makes about 20 screens, at most about 200 UX tests. A part keeps its UX tests in its own module, so a change to the part runs only its own screen tests and its hosts'. A host window's paths still use the part, but don't repeat the part's own paths. Before consolidation, the seven windows and dialogs held:

| Screen | Module | UX tests before consolidation |
| --- | --- | --- |
| Task View | Task list | 342 |
| Configure | Configure | 180 |
| Add window | Task editor | 137 |
| Contacts window | Contact list | 41 |
| What is new | Updates | 14 |
| Logs | Logs | 12 |
| Question dialog | Platform | 10 |

The counts are test methods marked `[Fact]` or `[Theory]` in `[UX]` classes, from November 22, 2026; a theory ran once per data row, so more ran. The published parts held the rest: the Task editor's editors and tag control, 162; the Formatted view's title and description, 66; the Platform's search band, hover card, and other parts, 71; the contact card, 9; and the Updates status bar, 13. Each screen's limit of 10 and today's counts are [below](#existing-ux-tests-shrink-one-screen-at-a-time).

### Our analyzer counts each screen's UX tests

[Our analyzer](technology-stack.md#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two) fails the build when one screen has more than 10 UX tests, counting every `[UX]` class of that screen in the test project. You decided it counts per screen, across classes, so a screen's tests can't escape the limit by spreading over several classes. You decided that each UX class also names its screen in its attribute. The rest is proposed, each a decision to confirm:

- **`[UX("<Screen>")]` names the class's screen**, such as `[UX("TagControl")]`, and gives each of its tests the trait `Screen=<Screen>` beside `Category=UX`. The name is required, since `[UX]` has no constructor without it.
- **The class's namespace decides its screen; the attribute must match it.** A screen folder's namespace ends with its screen's name, and a module with one screen uses its module's name, such as `TaskList`. The analyzer counts by namespace, so a wrong name can't move tests to another screen's count.
- **`MTD0008`: a `[UX]` class's screen name differs from its namespace's screen.** It's shown on the attribute and names both, so renaming a screen's folder without its attributes fails the build. This keeps the two homes you chose in step. A class that inherits its `[UX]` is checked against its own namespace, shown on its name; a null name shows as `(none)`.
- **It counts the test methods each non-abstract `[UX]` class declares or inherits**, the same test classes and test methods the [kind check](technology-stack.md#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two) defines. A class whose kind comes from its base class counts too.
- **`MTD0005`: a screen has more than 10 UX tests.** It's shown on the name of each `[UX]` class of that screen, and names the screen, its count, and the limit. Exactly 10 passes.
- **`MTD0006`: a `[UX]` class holds a theory.** It's shown on the method. A path is one test, and a theory's rows would multiply the count unseen, since rows from a method or a class exist only when the tests run. A theory inherited from a base that isn't a `[UX]` class is shown on the inheriting class's name.
- **`MTD0007`: a `[UX]` class sits in a namespace that has UX classes in a child namespace**, such as a module's top beside its screen folders. It's shown on the class's name and asks you to move the class into its screen's folder, so no screen's tests count under its module's name.
- **`MTD0005` and `MTD0007` show when you build, not as you type.** They need every class in the test project, so they run at the end of the compilation, which Visual Studio doesn't run live ([Roslyn issue 76693](https://github.com/dotnet/roslyn/issues/76693)). `MTD0006` shows as you type.
- **Its tests are unit tests beside the kind check's**, in `tests/Testing/Tests/Analyzers/`: 10 and 11 tests across two classes and two files, inherited tests, an abstract base, a theory, two screens counted apart, a UX class beside a screen namespace, and a screen name that matches and one that doesn't.

`MTD0003` and `MTD0004` already belong to the [real-input check](technology-stack.md#unit-and-ux-tests-cannot-send-real-input), so the new errors start at `MTD0005`. The limit of 10 lives in the analyzer, its only home in code.

### Existing UX tests shrink one screen at a time

Each screen's consolidation is one behavior-preserving change, in the lightest lane that fits, under [the process](../contributing/process.md). The tester does it in two calls, since only you can agree to a drop:

1. **Plan.** The tester lists the screen's common paths, at most 10, most used first. For each existing UX test it says where its behavior goes: a step of a named path; an existing unit test, by name; a new unit test, by name; or a drop, with the reason. The main session asks you to confirm the drops and the paths.
2. **Change.** The tester writes the path tests and the new unit tests, then deletes the old UX tests. In a module with several screens, the same change moves the screen's code and tests into its [screen folders](#a-change-inside-screens-runs-only-those-screens-tests). A new unit test that passes at once, since the behavior exists, gets its proof in the [closing failure proof](../../AGENTS.md#run-timing), as every path test does.

Screens went in order of what they save: Task View, Configure, the Add window with the Task editor's parts, then the rest. The screen argument of `[UX]` and the analyzer's four new errors took effect with the last consolidation. After it, the desktop tests hold 18 screens and 100 UX tests:

| Module | Screens and UX tests |
| --- | --- |
| Task list | TaskList 10 |
| Configure | Configure 10 |
| Task editor | AddWindow 10, TagControl 10, FieldEditor 5, Pickers 3 |
| Contact list | ContactsWindow 10, ContactCard 3 |
| Platform | QuestionDialog 3, SearchBand 3, HoverCard 3, AutoScroll 3, WindowHosting 5 |
| Formatted view | FormattedTitle 5, FormattedDescription 6 |
| Updates | WhatIsNew 4, StatusBar 4 |
| Logs | Logs 3 |

`SimulatedKeyTests`, the one UX class at the test project's top, became an integration test, as [the namespace rule](#the-script-checks-the-namespace-rule-it-relies-on) says.

## Public interface

None. No module uses the script or the analyzer rule.

## Failure modes

- **A test's namespace doesn't name its folder**: the script fails and names the file, as [above](#the-script-checks-the-namespace-rule-it-relies-on).
- **A changed path matches no rule**: the script fails and names it. Add a rule, then run it again.
- **A selected module has no test folder**: nothing runs for it, and the output says so.
- **A code folder looks like a screen folder but has no matching test folder**: the script treats its files as the module's top and selects the whole module, so it selects more, never less.
- **A screen has an 11th UX test, a UX theory, a UX class beside its screen folders, or a screen name that differs from its folder**: the build fails with `MTD0005`, `MTD0006`, `MTD0007`, or `MTD0008`, naming the class, method, or attribute.
- **A test outside the selection breaks**: final validation can't see it; the next Run All tests run or release run fails on it, and no release can be published from that commit.
- **A run with desktop tests starts on a locked or disconnected PC**: the desktop check stops the session first, as [Run timing](../../AGENTS.md#run-timing) says.

## Ruled out

- How the session picks the tests to run:
  - **Running every test at each session's end**: the run this page replaces. It is slow, and most of its failures came from the desktop.
  - **Choosing tests by which code each test ran, from coverage data**: a coverage run and a store of its results to keep, and a stale store would miss tests.
  - **A hand-kept list of which modules depend on which**: a second home for what the project files already say.
  - **Selecting every test when a path matches no rule**: it hides the missing rule, and the session runs everything without saying why.
  - **Shared test support selects only the test folders that mention the changed file's types**: it misses a test that reaches a fake through a helper in another folder.
  - **Moving every helper one test folder shares with another to the test project's top or its Fakes project**: cleaner selection, but each moved helper would select a whole test project when it changes.
  - **Dependents run only when a module's public API files change**: a behavior change behind an unchanged interface would reach dependents' tests only in the cloud.
  - **Narrowing a changed screen to its UX tests only**: it skips the unit tests of the code that changed.
- How UX tests are kept apart:
  - **Merging UX into integration tests**: you kept the kind; renaming saves no time.
  - **Only windows and dialogs as screens**, at most 70 UX tests: parts would lose their own screen tests, and moving their checks takes the most work.
  - **One budget per module**: Task View and the editors it shares with the Add window would share one limit.
  - **The analyzer limits each `[UX]` class to 10 tests, and review checks one class per screen**: a screen's tests could spread over two classes unseen.
  - **Review only**: nothing stops the count from creeping back.
  - **Screen names as constants in one class the attributes' project keeps**: a hand-kept list beside the folders that already name each screen.
  - **Counting a theory's rows**: rows from a method or a class exist only when the tests run.
  - **The screen read only from the namespace, with `[UX]` unchanged**: one home for the name, but you wanted each UX class to say its screen where you read it.
  - **The screen as a free string in `[UX]`, with no screen folders**: the script couldn't tell which screen a changed code file belongs to.
  - **Finding a changed file's screen from the test files that name its types**: it misses a screen that reaches the type through another type, and the script would have to parse C#.
- How a release is tested:
  - **The release command runs the full suite on your PC**: the release again depends on your PC's desktop.
  - **No tests before tagging, with the tag's run checking them**: a failure leaves a tag to [clear](version-tags.md).
  - The other ways a release could avoid running the tests again are in [Release a version](releasing.md#ruled-out).

## Open questions

None.
