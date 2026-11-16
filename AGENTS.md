# Memoria-ToDo

These rules apply to every change, by a human or AI, to code, tests, configuration, scripts, documentation, or AI instructions. On the never-merged [proof branch](docs/architecture/technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided), [Code design](#code-design), [Tests](#tests), and [Contributions](#contributions) principle 4 don't apply; every other rule does. The application's purpose and design are decided; the [design backlog](docs/architecture/README.md#design-backlog) tracks them.

## Core rule: no duplication

Every piece of knowledge has exactly one home. Everything else references it, imports it, or is generated from it.

- **A duplicate is knowledge that would have to change in two places at once.** Code that only looks alike but changes for different reasons isn't a duplicate; sharing it couples unrelated parts.
- **First use: share it when that's easy.** Make new functionality a shared component right away if both hold:
  - **It stands on its own**: it has one responsibility you can name without mentioning its caller.
  - **Sharing costs little**: it adds no options, flags, or extension points its one caller doesn't use, and no new project, package, or dependency.

  Otherwise write it where it's needed.
- **Second use: always extract.** When a second place needs existing functionality, move it into a shared component and use it from both places. Never copy it, not even temporarily.
- **Make shared components easy to use.** Name each for what it does, with a small interface shaped by its callers' needs. A caller must be able to use it correctly without reading its code.
- **Reference, don't restate.** Link to the source instead of copying or paraphrasing it in docs, comments, instructions, and reports.
- **Generate, don't hand-copy.** When a tool needs its own copy of a file, generate it from the single source. Put in it only what the tool can't read elsewhere, such as metadata.
- **Search before adding.** Check whether the knowledge already has a home, starting from the [repository map](#exploring-the-code).

Exception: tests may state expected values literally. An independent expectation is a check, not a copy. Shared test setup still follows the rule.

## Code design

- **Modules contain change.** Code is split into modules, one per capability, over a shared platform. Keep each change inside one module. [Modules](docs/architecture/modules.md) says what a change may touch, and which changes are architecture.
- **Simplest thing that works.** Write the least code that fully solves today's problem. No speculative options, layers, or extension points. Between two working designs, pick the one that's easier to read and to delete.
- **Write fast, memory-efficient code.** Choose algorithms and data structures for the expected workload. Avoid unnecessary work, allocations, copying, and retained memory. Measure runtime and memory for performance-sensitive changes with representative inputs; preserve correctness and readability.
- **Small components, one job each.** Compose components instead of growing files that mix responsibilities. A component of more than one file gets its own folder named after it.
- **Depend on interfaces; wire in one place.** A component depends only on the small interfaces it uses, never on another component's internals or concrete types. Only the composition root chooses implementations of the interfaces that modules and the platform publish.
- **Extend, don't edit.** Add new behavior as new code registered beside the existing code. When behavior varies by kind, dispatch through a shared interface and a registration, not `switch` or `if` chains.
- **Fail fast.** Reject invalid input, configuration, and state at the earliest boundary that can detect it, before any side effect. Never swallow a failure or return a success-shaped fallback.

## Tests

### Test design

- **Test first.** Every feature and bug fix starts with tests that describe it. See every new unit and integration test fail for the expected reason before implementation, at the red run below; UX tests are written then but first run at the session's end. Make the tests pass without editing them. A behavior-preserving change starts and ends with the same unit tests passing, with UX and integration checks deferred.
- **Prove every new test can fail, once.** A test seen failing for the expected reason at the red run has this proof; don't prove it again. Every other new test, such as a UX test or one that passed at red, gets its proof in the session's one [closing failure proof](#run-timing), all together: temporarily apply a break that should fail each test, run, see each fail for its expected reason, then restore the code. Never break, run, and restore one test at a time. Final validation never repeats a failure proof.
- **Group breaks so each failure has one cause.** Two breaks interfere when one can change whether the other's tests fail, or why: they change the same code, one sits in code the other's tests run, or one stops a build or a screen from opening. Every break must build. Put breaks that don't interfere in one group, in as few groups as possible, and run each group once, selecting only its tests. One break may prove several tests that each fail for its reason. A test that fails for another break's reason, or passes when another break in its group could have hidden its own, shows interference: move one of the two breaks to another group, then rerun every test whose proof is still missing, in as few runs as the groups allow. A test that passes under its own break alone, or fails for an unrelated reason, is a defect in the test, the break, or the code.
- **Test requirement intent through the public interface**, or the internal classes [Modules](docs/architecture/modules.md#a-module-is-one-capability-behind-a-small-interface) lets a module's own tests use. Cover the requirement's observable outcomes, edge cases, and errors, not the implementation's structure. Assert what callers observe: results, errors, visible state, and the calls the requirement promises. Don't assert private state, log lines, or internal call order; a behavior-preserving refactor must not break the tests.
- **Let tests expose design problems.** Hard-to-test code is a signal to improve its structure, not to couple tests to its internals. Refactor production code to separate responsibilities and dependencies where needed. Don't weaken assertions, expose private state, or add test-only switches. Public-interface and module-boundary changes still follow [Contributions](#contributions).
- **Tests are a living spec.** Name each test as a sentence about one behavior, such as `rejects an empty name`. Group tests by component and scenario, so the names alone explain the component.
- **Classify every test as unit, UX, or integration.** Unit tests use no real I/O and never start an application or service; they cover every behavior, with its edge cases and errors. A UX test checks one screen in-process, with fake services, and never starts the app; UX tests cover only each screen's common scenarios, the paths users take most, and leave edge cases and errors to unit tests. Unit tests far outnumber UX tests, in every module with a screen too; keep integration tests far fewer, for real boundaries and wiring that other tests cannot verify. The whole unit suite, not counting the build, runs within a [time limit](docs/architecture/technology-stack.md#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue) that the release workflow checks. The [technology stack](docs/architecture/technology-stack.md#attributes-mark-each-tests-kind) says how kinds are marked and run.
- **Synchronize, never sleep.** Wait on completion signals, not delays, polling, or busy loops. Timeouts only bound failure.
- **Isolate what tests change.** Each test gets its own files and external resources. Share only immutable setup.
- **Never skip a test.** No `Skip`, `SkipUnless`, `SkipWhen`, `Assert.Skip`, `Explicit`, commented-out test, or test deleted to get a green run. A test that can't pass yet doesn't get merged. Every run [fails on a skipped test](docs/architecture/technology-stack.md#a-skipped-test-fails-the-run).

### Run timing

Building and running tests is the slowest part of a change, so run only what proves it, and only a few times per session.

- **Run only the tests that cover the change.** During development, select only the new and changed tests, or for a behavior-preserving change the existing tests that cover it, in the one test project that holds them, with the [narrowest filter](docs/architecture/technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test): methods, or a class only when every test in it is selected. Widen to the module's tests only when the change touches code the module shares across its components. Never run a whole test project, or plain `dotnet test`, during development.
- **Run at two checkpoints, not after edits.** Red: the tests' author runs the selected unit and integration tests once, after writing all of them, except those deferred to the session's end below. Green: the coder runs the selected unit tests once, when the implementation is complete. Between and after them, don't build or run tests, except to diagnose a failure you can't reason out from the code. Reuse passing evidence while the covered code, tests, dependencies, and configuration are unchanged.
- **Review fixes run scoped and together.** One run covers all the fixes, selecting only the tests that cover them, never the module's tests or the full run.
- **Run UX tests only at the session's end.** Write them at red, then run them first in the closing failure proof, and again at final validation. Tests that run only in the Android test app, on an emulator, wait for the session's end too. Run other new integration tests at red, then again only in the closing failure proof when red didn't see them fail, and at final validation. Never run UX or integration tests otherwise, including through build scripts or combined test commands. Every run of them uses the [commands the technology stack gives](docs/architecture/technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test), which stop a test that hangs.
- **Check the desktop before any run with screen tests.** Before a run that includes desktop UX tests, or integration tests that use the screen, first print for the user the [steps that keep the desktop usable without a Remote Desktop window](docs/architecture/technology-stack.md#keep-the-desktop-usable-without-a-remote-desktop-window), then run `node .agents/desktop-check.mjs`. When it reports the desktop unusable, don't start the run: stop and ask the user to unlock the PC or reconnect, quoting its message. When screen tests fail with lost focus, denied access, or waits that ran out, run the check again before treating them as code failures.
- **End the session with two steps, in order, once all work is done.** Start them only when every change in the session is green, every review finding is fixed or decided by the user, and nothing else is left to build. Nothing else starts them: not an agent finishing its task, not a review or its fixes.
  1. **Closing failure proof.** The tester proves every new test that red didn't see fail, in the fewest [non-interfering groups](#test-design), one run per group, and confirms the code is restored.
  2. **Full run.** The coder runs the [one command](docs/architecture/technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) that builds and runs every test. This is final validation.
- **The full run must end with zero failed and zero skipped tests.** Fix every failure it shows, including ones in code this session didn't touch, test first. Rerun only the fixed tests, then the full command again, until it's clean. Never retry a failure until it passes, skip it, or report it as pre-existing instead of fixing it. Stop and ask the user when a fix needs a decision, or when the run [needs a desktop](docs/architecture/technology-stack.md#failure-modes) the session doesn't have.

## Documentation

Follow [writing docs](docs/contributing/writing-docs.md): write for a person who joined last week, summary first, in plain words.

## Contributions

The user decides and reviews architecture; AI writes and reviews the implementation.

1. **Only the user commits.** AI never commits or pushes. The one exception: after the user approves its preview, the [`version-tag` skill](docs/architecture/version-tags.md#the-skill-previews-asks-you-then-runs-the-script) runs the script that pushes or deletes one version tag.
2. **Architecture lives only in the review surface**, the only files the user needs to read:
   - `README.md`, `docs/`, every `AGENTS.md`, `.agents/`, and `.github/workflows/`;
   - each module's and the platform's public interface;
   - files that add dependencies or set tool and runtime versions.

   Everything else is implementation, checked by tests and the `reviewer` agent.
3. **Implementation never decides architecture alone.** A decision is a choice users notice or later work must respect: a default, a limit, a timeout, or what happens on failure. When code needs a decision no design file makes:
   - **Ask first** when a different answer would waste much of the work, or is hard to undo once released. Examples: security, public interfaces, stored data formats, new dependencies, and module boundaries.
   - **Otherwise decide, record, and flag**: take the option you'd recommend and write it into the owner design file, as the [template](docs/architecture/template.md) says. List it under decisions to confirm in the delivery report.
4. **Deliver through the [process](docs/contributing/process.md)**, in the lightest lane that fits the change: design, tests first, code, independent review, and a delivery report.
5. **Log what users notice.** A change users would notice adds its entry to [`CHANGELOG.md`](CHANGELOG.md) in the same change, written for users who don't know the code, as the [changelog guide](docs/contributing/changelog.md) says. Internal work gets no entry.

## AI instructions and agents

`AGENTS.md` is the only AI instruction source. GitHub Copilot, OpenAI Codex, and Claude Code (v2.1.277 or later) read it natively.

- **No tool-specific instruction files.** Don't create `CLAUDE.md`, `CLAUDE.local.md`, `.claude/CLAUDE.md`, `.github/copilot-instructions.md`, `.github/instructions/`, or `.claude/rules/`. They duplicate or split these rules, and any `CLAUDE.md` makes Claude Code stop reading `AGENTS.md`.
- **Scope rules to folders.** Put rules that apply to one part of the repository in an `AGENTS.md` in that folder. Keep every `AGENTS.md` short: each one loads into every session.
- **Agents are defined only in `.agents/subagents/`.** `subagents.json` sets each role's description, models, and reasoning effort, and `<role>.md` its instructions. After changing `subagents.json`, run `node .agents/generate.mjs` to rewrite `.claude/agents/`, `.codex/agents/`, and `.github/agents/`. Never edit those generated files; `node .agents/generate.mjs --check` reports stale ones.
- **Skills**, when one is needed, live only in `.agents/skills/<name>/SKILL.md`. For Claude Code, which doesn't read that folder, `node .agents/generate.mjs` also writes a [pointer](docs/architecture/agent-skills.md#claude-code-reads-each-skill-through-a-generated-pointer) to each skill in `.claude/skills/`. Rerun it after adding or removing a skill or changing its frontmatter, and never edit the pointers.

### Checking the `.agents` scripts

Each script's tests sit beside it in `<script>.test.mjs`, on Node's test runner. Each top-level `describe` ends its name with its tests' [kind](#test-design), `(unit)` or `(integration)`. From the repository root, run the unit tests during development with `node --test --test-name-pattern="\(unit\)" ".agents/*.test.mjs"`, and every test at [final validation](#run-timing) with `node --test ".agents/*.test.mjs"`.

The [generator's tests](.agents/generate.test.mjs) check the Claude, Codex, and Copilot files [generate.mjs](.agents/generate.mjs) makes from temporary agent settings and skills. They check settings, missing or outdated files, and invalid input. They don't launch agents or change this repository's agent or skill files. [Version tags](docs/architecture/version-tags.md#unit-tests-replace-git-and-integration-tests-use-temporary-repositories) says what the version tag script's tests check, the [release guide](docs/architecture/releasing.md#tests-check-the-guide-without-publishing-anything) what the guided release tests check, and the [technology stack](docs/architecture/technology-stack.md#agents-check-the-desktop-before-they-start-screen-tests) what the desktop check's tests check.

### Working with agents

The main session plans, asks the user, and accepts results. Agents do the work the [process](docs/contributing/process.md) assigns them.

- **Explain what you changed and checked.** Name the concrete component or script, say what it does in plain words, and explain what its tests verify and leave unverified. Don't use labels such as "generator tests" without that context.
- **Delegate each step to its role**, as the [process](docs/contributing/process.md) table assigns them. Send `explorer` any search or reading beyond a couple of files you already know, with a specific question.
- **Never substitute a built-in agent for a role.** Copilot's `explore`, `task`, `general-purpose`, `code-review`, and `rubber-duck`, and Claude Code's `Explore`, `Plan`, and `general-purpose`, don't follow these rules. Use one only for work no role covers, such as web research. If the roles aren't offered, tell the user; a new session loads them.
- **Give every agent a self-contained brief**, not a fork of your conversation. Keep its configured model unless selecting a reviewer under [Independent review](docs/contributing/process.md#independent-review). In Codex, set `agent_type` to the role and `fork_turns` to `"none"`.
- **Agents can't ask the user.** They stop and return their questions. Ask the user one decision at a time, recommended option first, with the `ask_user` tool when it's available.
- **Make trivial edits yourself.** Review every agent's result before accepting it.

### Exploring the code

- **Map before you search.** Run `node .agents/repo-map.mjs [<dir-or-file>] [--budget <chars>]` from the repository root. It prints the file tree and each file's symbols and headings, with line numbers. Use it instead of listing directories or opening files, then read only the lines you need.
- **Keep bulky files out of searches.** List generated, vendored, and bulky reference files in the root `.ignore` (`.gitignore` syntax). Searches and the repository map skip them; read an excluded file directly when needed.
