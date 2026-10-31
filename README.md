# Memoria-ToDo

Memoria-ToDo's purpose isn't written yet. So far this repository holds the rules every change follows and the AI agents that design, build, test, and review it.

| If you want to | Read |
| --- | --- |
| Learn the rules every change follows, human or AI | [`AGENTS.md`](AGENTS.md) |
| See the design and its open decisions | [Architecture](docs/architecture/README.md) |
| Follow a change from request to review | [Process](docs/contributing/process.md) |
| Write or change documentation | [Writing docs](docs/contributing/writing-docs.md) |

## What you need

- **Node.js**, to run the AI tooling in [.agents/](.agents/): the repository map and the agent file generator.
- **An AI coding tool**: GitHub Copilot, OpenAI Codex, or Claude Code v2.1.284 or later ([required for Sonnet 5.5](https://code.claude.com/docs/en/model-config)). Each one reads [`AGENTS.md`](AGENTS.md) and the agents generated from [.agents/subagents/](.agents/subagents/).
