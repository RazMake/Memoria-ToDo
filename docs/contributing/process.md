# How a change goes from request to review

Every change passes through design, tests, code, and review, each done by its own AI agent. You decide and review only the architecture; the agents write and check the rest.

The session you talk to runs these steps, asks you each decision, and delivers the report. Each agent's instructions are in [.agents/subagents/](../../.agents/subagents/).

| Step | Agent | Result |
| --- | --- | --- |
| Find what exists | [explorer](../../.agents/subagents/explorer.md) | Answers with `path:line` evidence |
| Design | [architect](../../.agents/subagents/architect.md) | Design file, behaviors to build, questions for you |
| Red | [tester](../../.agents/subagents/tester.md) | Failing unit tests and written integration tests |
| Green | [coder](../../.agents/subagents/coder.md) | Code that makes unit tests pass, inside one module |
| Review | [reviewer](../../.agents/subagents/reviewer.md) | Findings, sorted into code defects and design gaps |
| Final validation | [tester](../../.agents/subagents/tester.md) and [coder](../../.agents/subagents/coder.md) | [Failure proof and final checks](../../AGENTS.md#tests) |

## Changing the design

A design change is needed when the request needs a decision no design file makes, or it touches the [review surface](../../AGENTS.md#contributions). The design grows one decision at a time, so work can stop after any step and resume in a later session.

1. **Find the owner.** The architect picks the owner design file and module. A missing design file is created from the [template](../architecture/template.md) and added to the [design backlog](../architecture/README.md#design-backlog) as **Drafting**.
2. **Propose.** The architect writes each open question into the design file with 2–3 options, their trade-offs, and a recommendation.
3. **You decide.** The session asks you one question at a time, recommended option first.
4. **Record.** The architect writes your choice into the design file, adds the other options to Ruled out, and removes the question. It updates the [module map](../architecture/modules.md#module-map) and public interfaces when they change.
5. **Close the area** when its design file has no open questions: its backlog row becomes **Decided**.

Design files describe the current design only. Git history records how it changed.

## Changing the code

1. **List the behaviors.** The architect lists each rule the change must satisfy, from the owner design file. A rule that needs a missing decision goes through [Changing the design](#changing-the-design) first, or is decided, recorded, and flagged as [contributions](../../AGENTS.md#contributions) principle 3 allows.
2. **Red.** The tester follows the [test policy](../../AGENTS.md#tests), delivering failing unit tests and written integration tests.
3. **Green.** The coder changes the code until the selected unit tests pass, without editing them. A test that turns out wrong goes back to the tester. The coder handles production refactors needed for testability under the [test policy](../../AGENTS.md#tests).
4. **Review.** Follow [Independent review](#independent-review) to launch the reviewer and resolve every finding. The reviewer checks the change against the design file and the [module rules](../architecture/modules.md). Each finding is one of two kinds:
   - **Code defect**: the code, tests, or docs miss the design. Fix it test first, from step 2.
   - **Design gap**: the design is missing, unclear, or wrong. It goes to you as a decision; code doesn't settle it.

5. **Final validation.** After implementation and review fixes are complete, the main session calls the tester and coder for the [final checks](../../AGENTS.md#tests). Failed checks return the work to step 2.
6. **Report** in chat, in the format below.

A change that keeps behavior, such as a refactor or a move, uses the same unit tests before and after. Integration checks follow the [test policy's final-validation timing](../../AGENTS.md#tests).

A backlog row becomes **Implemented** only when every behavior in its design file is built and tested, or deferred to a named backlog row.

## Independent review

The main session owns reviewer selection and finding resolution. A reviewer cannot make its own session independent.

- **Select a different actual model.** Record the resolved model identities of every agent or session that wrote the code under review. Select a reviewer model different from all of them for this review. Verify the resolved reviewer model, not just its configured name: aliases and fallback names for the same model don't count.
- **Start a fresh session for each review.** Never reuse the author's session or an earlier reviewer session, or fork their conversations. Give the reviewer a self-contained brief with the scope, original requirements, owner design file, applicable instructions, code changes, and test evidence. Include the author and reviewer model identities and evidence of how the fresh session was created. Don't carry over the author's reasoning or earlier review conclusions.
- **Stop if independence cannot be verified.** If the tool cannot launch or confirm a fresh session with a distinct model, report the blocker and ask you how to proceed. Don't silently use the same model or claim an independent review occurred.
- **Evaluate every finding.** Track each by ID. Record either a verified fix or an evidence-backed rejection, with the reason and supporting code, specification, or test evidence. Take design gaps to you before implementing a decision. Keep unrelated pre-existing issues separate and ask you before expanding scope.
- **Re-review fixes without an arbitrary round limit.** Apply the same launch rules to each new review of the corrected change. Resolve every finding before final validation and completion; don't automatically carry leftovers into the report after two rounds. If progress stalls or a finding cannot be resolved, report the blocker and ask you rather than claiming completion.

## Delivery report

The report tells you exactly what to review, and nothing else:

- **Needs your review**: each changed file in the [review surface](../../AGENTS.md#contributions), with one line on what changed.
- **Decisions to confirm**: each decision taken without asking you, with the option taken and its alternatives.
- **Spec coverage**: each listed behavior with the tests that prove it, or "deferred" with the reason and its backlog row.
- **Evidence**: build results; unit and integration results reported separately, including deferred checks; and each review's [independence evidence and finding resolutions](#independent-review).
- **Left open**: unresolved findings, risks, and anything not verified.
