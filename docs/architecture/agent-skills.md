# How each AI tool finds a skill

Status: Decided

A skill's one home is `.agents/skills/<name>/SKILL.md`, as [AGENTS.md](../../AGENTS.md#ai-instructions-and-agents) places skills. Codex and GitHub Copilot read it there, and Claude Code reads a pointer to it that [generate.mjs](../../.agents/generate.mjs) writes.

## Behavior

The user asked that each skill work in Codex, Claude Code, and Copilot with no copy. So the generator gives a skill only to a tool that can't read `.agents/skills/`, as it gives each tool its agents. The generator belongs to no [module](modules.md), since it serves AI tools, not the apps. What a skill does is its own design's, such as the [`version-tag` skill's](version-tags.md#the-skill-previews-asks-you-then-runs-the-script).

### Each AI tool reads skills from its own folders

This table lists where each tool reads a project's skills:

| AI tool | Reads skills from | Finds a skill in |
| --- | --- | --- |
| OpenAI Codex | `.agents/skills/` only ([Codex skills](https://developers.openai.com/codex/skills)) | `.agents/skills/` |
| Claude Code | `.claude/skills/` only ([Claude Code skills](https://code.claude.com/docs/en/skills)) | The generated pointer |
| GitHub Copilot: its CLI, cloud agent, and VS Code | `.github/skills/`, `.claude/skills/`, and `.agents/skills/` ([about agent skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills)) | `.agents/skills/`, listed once, as below |
| VS Code's Local harness, removed by February 2027 | `.agents/skills/`, `.github/skills/`, then `.claude/skills/` ([folders](https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/chat/common/promptSyntax/config/promptFileLocations.ts)), keeping the first skill of each name ([service](https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/chat/common/promptSyntax/service/promptsServiceImpl.ts)) | `.agents/skills/` |

VS Code's Copilot, Claude, and Codex [harnesses](https://code.visualstudio.com/docs/agents/run/agent-harnesses) run those tools' own agents, so each finds a skill as its tool's row says. Not confirmed: how Copilot treats two skills with one name, which its docs don't say. In a hand check, Copilot CLI 1.0.112 listed such a skill once, from `.agents/skills/`.

### Claude Code reads each skill through a generated pointer

The user decided:

- **`generate.mjs` writes `.claude/skills/<name>/SKILL.md` for each skill**: the generated-file marker, the skill's frontmatter, and one line pointing to the source. The instructions stay only in the source.
- **It writes nothing for Codex or Copilot**: no `.github/skills/` copy, and no VS Code setting.
- **`node .agents/generate.mjs --check` reports a missing, outdated, or stale pointer.** [AGENTS.md](../../AGENTS.md#ai-instructions-and-agents) says when to rerun the generator.

Proposed, each a decision to confirm:

- **The pointer copies the source's whole frontmatter**, so Claude Code sees every field the source sets.
- **The generator removes a stale pointer**, one whose source is gone, but never a skill in `.claude/skills/` it didn't write.
- **A source with Windows line endings gives the same pointer as one with Unix line endings**, so `--check` reports only real changes.
- **`.ignore` lists `.claude/skills/`**, as it lists the generated agent folders, so searches find only the source.

### A broken skill stops the generator before it writes any file

Proposed, each a decision to confirm:

- **The generator checks every skill before it writes any file**, agent files included, so a broken skill changes nothing.
- **It rejects a skill** when its folder has no `SKILL.md`, the file has no frontmatter, `name` or `description` is missing or isn't plain text, or `name` isn't the folder's name or breaks the [Agent Skills standard's](https://agentskills.io/specification) naming rule.
- **Every frontmatter line that starts a key is a plain key, then `:`, then a space or the line's end**, and no key appears twice. The generator rejects a quoted key, a space before the `:`, a missing space after it, and a repeated key, so every YAML reader sees the same `name` and `description`.
- **`name` and `description` are each non-empty plain text on one line**, so the generator needs no YAML library. It rejects a value YAML reads as something else, or reads only in part: one that's quoted, such as `""`; a comment alone; one that starts with another [indicator](https://yaml.org/spec/1.2.2/#733-plain-style), such as `[` or `&`, holds `:` or `#` next to a space or tab, or ends with `:`; one that goes on to a later line, even past a blank or comment line; and one YAML's [core schema](https://yaml.org/spec/1.2.2/#1032-tag-resolution) reads as null, true, false, or a number, such as `null`, `~`, or `42`.

### Unit tests check the pointer text, and integration tests run the generator

Proposed, a decision to confirm: **unit tests in [generate.test.mjs](../../.agents/generate.test.mjs) check the pointer and each rejection on text in memory.** A few integration tests there run the generator in a temporary folder, to check that it writes, reports, and removes pointers. They run as [AGENTS.md](../../AGENTS.md#checking-the-agents-scripts) says.

## Public interface

None for modules; no module uses the generator. You and AI tools run `node .agents/generate.mjs`, with or without `--check`, as [AGENTS.md](../../AGENTS.md#ai-instructions-and-agents) says. Claude Code reads the pointers it writes.

## Failure modes

- **A skill breaks a rule**: the generator names the skill and the problem, and writes no file. Fix the skill, then run the generator again.
- **A pointer is out of date**, because a skill changed without a rerun: Claude Code shows the old description, or offers a removed skill, until you rerun `generate.mjs`. `node .agents/generate.mjs --check` reports it.
- **A later Copilot lists a skill twice**, from the source and the pointer: both lead to the same instructions, so either works.

## Ruled out

- **No skills in Claude Code**: you asked for each skill in all three tools.
- **A committed link from `.claude/skills/<name>` to the source**: on Windows, git checks a link out as a plain file unless links are turned on ([`core.symlinks`](https://git-scm.com/docs/git-config#Documentation/git-config.txt-coresymlinks)).
- **Keeping the source in `.claude/skills/`, or copying the whole skill there**: Codex reads only `.agents/skills/`, and a copy gives the instructions two homes.
- **A `.github/skills/` copy for Copilot**: Copilot already reads the source.
- **A VS Code setting that turns `.claude/skills/` off**: the Local harness already keeps one of the two, and the setting is deprecated ([agent skills in VS Code](https://code.visualstudio.com/docs/agent-customization/agent-skills)).
- **Only integration tests for the pointers, as the agent files have**: [AGENTS.md](../../AGENTS.md#test-design) asks unit tests to cover every rule.
