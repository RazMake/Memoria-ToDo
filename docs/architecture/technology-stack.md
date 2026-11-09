# How the code is built, tested, and released

Status: Decided

The technology stack is the set of tools that lay out, build, test, and release both apps from this repository. Each choice has its own section under [Behavior](#behavior).

## Behavior

[How the apps fit together](overview.md#every-part-targets-net-11) already decides the language, C#; .NET 11 for every part; and the main libraries. This page doesn't reopen them. It decides the rest: how the throwaway proofs run, how the code is laid out and named, where versions are pinned, the test tools, how the Android modules' tests build and where they run, the one command that builds and tests, how Visual Studio Code runs the desktop app, the architecture tests, whether the desktop app carries its own .NET runtime, the release step, the Android build of libgit2, and where the Android signing key is kept.

### Earlier designs set what the stack must serve

The table lists each need the stack must meet, and the page that sets it:

| Need | Set by |
| --- | --- |
| How tests are classified, and when each kind runs | [AGENTS.md tests](../../AGENTS.md#tests) |
| One project per module, its tests, and its public API files | [Modules](modules.md#a-module-is-one-capability-behind-a-small-interface) |
| Which modules a module may use | [Module map](modules.md#module-map) |
| Why Sync's git tests are integration tests | [Modules](modules.md#the-platform-serves-every-module-and-knows-none) |
| The desktop installer and its updates | [Desktop Shell](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date) |
| The Android app's updates and their signing | [Android Shell](android/shell.md#updates-come-from-this-repositorys-github-releases) |
| The application binary interfaces (ABIs), or processor types, each Android package holds | [Android app](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material) |
| The proof on a phone, which comes before other Android work | [Android app](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) |

### This page resolves one risk and shares two

Of the [risks the later rows must resolve](overview.md#risks-the-later-rows-must-resolve), this page owns "We maintain libgit2's Android build", which [a script in this repository](#a-script-in-this-repository-builds-libgit2-for-android-and-its-binaries-are-committed) resolves. It shares two:

- **Each ABI makes the Android package bigger.** The Android app picks the ABIs; this page builds one libgit2 for each.
- **The transport must read both Secure Shell (SSH) address forms.** Sync owns it; [Sync's test server](#syncs-integration-tests-run-an-ssh-server-inside-the-test-process) accepts both forms, so Sync's tests can cover them.

The other risks belong to the proofs, the Android app, and Sync, as the risk table says. This page only sets when the proofs run and where their code lives, [below](#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided).

### The throwaway proofs run on a never-merged branch, before this page is Decided

Two throwaway proofs ran before this page was Decided, though the [design backlog](README.md#design-backlog) held back product code until then. The [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) tested LibGit2Sharp with a libgit2 we build with the Android Native Development Kit (NDK). The [Windows proof](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) tested our SSH transport on Windows. Both tested the riskiest parts of the design before the choices that depend on them, and both passed.

- **They ran after the choices they don't affect, and before the two they do.** [Where the Android modules' tests run](#each-android-module-builds-for-both-the-pc-and-android), and [how libgit2 is built for Android](#a-script-in-this-repository-builds-libgit2-for-android-and-its-binaries-are-committed), waited for the proof on a phone. Had it failed, every [fallback](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) would have dropped libgit2, so a libgit2 build decided first would have been wasted.
- **Proof code lives on a throwaway branch of this repository and is never merged**, so it never becomes product code. You create the branch and make its commits, since [only you commit](../../AGENTS.md#contributions).
- **Proof code skips AGENTS.md's [Code design](../../AGENTS.md#code-design) and [Tests](../../AGENTS.md#tests) rules, and [Contributions](../../AGENTS.md#contributions) principle 4.** Principle 4 delivers each change through the [process](../contributing/process.md): design, tests first, code, independent review, and a delivery report. A proof only shows whether something works, so none of that is worth its cost for code that's thrown away. Every other rule in [AGENTS.md](../../AGENTS.md) still applies, so AI never commits or pushes the branch.
- **What a proof shows is recorded in the design files it checks**, such as the [Android app](android/README.md) and [How the apps fit together](overview.md). That record is a design change, not a delivery report.

### Code and tests sit in mirrored trees by part, in one solution

Each module's project and its tests sit at matching paths in two trees at the repository root, `src/` and `tests/`, grouped by part. The parts are `Shared`, `Desktop`, and `Android`: the shared library, the desktop app, and the Android app, as [How the apps fit together](overview.md#two-apps-share-one-c-library) defines them. The [module map](modules.md#module-map) lists each part's modules.

- **Tests mirror code.** A module's project sits in `src/<Part>/<Module>/` and its tests in `tests/<Part>/<Module>/`, such as `src/Android/Shell/` and `tests/Android/Shell/`. Three test folders mirror no `src/` folder: `tests/<Part>/Fakes/`, for each part's [shared fakes](#each-port-has-one-hand-written-fake-shared-by-every-test); `tests/Testing/`, for the kind attributes and [our analyzer that checks them](#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two); and `tests/Architecture/`, for the [architecture tests](#architecture-tests-check-the-module-map). Proposed: a fourth, `tests/Desktop/PlatformTestHost/`, holds the small program that the [desktop platform's integration tests](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) start, and `tests/Testing/` also holds the [unit-run timing program and its tests](#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue). This is a decision to confirm.
- **A project's name says its part.** Projects are named `Memoria.ToDo.<Part>.<Module>`, such as `Memoria.ToDo.Android.Shell`, because the desktop and Android apps have modules with the same names, such as Shell. A module name with spaces drops them, so the desktop Task editor's project is `Memoria.ToDo.Desktop.TaskEditor`.
- **Each part's [platform](modules.md#the-platform-serves-every-module-and-knows-none) is one project, `Memoria.ToDo.<Part>.Platform`, in `src/<Part>/Platform/`.**
- **Each app's composition root is one project, `Memoria.ToDo.<Part>.App`, in `src/<Part>/App/`.** It builds the app you install.
- **A platform's or composition root's tests sit at the matching path under `tests/`**, as a module's do.
- **One solution, `Memoria.ToDo.slnx` at the repository root, holds every project.** The `.slnx` format is the default since .NET 10 ([`dotnet new sln`](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default)).

### Three files pin every version

Every .NET tool and package version lives in one of three files, so every machine and every release build use the same versions. The table lists each file and what it pins:

| File | Pins |
| --- | --- |
| `global.json` | The .NET software development kit (SDK) and the workload set; it also selects the test runner |
| `Directory.Packages.props` | Every NuGet package, through [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management), so project files name packages without versions |
| `.config/dotnet-tools.json` | .NET tools, such as Velopack's `vpk` and [XHarness](#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release) |

Pinning the workload set means Android workload updates arrive only when you change the pin ([workload sets](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-sets)). These files are [review surface](../../AGENTS.md#contributions), so you see every version change.

Proposed: the Velopack package's version is read from `vpk`'s entry in `.config/dotnet-tools.json`, so it has one home. Velopack supports only a `vpk` and a Velopack package of the same version ([Velopack issue 644](https://github.com/velopack/velopack/issues/644)). `Directory.Packages.props` reads it as `Directory.Build.props` reads the [kinds' constants](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test), and a version it can't read fails the restore. This is a decision to confirm.

Not confirmed: that `vpk` runs on the pinned SDK. `vpk` 1.2.0 is built for .NET 8, 9, and 10 ([vpk on NuGet](https://www.nuget.org/packages/vpk/1.2.0)), so with only .NET 11 installed it must roll forward to a newer runtime, which a tool's entry in `.config/dotnet-tools.json` can allow.

`global.json` pins one SDK, and each PC installs it for the current user:

- **The SDK version lives in [global.json](../../global.json)**. The first desktop milestone uses .NET 11 release candidate 3, as you approved. `global.json` rolls forward only to a newer patch in the same feature band, `latestPatch`, so a PC with a newer patch installed uses it ([rollForward](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#rollforward)). Moving to a new feature band, or raising the lowest SDK every PC needs, is a change to this pin.
- **Each PC installs it beside its system SDK, which stays as it is.** Microsoft's `dotnet-install.ps1 -JSonFile global.json -InstallDir "$env:LOCALAPPDATA\Memoria-ToDo\dotnet"` installs the pinned version in a folder dedicated to this repository, with no administrator rights ([dotnet-install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)).
- **Each build session puts that folder first on `PATH` and in `DOTNET_ROOT`.** The script changes `PATH` only for its own session and never sets `DOTNET_ROOT`, which tells built programs where to find the .NET 11 runtime ([DOTNET_ROOT](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_root-dotnet_rootx86-dotnet_root_x86-dotnet_root_x64)).
- **A session that skips that step gets an error that points to the README.** `global.json`'s `errorMessage` replaces .NET's "SDK not found" text with a pointer to the [README](../../README.md#what-you-need)'s setup steps ([errorMessage](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#errormessage)). Proposed, a decision to confirm. `global.json`'s `paths` can't find the SDK instead: it takes only absolute paths or paths relative to `global.json`, not the per-user folder ([paths](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#paths)).

Not confirmed: the workload set that matches this SDK. The desktop-only milestone needs no Android workload; its pin waits for Android implementation.

When the first code lands, the [README](../../README.md#what-you-need)'s list of what you need grows: the .NET 11 SDK, the Android workload, git for [Sync's integration tests](#syncs-integration-tests-run-an-ssh-server-inside-the-test-process), the step that installs a Java Development Kit (JDK) and the Android SDK, as [the workload set decides](#the-pinned-workload-set-decides-the-jdk-and-android-sdk), and an Android emulator for [final validation's Android tests](#each-android-module-builds-for-both-the-pc-and-android). Only the [libgit2 script](#a-script-in-this-repository-builds-libgit2-for-android-and-its-binaries-are-committed) needs the NDK and CMake. Proposed: each item joins the list with the first step that needs it, so the desktop's first steps add only the .NET 11 SDK, with the command that installs it, and [Visual Studio Code with the C# extension](#visual-studio-code-runs-the-desktop-app-with-f5-on-the-pinned-sdk). Git joins with Sync, and the Android items with the Android app's first step. This is a decision to confirm.

### Visual Studio Code runs the desktop app with F5, on the pinned SDK

In Visual Studio Code, F5 builds the desktop app and starts it with the debugger, and Ctrl+F5 does the same without it ([default keys](https://code.visualstudio.com/docs/reference/default-keybindings)), as you asked. Both use the [pinned SDK](#three-files-pin-every-version), with no change to the system's `PATH`. Proposed, each a decision to confirm:

- **One launch configuration, in `.vscode/launch.json`, serves both keys.** It starts the composition root's program with the debugger from Microsoft's C# extension, `ms-dotnettools.csharp` ([C# debugging](https://code.visualstudio.com/docs/csharp/debugging)). It sets `DOTNET_ROOT` to the pinned folder, as every build session does.
- **Its build step, in `.vscode/tasks.json`, runs the `dotnet` in the pinned folder**, by its full path.
- **Its terminals start with the pinned folder first on `PATH` and in `DOTNET_ROOT`.** `.vscode/settings.json` sets both in `terminal.integrated.env.windows`, so `dotnet build` and `dotnet test` need no setup step there. Visual Studio Code applies it only in a trusted workspace ([Restricted Mode](https://code.visualstudio.com/docs/editing/workspaces/workspace-trust#_workspace-settings)), and only to terminals opened after the change ([terminal environment](https://code.visualstudio.com/docs/terminal/profiles#_configuring-profiles)). Not confirmed by a run.
- **`.vscode/extensions.json` recommends only the C# extension.** Building, running, and debugging don't need C# Dev Kit.
- **Code completion needs Visual Studio Code started with the pinned folder first on `PATH`.** The extension finds the SDK through `PATH`, and its setting for an SDK path can't name a folder through an environment variable ([vscode-csharp issue 6471](https://github.com/dotnet/vscode-csharp/issues/6471)). The [README](../../README.md#what-you-need) gives the command. Not confirmed by a run.

### Tests use xUnit.net v3

Every test uses [xUnit.net v3](https://xunit.net/), pinned in `Directory.Packages.props` like every package. It fits the [test rules](../../AGENTS.md#tests) in three ways:

- **It runs on Microsoft.Testing.Platform (MTP) by default.** Its MTP support is built in, so `dotnet test` runs it with the runner `global.json` selects ([xUnit.net on MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)).
- **Custom trait attributes can mark a test's kind.** An attribute that implements `ITraitAttribute` marks a test method, a test class, or a whole assembly ([extensibility](https://github.com/xunit/xunit.net/blob/main/site/docs/getting-started/v3/extensibility.md), [custom traits in v3](https://github.com/xunit/xunit/discussions/3059)). [Attributes mark each test's kind](#attributes-mark-each-tests-kind-in-one-test-project-per-module) uses this.
- **Each test gets a new instance of its test class** ([shared context](https://xunit.net/docs/shared-context)), so a test's fields can't carry state into the next test, which serves "Isolate what tests change" in [AGENTS.md](../../AGENTS.md#test-design).

Not confirmed: running xUnit.net v3 on .NET 11's Android runtime, which the [Android tests on an emulator](#each-android-module-builds-for-both-the-pc-and-android) need.

### Each port has one hand-written fake, shared by every test

Tests check results with xUnit.net's own assertions, its `Assert` class, and replace each port with a hand-written fake. The ports are the secure key store, the file system, image shrinking, and .NET's TimeProvider as the clock ([patterns](overview.md#each-pattern-serves-one-requirement)), the desktop Task list's [web view](desktop/task-list.md#the-task-list-gets-its-web-view-through-an-interface-it-publishes), the desktop platform's [theme and theme setting](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), and the desktop [Shell's](desktop/shell.md#public-interface) and [Configure's](desktop/configure.md#public-interface) ports. A fake behaves like the real thing, so tests check results, not calls, as [AGENTS.md](../../AGENTS.md#test-design) asks.

- **Each fake lives once, and every test that needs it shares it**, because shared test setup follows the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication). The same holds for the fake services a UX test uses.
- **Every fake sits in one project per part, `Memoria.ToDo.<Part>.Fakes`, in `tests/<Part>/Fakes/`.** It holds no tests, and test projects reference it for the fakes they need, so no test project references another.

### Attributes mark each test's kind, in one test project per module

Each module keeps all its tests in one test project, and an attribute on each test class marks its tests' kind. [AGENTS.md](../../AGENTS.md#tests) defines the three kinds, unit, UX, and integration, limits the unit suite's time, which [the workflow checks](#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue), and says when each kind runs.

- **A module's test project is named `Memoria.ToDo.<Part>.<Module>.Tests`**, such as `Memoria.ToDo.Android.Shell.Tests`, and sits in `tests/<Part>/<Module>/`.
- **Every test class carries one of our own attributes, `[Unit]`, `[UX]`, or `[Integration]`, so each test class holds tests of one kind.** Each is a custom xUnit.net trait attribute that gives every test in the class the trait `Category=Unit`, `Category=UX`, or `Category=Integration`. One kind per class fits "Group tests by component and scenario" in [AGENTS.md](../../AGENTS.md#test-design).
- **The compiler catches a misspelled attribute, and one on a method.** The attributes are C# types that may mark only a class (`AttributeTargets.Class`).
- **A test class with no kind attribute, or two, can't build.** [Our analyzer](#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two) fails the build and names the class. Without it, a class with no kind would sit out every development run, since those select only `Category=Unit`, as [`dotnet test`'s filter](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) says.
- **The attributes live in `Memoria.ToDo.Testing.Kinds`, in `tests/Testing/Kinds/`, which every test project references.** The analyzer is `Memoria.ToDo.Testing.Analyzers`, in `tests/Testing/Analyzers/`, and its tests are `Memoria.ToDo.Testing.Analyzers.Tests`, in `tests/Testing/Analyzers.Tests/`. The three folders sit side by side, because a .NET project compiles every C# file under its folder ([default includes](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview#default-includes-and-excludes)).
- **The kinds' strings live in one C# file of constants in that project, so none can be mistyped.** It holds the trait name, `Category`; the three kind names; and the attributes' full type names. It uses nothing else, so the attributes' project compiles it, and the analyzer's project compiles the same file through a linked `Compile` item. So the file must also compile in the analyzer's .NET Standard 2.0 project. No other code spells these strings: `Directory.Build.props` [reads the two its filter needs](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) from this file.

### Our analyzer fails the build when a test class has no kind attribute, or two

An analyzer is a plug-in that the C# compiler, Roslyn, runs on the code it compiles. Ours checks each test class's [kind attribute](#attributes-mark-each-tests-kind-in-one-test-project-per-module), so a missing or doubled one fails the build, rather than waiting for a run of every test.

- **Every test project loads it through `Directory.Build.props`**, so no test project is left out.
- **It reports a compile error, naming the class, when a test class has no kind attribute or more than one.** A test class is a non-abstract class that declares or inherits at least one xUnit.net test method, which matches what xUnit.net runs: it skips abstract classes ([class filter](https://github.com/xunit/xunit/blob/main/src/xunit.v3.core/Framework/TestFrameworkDiscoverer.cs)) and finds inherited test methods ([method lookup](https://github.com/xunit/xunit/blob/main/src/xunit.v3.core/Configuration/RegisteredEngineConfig_reflection.cs)).
- **A test method is one marked `[Fact]`, `[Theory]`, or an attribute derived from them**, such as Xunit.StaFact's `[WpfFact]`. Both `[Theory]` ([source](https://github.com/xunit/xunit/blob/main/src/xunit.v3.core/TheoryAttribute_reflection.cs)) and `[WpfFact]` ([source](https://github.com/AArnott/Xunit.StaFact/blob/main/src/Xunit.StaFact/WindowsDesktop/WpfFactAttribute.cs)) derive from `[Fact]`.
- **`dotnet build` fails, and the editor shows the error as you type.** An analyzer error fails the build and is underlined in red in the editor ([severity levels](https://learn.microsoft.com/en-us/visualstudio/code-quality/use-roslyn-analyzers#severity-levels)), and Visual Studio checks code as you type ([code analysis](https://learn.microsoft.com/en-us/visualstudio/ide/find-and-fix-code-errors#use-code-analysis)). The check runs inside the compiler, so test projects that build for Android get it too.
- **The analyzer targets .NET Standard 2.0 and doesn't reference xUnit.net.** Microsoft asks analyzers to target .NET Standard 2.0, because one analyzer runs both in command-line builds on .NET and in Visual Studio on .NET Framework ([write an analyzer](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix#explore-the-analyzer-template)). It finds xUnit.net's attributes and ours by their full type names.
- **It adds one package, Microsoft.CodeAnalysis.CSharp, pinned in `Directory.Packages.props`.** The pin must not be newer than the compiler in the pinned SDK, or the one in your editor: an older compiler skips the analyzer, with warning CS9057, and the build passes without the check ([analyzer compatibility](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/source-generator-errors#analyzer-and-source-generator-compatibility)). Each package version also names the oldest Visual Studio it runs in ([Roslyn version support](https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support)).
- **A pin that's too new fails the build.** `Directory.Build.props` makes warning CS9057 an error in test projects, so the check is never skipped silently.
- **Its own tests are unit tests, in `[Unit]` classes, and use only Microsoft.CodeAnalysis.CSharp.** Each test compiles a code snippet in memory, against the assemblies already loaded in the test process, so it reads no files, and checks the errors the analyzer reports. Not confirmed by a run: compiling that way.

Proposed, each a decision to confirm:

- **Its two errors are `MTD0001`, for no kind attribute, and `MTD0002`, for more than one**, each shown on the class's name.
- **A kind on a base class counts for the classes derived from it**, as xUnit.net gives them its trait ([attribute lookup](https://github.com/xunit/xunit/blob/main/src/xunit.v3.common/Extensions/ReflectionExtensions_reflection.cs)). A different kind added on top makes two; the same kind repeated counts once. A nested class doesn't take its outer class's kind.
- **The analyzer's project turns off warning RS2008**, which asks for release-tracking files meant for analyzers published as packages.

### Plain `dotnet test` runs only unit tests, and final validation runs every test

`Directory.Build.props` at the repository root sets one default filter for every test project: xUnit.net's `--filter-trait "Category=Unit"` ([xUnit.net on MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)). It's passed through `TestingPlatformCommandLineArguments`, the property `dotnet test` forwards to each test application in Microsoft.Testing.Platform mode, and a property given with `-p:` can change it ([`dotnet test`](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)).

- **Plain `dotnet test` at the repository root builds everything and runs only unit tests.** `dotnet test --project <path>` runs one project's unit tests, as a focused batch.
- **Final validation runs `dotnet test` with one `-p:` property that drops the filter**, so every test runs. This is the one command that builds the code and runs every test, which the [design backlog](README.md#design-backlog) asks for.
- **A run in which no test ran fails.** With the .NET 11 SDK, a test project that matches no tests exits with code 8, but `dotnet test` counts that as success when it adds up the results, so one empty test project doesn't fail the run. A run where no project ran any test exits with code 8, a failure ([whole-run minimums](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp#whole-run-and-per-module-minimums)). So `dotnet test --project` on a project with no unit tests, such as the one that holds the [architecture tests](#architecture-tests-check-the-module-map), fails during development; its tests run with every other test. For the same reason, every push fails the [workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) until the first unit test exists, which you accepted.
- **The filter's trait name and unit kind name come from the [kinds' constants file](#attributes-mark-each-tests-kind-in-one-test-project-per-module)**, so `Directory.Build.props` keeps no copy of them. It reads the file with `System.IO.File::ReadAllText` and picks out the two names with a regular expression, both allowed in MSBuild property functions ([static property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions#static-property-functions)). If the file's layout changes so the match fails, the filter matches nothing, and plain `dotnet test` fails with exit code 8.

Not confirmed by a run: this default with xUnit.net v3 and the MTP runner `global.json` selects, and reading the two names from the constants file.

### Each Android module builds for both the PC and Android

Each Android module's one project builds twice, for the PC and for Android, so its view models and rules get unit tests on the PC. Code that calls Android builds only for Android. A test project for the PC can't reference a project built only for Android ([target frameworks](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)), so without the PC build, no Android module would have unit tests during development.

- **Each file in an Android module is either shared or Android-only.** A shared file builds for both. An Android-only file calls Android, so it builds only for Android.
- **One project per module stays**, as [Modules](modules.md#a-module-is-one-capability-behind-a-small-interface) decides, at the cost of two builds per project.
- **The module's one test project builds for both too.** Its PC build holds the unit tests, which run during development and on each push. Its Android build is a test app holding the UX tests of the module's screens and the integration tests of its few Android-only parts.
- **The test app runs on an emulator only at final validation and before each release**: on your PC at final validation, as [Run timing](../../AGENTS.md#run-timing) says, and on each version tag in the [Linux job](#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release).

Not confirmed: which test runner runs xUnit.net v3 tests in an Android test app on .NET 11's runtime. xUnit.net v3 ships no runner for devices, so a test app must supply its own ([xUnit discussion](https://github.com/xunit/xunit/discussions/3112)), and the proof on a phone ran no tests. The runner is a new dependency, so the implementation brings one that works to you before adding it, as [Contributions](../../AGENTS.md#contributions) asks. If none works, that's a [design gap](../contributing/process.md#changing-the-code) for you to decide.

Not confirmed by a run: a test project that builds for both, where plain `dotnet test` runs only its PC build, and final validation's [one command](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) also runs its Android build on an emulator. [XHarness](https://github.com/dotnet/xharness#system-requirements), which runs the test apps in the [Linux job](#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release), runs them from Windows too.

### WPF screens get UX tests on the PC, and Android screens on an emulator

A UX test builds one screen inside the test process, with fake services, and never starts the app ([AGENTS.md](../../AGENTS.md#test-design)). The table lists each part of the user interface, where its UX tests run, and what runs them:

| Part of the user interface | Where its UX tests run | What runs them |
| --- | --- | --- |
| A Windows Presentation Foundation (WPF) screen of the desktop app | On the PC, in its module's test project | [Xunit.StaFact](https://github.com/AArnott/Xunit.StaFact)'s `[WpfFact]`, which runs a test on a single-threaded apartment (STA) thread with a dispatcher, as WPF needs. Its 4.x versions support xUnit.net v3 4.x. Not confirmed: .NET 11. |
| The WebView2 web view in the desktop's View Tasks | Nowhere: WebView2 runs its own browser processes outside the test process ([process model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model)) | A UX test of View Tasks passes a fake web view through the [Task list's interface](desktop/task-list.md#the-task-list-gets-its-web-view-through-an-interface-it-publishes); only integration tests check the real one |
| A .NET for Android screen | On an emulator, in its module's test app, at final validation and before each release, since Android views need Android's runtime | The runner the implementation finds, as [the Android modules' section](#each-android-module-builds-for-both-the-pc-and-android) says. [DeviceRunners](https://mattleibow.github.io/DeviceRunners/articles/using-dotnet-test.html) runs xUnit.net v3 tests on Android through `dotnet test`, but its test app is a .NET MAUI app. Not confirmed: hosting our .NET for Android views in it, and .NET 11. |

- **If no runner can host our Android views, Android screens get no UX tests.** Their view models still get unit tests on the PC, since [each Android module builds for both](#each-android-module-builds-for-both-the-pc-and-android), and emulator tests check the screens as integration tests. The hand-written code that updates Android views from view models ([Model-View-ViewModel](overview.md#each-pattern-serves-one-requirement)) then gets few checks.
- **In Android UX tests, the Task list's web view is replaced** as the [Android Task list](android/task-list.md#one-web-view-starts-the-first-time-a-task-expands) says.

### Architecture tests check the module map

Tests that check every project, rather than one module, sit in a test project of their own, `Memoria.ToDo.Architecture.Tests`, in `tests/Architecture/`, since they can't sit in a module's test project. They're integration tests, marked `[Integration]`, because they read files, which is real I/O. So they run whenever every test runs: at final validation and on each version tag in the [workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag).

- **One test compares each project's references with the [module map](modules.md#module-map).** It checks that there are no cycles, no references between the apps, and no module named by the platform. It reads the "May use" columns of the map's tables in `modules.md`, so the map keeps one home, and matches each name there to a project by the [naming rules above](#code-and-tests-sit-in-mirrored-trees-by-part-in-one-solution).
- **A change to the tables' format fails that test, which then needs a fix.** Code reads a Markdown table, so a new column or a renamed heading breaks it.
- **During development, the reviewer checks references**, as [its instructions](../../.agents/subagents/reviewer.md) say.

### Sync's integration tests run an SSH server inside the test process

Sync's [setup](sync.md#public-interface) takes an SSH address, and test-only switches are [not allowed](../../AGENTS.md#test-design), so Sync's integration tests need an SSH server that runs git. Each test starts its own server inside the test process, built on Microsoft's [dev tunnels SSH library](https://github.com/microsoft/dev-tunnels-ssh/blob/main/src/cs/Ssh/README.md), a .NET SSH client and server.

- **The test runs git for the server.** The library runs no commands itself, so the test answers each command request by starting `git-upload-pack` or `git-receive-pack` from the installed git, on a bare repository the test owns.
- **No install, administrator rights, or network is needed, except git.** Every machine that runs these tests needs git installed.
- **The server accepts both SSH address forms**, as the [risk table](overview.md#risks-the-later-rows-must-resolve) needs.
- **The tests sit in Sync's test project, marked `[Integration]`**, as [decided above](#attributes-mark-each-tests-kind-in-one-test-project-per-module).
- **Sync's unit tests cover reading both address forms, and its integration tests use the URL form with their own server's port.** The URL form can name a port.
- **Each test of the scp-style form binds a loopback address of its own, `127.0.0.N`, on port 22**, so such tests can run side by side. That form can't name a port, so it always reaches port 22 ([git URLs](https://github.com/git/git/blob/master/Documentation/urls.adoc)). Windows doesn't reserve ports below 1024 for administrators; this rests on one probe, in which a normal user bound `127.0.0.2:22` on your PC on November 6, 2026. Not confirmed on other machines, or on one whose own SSH server listens on port 22.
- **Git's version isn't pinned.** The README will list git as a prerequisite, as [above](#three-files-pin-every-version) says, and the workflow uses the git its Windows runner image includes ([Windows Server 2025 image](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)). The tests use only `git-upload-pack` and `git-receive-pack`. Likely, but not confirmed: their wire protocol stays compatible across git versions, since the client asks for protocol version 2 and must ignore capabilities it doesn't know ([protocol v2](https://git-scm.com/docs/gitprotocol-v2)).
- **The [Windows proof](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) ran on this server first, and found two gaps.** Sync's transport avoids both:
  - When a client offers an RSA key with a SHA-1 signature, the server throws and drops the connection, instead of refusing the offer. Sync never offers SHA-1, as [Sync](sync.md#each-device-signs-in-with-its-own-rsa-key) proposes.
  - When the client closes a channel, the server doesn't close it back, as SSH requires ([RFC 4254](https://www.rfc-editor.org/rfc/rfc4254#section-5.3), [SshChannel](https://github.com/microsoft/dev-tunnels-ssh/blob/main/src/cs/Ssh/SshChannel.cs)). The transport lets the server end each command first, as the [proof found](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport).

The library ran on .NET 11 RC 3 in the Windows proof. Its latest package, 3.12.42 from August 24, 2026, is built for .NET Standard 2.1 and .NET Framework 4.8 ([NuGet](https://www.nuget.org/packages/Microsoft.DevTunnels.Ssh)).

### The desktop app carries its own .NET runtime

The desktop app is self-contained: it carries the .NET runtime it runs on, so installing it needs no runtime install and no administrator rights, as the [desktop Shell](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date)'s per-user install requires.

- **A .NET security fix reaches users only in a release of ours**, since a self-contained app's runtime is upgraded only by releasing a new version of the app ([self-contained apps](https://learn.microsoft.com/en-us/dotnet/core/deploying/#publish-as-self-contained)).
- Likely, but not confirmed: the runtime mostly costs size on the first install. Velopack's later updates are delta packages, binary patches of the files that changed ([delta updates](https://docs.velopack.io/packaging/deltas)), so a runtime that didn't change adds little to them.

### A GitHub Actions workflow checks each push and publishes each version tag

One GitHub Actions workflow, on a Windows runner, builds both apps, so releases don't depend on one PC. It uses the [pinned versions](#three-files-pin-every-version) and the two test commands [above](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test). The [coder](../../.agents/subagents/coder.md) writes its file in `.github/workflows/`, as it writes the version files and other build files. That folder is [review surface](../../AGENTS.md#contributions), so you review every change to the file, such as to the time limit or to how it uses the signing secrets.

- **Each push builds the code and runs the unit tests**, with plain `dotnet test`. [The next section](#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue) says which unit runs it times.
- **A version tag runs every test kind, unit, UX, and integration, then builds, signs, and publishes one GitHub release.** The Android tests that need an emulator run in a [Linux job](#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release), and publishing waits for it. The release holds the desktop installer, Velopack's update files, and the Android package (APK). Velopack's flow downloads the last release, packs the new one, and uploads it ([GitHub Actions](https://docs.velopack.io/distributing/github-actions)). The APK is built and signed with `dotnet publish` ([publish from the command line](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli)), with the key from [its secret](#the-apk-signing-key-lives-in-a-github-actions-secret-backed-up-offline).
- **A failed test or build publishes nothing**, as [Fail fast](../../AGENTS.md#code-design) asks.
- **The tag is the version's only home.** The APK's [version code](https://developer.android.com/studio/publish/versioning) is computed from it.

Proposed, each a decision to confirm:

- **A tag such as `v1.2.3` gives the version 1.2.3** to the desktop app's files and its Velopack package. The workflow runs on every tag that starts with `v`, and fails before building when the rest isn't three numbers, `<major>.<minor>.<patch>`, with no leading zeros, as [Semantic Versioning](https://semver.org/#spec-item-2) requires, so a mistyped tag isn't silently ignored.
- **Only a commit on the default branch is released.** Before building, the release job fails unless the tag's commit is on the default branch, which git's [`merge-base --is-ancestor`](https://git-scm.com/docs/git-merge-base#Documentation/git-merge-base.txt---is-ancestor) checks. Velopack updates every installed copy, so a tag on an unmerged branch would otherwise reach every user.
- **Until the Android app's first step, a release holds only the desktop files, and there's no Linux job.**
- **The app's name lives only in the `Product` property of `Directory.Build.props`**, which the build writes into every assembly, where the desktop platform's [App name](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) reads it. The release workflow reads it with `dotnet msbuild <project> -getProperty:Product` ([evaluate properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties)) for the installer and the release, and fails when it's empty.
- **The release is published at once, named with the app's name and its version**, such as `<name> 1.2.3`. It holds the installer and Velopack's update files, packed under the app's [installer ID](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), but not Velopack's portable zip, since a zipped folder was [ruled out](desktop/shell.md#ruled-out).
- **The desktop app is built for x64 processors only.** Windows on Arm runs it through emulation ([emulation on Arm](https://learn.microsoft.com/en-us/windows/arm/apps-on-arm-x86-emulation)).
- **Each job's token gets only the permissions its job needs.** The timed job writes issues, and only the job that publishes writes the repository's contents, which a release needs ([`GITHUB_TOKEN`](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token)).
- **Every job names its runner image exactly, `windows-2025`, not the latest one**, so a new image arrives only when you change the name ([runner images](https://github.com/actions/runner-images)).
- **The workflow uses only GitHub's own actions, each pinned to its major version.** GitHub already runs the jobs, so its actions add no one new to trust.
- **The release job gives its token to Velopack's download of the last release**, so GitHub's lower limit for requests without a token doesn't fail it ([rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)).

Not confirmed: that a GitHub-hosted Windows runner can show a tray icon and a message box, which the desktop's integration tests need. Reports disagree on whether its session has a desktop ([FlaUI on GitHub Actions](https://www.chrismalpass.net/posts/flaui-github-actions-cicd/), [The Green Report](https://www.thegreenreport.blog/articles/integrating-flaui-tests-into-a-cicd-pipeline-with-github-actions/integrating-flaui-tests-into-a-cicd-pipeline-with-github-actions.html)). If it can't, every version tag fails until this page picks another place for those tests.

### The workflow times the unit run, and a slow run opens one issue

The [workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) times a push's unit run, not counting the build, against the time limit [AGENTS.md](../../AGENTS.md#test-design) sets for the unit suite. Only the workflow acts on the limit: it alone opens, updates, and closes the issue below.

- **A unit run on a PC prints its duration, and nothing else.** It doesn't fail on the limit, doesn't send work back, and doesn't open or update an issue. Plain `dotnet test` already prints the duration: its run summary ends with a `duration:` line, which counts from the end of the build to the end of the run ([the summary](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Test/MTP/Terminal/TerminalTestReporter.cs), [where the count starts](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs)).
- **The limit is one value in the workflow file, its only home.** It starts at 5 seconds, and you change it there. Other pages link to this section rather than repeat the number.
- **A unit run over the limit opens a GitHub issue that lists each test's time, slowest first.** The push itself stays green.
- **Only one such issue is open at a time.** When the issue already exists, the workflow adds a note to it instead of opening another. The note holds the run's duration and, like the issue, each test's time, slowest first.
- **The workflow closes the issue when a timed run is under the limit**, so raising the limit also closes it.
- **The workflow needs permission to write issues**, which its `permissions` key grants to the token GitHub gives each run ([`GITHUB_TOKEN`](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token)).
- **Only pushes to the default branch are timed, one run at a time.** Pushes to the proof branch and other branches never open a timing issue. The timing job runs in a GitHub Actions [concurrency group](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency), so each run waits for the one before it to finish, and two runs can't each open an issue. When several runs wait, GitHub keeps only the newest, so the latest push is always timed.

Proposed, each a decision to confirm:

- **Each test's time comes from the JUnit reports xUnit.net writes**, one per test project ([xUnit.net on MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)), and the run's duration from the `duration:` line. The timed run adds xUnit.net's JUnit report option and one results folder to plain `dotnet test`. Neither changes which tests run. The option is `--report-xunit-junit` in the pinned xUnit.net v3 4.0.2, as [the workflow](../../.github/workflows/build.yml) uses it, not the `--report-junit` its documentation names. The unit job hands the reports and its console output to the timing job.
- **A small C# program, `Memoria.ToDo.Testing.UnitRunTiming`, decides what to do and writes the text**: open the issue, add a note, close it, or nothing. The workflow passes it the run's files, the limit, and whether the issue is open, then makes the GitHub calls, so the program does no network I/O. It lives in `tests/Testing/UnitRunTiming/`, beside the kind attributes, and its tests, `Memoria.ToDo.Testing.UnitRunTiming.Tests`, in `tests/Testing/UnitRunTiming.Tests/`. [Its command line](#the-timing-program-reads-the-runs-files-and-prints-one-word) is below.
- **The workflow finds the one issue by a label of its own**, so editing the issue's title doesn't break it. The label and the issue's title are constants in the workflow file, and the program writes only the text. The workflow creates the label when it first opens the issue.
- **A failed unit run isn't timed.** The push is already red, and the times would describe a run that stopped early.

Not confirmed by a run: the `duration:` line with the pinned .NET 11 SDK. This page read it in the SDK's source.

Likely, but not confirmed: hosted runners' timings vary from run to run, so noise alone can open an issue, which is one reason the limit is easy to change.

Not confirmed: how much of the limit goes to starting each test project's test application, since `dotnet test` starts up to one per processor at a time ([`dotnet test` with MTP](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp)). Also not confirmed by a run: xUnit.net's JUnit report and the test times it records.

#### The timing program reads the run's files and prints one word

The workflow runs the [timing program](#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue) after a passing unit run. The program prints its decision as one word and writes the issue's text to a file, so the workflow parses nothing. Proposed, each a decision to confirm.

The program takes the five arguments the table lists, each required once:

| Argument | What it gives |
| --- | --- |
| `--reports <folder>` | The unit run's results folder. The program reads every `*.junit.xml` file in it and its subfolders. |
| `--test-output <file>` | The unit run's saved console output, which holds the `duration:` line. Terminal color codes in it are ignored. |
| `--limit-seconds <number>` | The time limit, a positive number with `.` for decimals, such as `5` or `2.5` |
| `--issue-open <true or false>` | Whether the timing issue is open now |
| `--text-file <file>` | Where the program writes the issue's text, in Markdown |

The next table lists what the program decides in each case. A run is over the limit only when its duration is greater than the limit:

| The run is | The issue is | It prints | It writes to the text file |
| --- | --- | --- | --- |
| Over the limit | Not open | `open` | The issue's text: the run's duration, the limit, and each test's time |
| Over the limit | Open | `note` | A note with the same content |
| At or under the limit | Open | `close` | One line with the run's duration and the limit, for the closing comment |
| At or under the limit | Not open | `none` | Nothing, and it doesn't create the file |

- **Each test's time is a row of a Markdown table**, with columns for its class, its name, and its time in whole milliseconds. Rows go slowest first, and tests with the same time go by class, then name. Every test case in the reports is listed. The text gives the limit in seconds, such as `2.5 s`.
- **A text longer than GitHub's limit of 65,536 characters for an issue or a comment keeps the slowest tests that fit**, and ends by saying how many were left out ([body limit](https://github.com/orgs/community/discussions/41331)).
- **Exit code 0 means the program decided**, whatever it printed. **Exit code 1 means the input is invalid**: the program prints one line naming the problem to the error output, prints nothing else, and writes no file, so the workflow step fails before any GitHub call. Any other non-zero code is a crash, which also fails the step.
- **The program checks every input before it writes anything.** Input is invalid in these cases:
  - **Arguments**: one is missing, repeated, or unknown; the limit isn't a positive number; `--issue-open` is neither `true` nor `false`; or the text file's folder doesn't exist.
  - **Reports**: the folder doesn't exist or holds no report, a report isn't JUnit XML, or a test case has no time in seconds. A report with no test cases is valid, since a test project may have no unit tests.
  - **Console output**: it doesn't have exactly one `duration:` line, or the duration isn't in the [summary's format](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Test/MTP/Terminal/TerminalTestReporter.cs) with hour, minute, second, and millisecond parts, such as `312ms`, `4s 312ms`, or `1m 05s 012ms`. A run of a day or more is rejected.
- **Its unit tests give the deciding code text, not files**, so they do no I/O. A few integration tests run the program on files, to check its arguments, the printed word, the text file, and the exit codes.

Not confirmed: whether the `duration:` label is translated when the runner's language isn't English. If it is, the program fails with exit code 1 and names the missing line.

### A Linux job runs the Android tests on an emulator before each release

On each version tag, one job of the [release workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) runs on a GitHub-hosted Linux runner. It starts an Android emulator and runs each Android module's [test app](#each-android-module-builds-for-both-the-pc-and-android): the UX tests of Android screens and the integration tests of Android-only parts. Publishing waits for it, so the version tag stays the one place that checks a release.

- **The job runs on Linux because GitHub states the emulator's hardware acceleration only for its Linux runners** ([GitHub-hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)). Likely, but not confirmed: its Windows runners can't run the emulator, because they lack nested virtualization; an issue about it is open ([runner-images issue 9343](https://github.com/actions/runner-images/issues/9343)).
- **[XHarness](https://github.com/dotnet/xharness) runs the test apps from Linux.** It installs each one on the emulator, runs it, and collects its results ([system requirements](https://github.com/dotnet/xharness#system-requirements)).
- **XHarness is a .NET tool, so `.config/dotnet-tools.json` pins it**, as it [pins every tool](#three-files-pin-every-version). It's published only as a prerelease, on Microsoft's dotnet-eng feed rather than NuGet.org ([installation](https://github.com/dotnet/xharness#installation-and-usage)).
- **A failed Android test, or an emulator that doesn't start, publishes nothing**, like any [failed test](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag).

Not confirmed: the emulator on a hosted Linux runner with the test runner the implementation finds, as [the Android modules' section](#each-android-module-builds-for-both-the-pc-and-android) says.

### The pinned workload set decides the JDK and Android SDK

The Android build needs a JDK and Android SDK packages, such as a platform and build tools, and none of the [three version files](#three-files-pin-every-version) names them. The pinned workload set decides both, so no fourth version file is needed.

- **Every PC and the release workflow run the Android workload's `InstallAndroidDependencies` build target.** It installs the Android SDK packages the project needs, and a JDK, into the folders `AndroidSdkDirectory` and `JavaSdkDirectory` name, outside the repository ([install dependencies](https://learn.microsoft.com/en-us/dotnet/android/getting-started/installation/dependencies)). Since `global.json` pins the workload set, a workload update brings the SDK packages it needs.
- **If a build shows that the JDK it installs isn't fixed, a `.java-version` file at the repository root pins the JDK's major version.** The workflow reads it through `actions/setup-java`'s `java-version-file` input ([setup-java](https://github.com/actions/setup-java)), and on a PC you install that JDK yourself. The Android SDK stays with the workload.

Not confirmed: that the workload set fixes the JDK `InstallAndroidDependencies` installs, and which version that is. Microsoft recommends its OpenJDK 21 for .NET for Android, tested against its builds, on the same page; that page dates from March 2026 and isn't confirmed for .NET 11.

The proof ran the target with workload 37.2.0-rc.2.84 and an empty Android SDK folder. It installed the android-37.0 platform, build tools 37.0.0, and the platform tools, then failed: it needs the SDK's command-line tools to accept the licenses, and doesn't install them. A JDK was already in place, so whether the target installs one wasn't tested.

### A script in this repository builds libgit2 for Android, and its binaries are committed

The Android app ships libgit2 built from the commit LibGit2Sharp pins, so both apps run the same libgit2. A script in this repository builds it, one file per ABI, only when that commit changes. The files it builds are committed with a note of the commit and the NDK that built them, so normal builds, on a PC or in the [workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), need no NDK or CMake.

- **The commit is LibGit2Sharp's.** LibGit2Sharp's own binaries come from one pinned libgit2 commit, built with CMake into a file named after that commit, the name LibGit2Sharp loads ([build script](https://github.com/libgit2/libgit2sharp.nativebinaries/blob/master/build.libgit2.sh)). LibGit2Sharp 0.32.0, the latest on November 5, 2026, carries libgit2 1.8.6 ([releases](https://github.com/libgit2/libgit2sharp/releases)), which it loads on Android as `libgit2-5853918.so`.
- **The script runs when a LibGit2Sharp update in `Directory.Packages.props` changes that commit**, and the same change commits the new files and note. So a libgit2 fix waits for a LibGit2Sharp release that carries it: libgit2 1.9.7 shipped August 13, 2026 ([libgit2 releases](https://github.com/libgit2/libgit2/releases)).
- **It builds for arm64-v8a and x86_64 with NDK r28 or later**, which aligns libraries to the 16 KB memory pages newer phones use ([page sizes](https://developer.android.com/guide/practices/page-sizes)). The released APK holds arm64-v8a, and debug builds add x86_64 for the emulator, as the [Android app](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material) proposes.
- **It leaves out libgit2's own SSH and HTTPS, and with them OpenSSL and libssh2.** libgit2 tries our registered transport first ([transport.c](https://github.com/libgit2/libgit2/blob/v1.8.6/src/libgit2/transport.c)).
- **Proposed: the script, its files, and the note live in `src/Android/App/libgit2/`**, beside the composition root that packs them into the APK. The script is `build-libgit2.ps1`, and it starts from the proof's, `proofs/Android/build-libgit2.ps1` on the [proof branch](#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided). This is a decision to confirm.
- **Proposed: the Android app's build fails, naming the script, when no committed file has the name the pinned LibGit2Sharp loads.** So a LibGit2Sharp update that moves libgit2 can't build until the script runs. This is a decision to confirm.
- **Nothing libgit2-related goes to this repository's Releases**, which the apps read for updates.

The proof built libgit2 this way with NDK r30, the latest then ([changelog](https://github.com/android/ndk/wiki/Changelog-r30)), in about 40 seconds per ABI on a cloud dev box, and the [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) showed the result works. The script keeps the three settings the proof found the build needs:

- **`-Dinline=__inline__` in the C flags.** libgit2 builds as C90, and NDK r30's kernel headers use `static inline`, which C90 lacks.
- **SONAME left on.** libgit2 applies the file name LibGit2Sharp loads, `libgit2-5853918.so`, only when SONAME is on.
- **Stripping with `llvm-strip`.** The NDK compiles with debug information, so each file is about 8.3 MB before and 1.6 MB after.

The proof also left out the files of LibGit2Sharp.NativeBinaries, the package that holds LibGit2Sharp's own libgit2 builds, because Android would otherwise pick its Linux build.

Not confirmed: that two runs of the script with the same commit and NDK build identical files. A reviewer can check a committed file only by rebuilding it and comparing.

### The APK signing key lives in a GitHub Actions secret, backed up offline

Every update must be signed with the same key ([app signing](https://developer.android.com/studio/publish/app-signing)), so losing it ends updates. The key store lives in a GitHub Actions secret, so the [release workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) can sign the APK. You keep an offline backup of the key store and its passwords outside GitHub.

- **Only the job that publishes a version tag uses the signing secrets.** A workflow reads a secret only where it names it ([secrets](https://docs.github.com/en/actions/concepts/security/secrets)), and only you commit workflows, as [Contributions](../../AGENTS.md#contributions) says.
- **The key never enters the repository.** Development builds and the proofs use the SDK's debug key, whose APKs can't update a released app.

Likely, but not confirmed: the key store goes into the secret as base64 text, which GitHub suggests for small binary files ([base64 secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets#storing-base64-binary-blobs-as-secrets)), and its two passwords, for the key store and for the key, go into two more secrets. The build reads the passwords from environment variables, which keeps them out of logs ([publish from the command line](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli)).

## Public interface

None. No module uses the build tools.

## Failure modes

- **The Android signing key is lost**: no later Android package (APK) installs over the app, and Android's installer says so ([Android Shell](android/shell.md#failure-modes)). [Where the key lives](#the-apk-signing-key-lives-in-a-github-actions-secret-backed-up-offline) says how it's kept and backed up.
- **A project uses a module the map doesn't allow**: during development, the reviewer sees it in the project file ([Modules](modules.md#failure-modes)); when every test runs, an [architecture test](#architecture-tests-check-the-module-map) fails.
- **A test class has no kind attribute, or two**: the build fails with [our analyzer's](#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two) error, which names the class, and the editor underlines it as you type.
- **The pinned Microsoft.CodeAnalysis.CSharp is newer than a compiler that builds the tests**: that compiler skips our analyzer with warning CS9057, which `Directory.Build.props` [makes an error](#our-analyzer-fails-the-build-when-a-test-class-has-no-kind-attribute-or-two), so the build fails.
- **The constants file's layout changes, so `Directory.Build.props` can't read the two names its filter needs**: the filter matches nothing, and plain `dotnet test` fails with exit code 8, as [the filter's section](#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) says.
- **A timed unit run takes longer than the limit**: the push stays green, and the workflow opens [one issue](#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue), or adds a note to it when it's already open.
- **The timing program rejects its input**, such as a missing report: the timing job fails and names the problem, and the issue stays as it was, as [its command line](#the-timing-program-reads-the-runs-files-and-prints-one-word) proposes.
- **A version tag's tests or build fail, including the Android tests in the [Linux job](#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release) or its emulator**: the [release workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) publishes nothing, and its run on GitHub shows the failure.
- **A tag starts with `v` but isn't a version, such as `v1.2`**: the workflow fails before building, as [proposed](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), and publishes nothing.
- **A version tag's commit isn't on the default branch**, such as a tag pushed before its commit: the workflow fails before building, as [proposed](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag), and publishes nothing. Push the commit to the default branch, then re-run the workflow, which checks the branch again ([re-runs](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs)).
- **A LibGit2Sharp update moves to another libgit2 commit, but the Android files aren't rebuilt**: the Android app's build fails and names the [libgit2 script](#a-script-in-this-repository-builds-libgit2-for-android-and-its-binaries-are-committed), as that section proposes.

## Ruled out

The options below were considered and rejected, grouped by the decision they belong to.

- When the throwaway proofs run, and where their code lives:
  - **No exception for the proofs, with libgit2's Android build decided only as far as the proof needs and revisited later**: "Decided" would hold an open question, which the [process](../contributing/process.md#changing-the-design) doesn't allow.
  - **No exception for the proofs, with every choice decided first**: if the proof on a phone fails, the libgit2 build and Android test choices are wasted, and the riskiest check waits for every tool choice.
  - **A separate scratch repository for proof code**: you preferred a branch of this repository that's never merged.
- Which AGENTS.md rules proof code follows:
  - **Every rule**: module rules and tests first would slow down code that's thrown away.
  - **Only no-duplication and fail-fast from the code and test rules**: keeping single rules from a waived section is harder to follow and check than waiving whole sections.
  - **Keep Contributions principle 4 in full**: tests first and independent review would slow down code that's thrown away, as the waived Tests rules already accept.
  - **Waive only tests first and independent review, keeping the design step and the delivery report**: what a proof shows already goes into the design files it checks, so a design step and a report add little for code that's thrown away.
- How the code is laid out and named:
  - **One folder per module, holding its project and its test projects side by side**: tests would sit beside shipping code, and paths would be deeper.
  - **One solution per part: shared library, desktop, and Android**: three solutions to keep in step.
  - **The desktop platform's test program inside the platform's test folder**: the test project would compile the program's files too ([default includes](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview#default-includes-and-excludes)).
- Where each tool and package version lives:
  - **Pinning everything except the workload set**: Android workload updates would arrive by themselves, so two machines could build different packages from one commit.
  - **Versions in each project file**: one version would live in many files, which breaks the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication).
  - **`vpk` and the Velopack package each pinned on its own, with a check that they match**: one version in two files, which the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) forbids even with a check.
  - **No `vpk` entry, with the workflow running `vpk` at the Velopack package's version** ([`dotnet tool exec`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec)): it reverses the decided tool pin, and `vpk` would run only in the workflow.
- How Visual Studio Code finds the pinned SDK:
  - **The C# extension's SDK path setting in the repository's settings**: it can't name a folder through an environment variable, and each PC's folder has the user's name in it.
  - **The pinned folder on the user's `PATH` for good**: it changes the system SDK every other project uses, which the [pinned SDK](#three-files-pin-every-version) avoids.
- Which test framework the tests use:
  - **MSTest**: it also runs on MTP and makes a new instance of the test class for each test ([MSTest lifecycle](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests-lifecycle)); you preferred xUnit.net v3.
  - **NUnit**: by default, one instance of a test class serves all its tests, so fields carry state between them unless the class or assembly opts out ([FixtureLifeCycle](https://docs.nunit.org/articles/nunit/writing-tests/attributes/fixturelifecycle.html)). It runs on MTP only once its NUnit runner is turned on ([NUnit and MTP](https://docs.nunit.org/articles/vs-test-adapter/NUnit-And-Microsoft-Test-Platform.html)).
- Which assertion and test-double libraries the tests use:
  - **Shouldly, for clearer failure messages**: one more dependency, only to make failures easier to read.
  - **A mocking library, such as [NSubstitute](https://github.com/nsubstitute/NSubstitute)**: it invites asserting calls and call order, which [AGENTS.md](../../AGENTS.md#test-design) allows only when a requirement promises the call.
  - **FluentAssertions 8**: it needs a paid license for commercial use ([Xceed's FAQ](https://xceed.com/fluent-assertions-faq/)).
- How unit, UX, and integration tests stay apart:
  - **Separate unit and integration test projects for each module**: you preferred one test project per module, with attributes marking each test's kind.
  - **A test with no kind attribute is a unit test**: a forgotten `[UX]` or `[Integration]` would run a test that does real I/O with the unit tests, and the time check would flag only a slow one.
  - **xUnit.net's built-in `[Trait("Category", "...")]`**: a misspelled kind silently puts the test in no group.
  - **A kind attribute on a class or a method, with the method's replacing the class's**: xUnit.net adds a method's traits to its class's rather than replacing them, so our attribute code would have to do the replacing.
  - **A kind attribute on each test method only**: every test would repeat its attribute.
- How the unit suite's time limit is checked:
  - **Fail the unit run with `--timeout` set to the limit**: a busy machine can fail on time alone, and a stopped run reports no results for the tests it didn't finish.
  - **Leave it to review, with nothing measured**: nothing would check the limit.
  - **Local runs that fail or open an issue on the limit**: only the workflow acts on the limit, so the issue has one source.
  - **The limit in a GitHub repository variable**: it isn't in the repository, and the workflow fails when it's unset.
  - **You close the issue by hand**: the workflow closes it when a timed run is under the limit, so raising the limit also closes it.
  - **A new issue for each slow push**: when the issue already exists, the workflow adds a note to it instead, with the run's duration and each test's time, slowest first.
  - **A note with only the run's duration**: you would see each test's time only from the push that opened the issue, though the slowest test can change between pushes.
  - **The timing logic written into the workflow file, in PowerShell**: it gets no unit tests, so only a push to GitHub checks it.
  - **A Node.js script with Node's own tests**: its tests would sit outside `dotnet test`, so final validation would need one more command.
  - **Finding the issue by its title**: editing the title would make the workflow open a second issue.
- Which tests `dotnet test` runs:
  - **A C# script, `build.cs`, run with `dotnet run build.cs`, for final validation and later releases**: a script to maintain, when one `-p:` property already adds the other kinds.
  - **A PowerShell 7 script**: it adds PowerShell 7 to what you need, for the same few commands.
  - **Plain `dotnet test` runs every test, and development always passes the unit filter**: a plain run during development would run UX and integration tests, which [Run timing](../../AGENTS.md#run-timing) allows only at session end.
  - **Pass `--ignore-exit-code 8`, so a run in which no test ran passes** ([MTP options](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options#platform-options)): a broken filter that matches nothing, such as one with a misspelled kind, would pass silently.
- Which tools run a UX test of one screen:
  - **WPF tests that start their own STA thread and dispatcher, instead of Xunit.StaFact**: we'd write and maintain that thread code, to save one dependency.
- How Android modules build for their tests:
  - **Android modules build only for Android, and all their tests run on an emulator at final validation**: no unit tests during development for four modules.
  - **Each Android module splits into a logic project and an Android project**: it breaks [one project per module](modules.md#a-module-is-one-capability-behind-a-small-interface), an architecture change.
- How the architecture test knows the module map:
  - **The map in a data file, with a script that writes the tables in `modules.md`, as [generate.mjs](../../.agents/generate.mjs) writes agent files**: a generator and a staleness check to maintain, when the test can read the tables.
  - **The test lists the allowed references itself**: a second home for the map, which breaks the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication), and a map change that skips the test goes unnoticed.
- How a test class with no kind attribute, or two, is found:
  - **An architecture test that reads every built test project**: it catches a missing or doubled attribute only when every test runs, at final validation and on each version tag, not when you build.
  - **A tool that reads each built test project after it builds**: the editor shows nothing, and Android test projects build for Android, which adds moving parts.
  - **Kind-specific test attributes, such as `[UnitFact]`, with Microsoft's [BannedApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md) banning plain `[Fact]` and `[Theory]`**: the kind would move from the class to each method, reversing the [decision to mark each class](#attributes-mark-each-tests-kind-in-one-test-project-per-module), and every test attribute, `[WpfFact]` included, would need a version for each kind.
  - **The tester lists them with two `dotnet test --list-tests` runs at final validation**: nothing fails on its own, and how the filters behave in those listings wasn't confirmed.
  - **No check, leaving it to the reviewer**: a missing attribute is easy to miss.
- Which classes the analyzer checks:
  - **Only classes that declare a test method**: a derived class that only inherits its tests would go unchecked.
- How `Directory.Build.props` gets the trait name and the unit kind name:
  - **It keeps its own copy, with an exception in AGENTS.md for values a tool can't read**: the [core rule](../../AGENTS.md#core-rule-no-duplication) would gain an exception.
  - **It writes the constants file before each compile**: this reverses the one checked-in C# file, and the constants don't exist until the first build, so a fresh clone shows errors in the editor.
- What the analyzer's tests build on:
  - **Microsoft's analyzer-testing packages, such as Microsoft.CodeAnalysis.CSharp.Analyzer.Testing**: by default they download reference assemblies from NuGet during a test ([ReferenceAssemblies](https://github.com/dotnet/roslyn/blob/main/src/RoslynSdk/Microsoft.CodeAnalysis.Testing/Microsoft.CodeAnalysis.Analyzer.Testing/ReferenceAssemblies.cs)), which is real I/O. Avoiding that means supplying the references ourselves, as we do without them, so they'd add packages for little.
- Where the kind attributes and the analyzer live:
  - **A part's Fakes project**: test projects in every part use the attributes, and each Fakes project has another job, holding one part's fakes.
  - **One project, `Memoria.ToDo.Testing`, in `tests/Testing/`**: the analyzer's two projects can't sit in folders inside it, since it would compile their files too, so `tests/` would gain three folders that mirror nothing, instead of one.
- Where Sync's integration tests find an SSH git server:
  - **A real OpenSSH server on the machine, such as Windows' optional one**: it needs administrator rights and machine state that tests share, and GitHub's hosted runners run service containers, such as an SSH server, only on Ubuntu ([service containers](https://docs.github.com/en/actions/tutorials/use-containerized-services/use-docker-service-containers)).
  - **Real hosts, with a test repository and a stored key**: tests would share one repository, need a secret on every machine, and fail offline.
- Whether git's version is pinned for Sync's integration tests:
  - **Pin git's version**: every PC and the workflow would install one git version, with a version file and an install step to maintain, for a protocol that likely doesn't change.
- Whether the desktop app carries its own .NET runtime:
  - **Framework-dependent, with Velopack installing the .NET Desktop Runtime first** ([bootstrapping](https://docs.velopack.io/packaging/bootstrapping)): the .NET installer installs system-wide ([install on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)), so it likely needs administrator rights, which the per-user install avoids.
- Where builds and releases run:
  - **A release command on your PC, with your GitHub token**: releases would depend on one PC, and nothing would check pushes.
  - **The workflow checks pushes, and releases are made on your PC**: two paths would build the same apps.
- What a desktop release holds, and which tags make one:
  - **Prerelease tags, such as `v1.2.3-beta.1`**: nothing installs or tests a prerelease yet.
  - **A tag on any commit**: a commit nobody merged would reach every installed copy through updates.
  - **A draft release you publish by hand**: one more step for each version, after every test has passed.
  - **Velopack's portable zip as well**: a second way to install, which users would have to choose between.
  - **An Arm64 build as well**: a second package to build and test, while emulation already runs the x64 one.
  - **One token with every permission for the whole workflow**: a step that only builds could change the repository.
- Whether the workflow file is review surface:
  - **Leave the review-surface list unchanged**: a change to the time limit or to how the workflow uses the signing secrets would show only in a commit diff.
- Which tests the workflow runs:
  - **Every test kind on each push**: each push would wait for UX and integration tests, which already run at final validation and on each version tag.
- Where Android UX tests and tests on a device run before a release:
  - **You run them on your PC, with an emulator, before you push a version tag**: nothing checks that they ran, and releases again depend on your PC, which the [workflow](#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) was chosen to avoid.
  - **A self-hosted runner on your PC runs them as a job of the release workflow**: your PC must be on for each release. GitHub recommends self-hosted runners only for private repositories, since a pull request from a fork can run code on the machine, and this repository is public ([self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners)).
- How the JDK and Android SDK versions are chosen:
  - **Nothing pinned, with each PC and runner using what it has**: two machines could build different packages from one commit, which [pinning the workload set](#three-files-pin-every-version) prevents.
- How libgit2 is built for Android, and where its binaries live:
  - **Every full build compiles libgit2, cached until the pinned commit changes**: every machine, the release workflow included, needs the NDK and CMake, and first builds are slow.
  - **A separate repository publishes it as a NuGet package**: a second repository to keep, and GitHub Packages needs a token even for public packages ([NuGet registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)).
- Where the APK signing key is kept:
  - **Only on your PC, with an offline backup**: the release workflow couldn't sign the APK, so releases would move to your PC, which is ruled out above.
  - **A cloud key vault the workflow reads**: it needs a cloud subscription for a personal app.
