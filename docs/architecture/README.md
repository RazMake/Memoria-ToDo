# Architecture

This chapter holds Memoria-ToDo's design: what each part does, how parts depend on each other, and the decisions still open. The application's purpose isn't designed yet, so no module exists.

| If you want to | Read |
| --- | --- |
| Learn how code is split into modules, and what a change may touch | [Modules](modules.md) |
| Start a design file for a new area | [Design file template](template.md) |
| Learn how a design decision gets made | [Changing the design](../contributing/process.md#changing-the-design) |

## Design backlog

Each row is one area to design, owned by one design file. Work on the first row that isn't **Decided**; a row marked **Drafting** resumes where its design file's open questions left off.

Status is one of **Open**, **Drafting**, **Decided**, or **Implemented**, as [the process](../contributing/process.md) defines.

| # | Area | Design file | Status |
| --- | --- | --- | --- |
| 1 | Purpose and scope: what the application does, and for whom | None yet | Open |
| 2 | Technology stack, including the one command that builds the code and runs every test | None yet | Open |
| 3 | How each module marks its public interface, so a reviewer finds it fast | [Modules](modules.md) | Open |
| 4 | The first modules and the [module map](modules.md#module-map) | [Modules](modules.md) | Open |
