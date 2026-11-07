# Memoria-ToDo

These rules apply to every change, by a human or AI, to code, tests, configuration, scripts, documentation, or AI instructions. On the never-merged [proof branch](docs/architecture/technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided), [Code design](#code-design), [Tests](#tests), and [Contributions](#contributions) principle 4 don't apply; every other rule does. The application's purpose and design are decided, and no code exists yet; the [design backlog](docs/architecture/README.md#design-backlog) tracks them.

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

- **Test first.** Every feature and bug fix starts with tests that describe it. See unit tests fail for the expected reason before implementation; write UX and integration tests upfront but defer their execution as below. Make the tests pass without editing them. A behavior-preserving change starts and ends with the same unit tests passing, with UX and integration checks deferred.
- **Prove every new test can fail.** A test seen failing for the expected reason has this proof. Otherwise, temporarily break the code it covers, see the test fail for the expected reason, then restore it. Follow the run timing below for every failure proof.
- **Test requirement intent through the public interface.** Cover the requirement's observable outcomes, edge cases, and errors, not the implementation's structure. Assert what callers observe: results, errors, visible state, and the calls the requirement promises. Don't assert private state, log lines, or internal call order; a behavior-preserving refactor must not break the tests.
- **Let tests expose design problems.** Hard-to-test code is a signal to improve its structure, not to couple tests to its internals. Refactor production code to separate responsibilities and dependencies where needed. Don't weaken assertions, expose private state, or add test-only switches. Public-interface and module-boundary changes still follow [Contributions](#contributions).
- **Tests are a living spec.** Name each test as a sentence about one behavior, such as `rejects an empty name`. Group tests by component and scenario, so the names alone explain the component.
- **Classify every test as unit, UX, or integration.** Unit tests use no real I/O and never start an application or service. A UX test checks one screen in-process, with fake services, and never starts the app. Unit tests are by far the most numerous, then UX tests; keep integration tests far fewer, for real boundaries and wiring that other tests cannot verify. The whole unit suite, not counting the build, runs within a [time limit](docs/architecture/technology-stack.md#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue) that the release workflow checks. The [technology stack](docs/architecture/technology-stack.md#attributes-mark-each-tests-kind-in-one-test-project-per-module) says how kinds are marked and run.
- **Synchronize, never sleep.** Wait on completion signals, not delays, polling, or busy loops. Timeouts only bound failure.
- **Isolate what tests change.** Each test gets its own files and external resources. Share only immutable setup.

### Run timing

- **Batch focused unit checks.** Run related unit tests together at meaningful red/green checkpoints, not after every small edit. Reuse passing evidence while the covered code, tests, dependencies, and configuration are unchanged. Avoid full-suite reruns during development.
- **Run UX and integration tests only at session end.** Never run them during development, including through build scripts or combined test commands. The main session authorizes final validation only after implementation and review fixes are complete; finishing an agent task is not session end. At final validation, prove new UX and integration tests can fail, restore temporary breaks, then run the final build and full unit, UX, and integration suites. If checks reveal defects, return to development and defer UX and integration reruns until final validation resumes.

## Documentation

Follow [writing docs](docs/contributing/writing-docs.md): write for a person who joined last week, summary first, in plain words.

## Contributions

The user decides and reviews architecture; AI writes and reviews the implementation.

1. **Only the user commits.** AI never commits or pushes.
2. **Architecture lives only in the review surface**, the only files the user needs to read:
   - `README.md`, `docs/`, every `AGENTS.md`, `.agents/`, and `.github/workflows/`;
   - each module's and the platform's public interface;
   - files that add dependencies or set tool and runtime versions.

   Everything else is implementation, checked by tests and the `reviewer` agent.
3. **Implementation never decides architecture alone.** A decision is a choice users notice or later work must respect: a default, a limit, a timeout, or what happens on failure. When code needs a decision no design file makes:
   - **Ask first** when a different answer would waste much of the work, or is hard to undo once released. Examples: security, public interfaces, stored data formats, new dependencies, and module boundaries.
   - **Otherwise decide, record, and flag**: take the option you'd recommend and write it into the owner design file, as the [template](docs/architecture/template.md) says. List it under decisions to confirm in the delivery report.
4. **Deliver through the [process](docs/contributing/process.md)**: design, tests first, code, independent review, and a delivery report.

## AI instructions and agents

`AGENTS.md` is the only AI instruction source. GitHub Copilot, OpenAI Codex, and Claude Code (v2.1.277 or later) read it natively.

- **No tool-specific instruction files.** Don't create `CLAUDE.md`, `CLAUDE.local.md`, `.claude/CLAUDE.md`, `.github/copilot-instructions.md`, `.github/instructions/`, or `.claude/rules/`. They duplicate or split these rules, and any `CLAUDE.md` makes Claude Code stop reading `AGENTS.md`.
- **Scope rules to folders.** Put rules that apply to one part of the repository in an `AGENTS.md` in that folder. Keep every `AGENTS.md` short: each one loads into every session.
- **Agents are defined only in `.agents/subagents/`.** `subagents.json` sets each role's description, models, and reasoning effort, and `<role>.md` its instructions. After changing `subagents.json`, run `node .agents/generate.mjs` to rewrite `.claude/agents/`, `.codex/agents/`, and `.github/agents/`. Never edit those generated files; `node .agents/generate.mjs --check` reports stale ones.
- **Skills**, when one is needed, live only in `.agents/skills/<name>/SKILL.md`.

### Checking the agent generator

At [final validation](#tests), run `node --test .agents/generate.test.mjs`. These [tests](.agents/generate.test.mjs) run [generate.mjs](.agents/generate.mjs) with temporary agent settings and inspect the Claude, Codex, and Copilot files it writes. They check settings, missing or outdated files, and invalid input. They don't launch agents or change this repository's agent files.

### Working with agents

The main session plans, asks the user, and accepts results. Agents do the work the [process](docs/contributing/process.md) assigns them.

- **Explain what you changed and checked.** Name the concrete component or script, say what it does in plain words, and explain what its tests verify and leave unverified. Don't use labels such as "generator tests" without that context.
- **Delegate exploration to `explorer`**: any search or reading beyond a couple of files you already know. Ask it a specific question.
- **Give every agent a self-contained brief**, not a fork of your conversation. Keep its configured model unless selecting a reviewer under [Independent review](docs/contributing/process.md#independent-review). In Codex, set `agent_type` to the role and `fork_turns` to `"none"`.
- **Agents can't ask the user.** They stop and return their questions. Ask the user one decision at a time, recommended option first, with the `ask_user` tool when it's available.
- **Make trivial edits yourself.** Review every agent's result before accepting it.

### Exploring the code

- **Map before you search.** Run `node .agents/repo-map.mjs [<dir-or-file>] [--budget <chars>]` from the repository root. It prints the file tree and each file's symbols and headings, with line numbers. Use it instead of listing directories or opening files, then read only the lines you need.
- **Keep bulky files out of searches.** List generated, vendored, and bulky reference files in the root `.ignore` (`.gitignore` syntax). Searches and the repository map skip them; read an excluded file directly when needed.
