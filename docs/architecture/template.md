# Design file template

Copy the block below into a new design file under `docs/architecture/`. Keep every section; write "None yet" when a section is empty.

A design file records decisions and their reasons: what happens, its rules, limits, defaults, and failure handling. Write each decision as a lasting principle a user or a later design can rely on, such as "icons never touch their cell's edges". Exact values, such as pixel sizes, and mechanics, such as function names and flags, live only in code, unless another module depends on them. Write it as [writing docs](../contributing/writing-docs.md) says.

```markdown
# <Area name>

Status: Drafting | Decided | Implemented

<What this area is for, in one or two sentences.>

## Behavior

How the area works now, told as a story: what happens, its rules, and its limits. Say what it deliberately leaves to other areas, with links.

## Public interface

What other modules may use, and what each part promises. Write "None" when nothing outside the module uses it.

## Failure modes

- <Failure>: how it is detected, and what the user sees.

## Ruled out

- **<Option>**: why, in a few words.

## Open questions

Only while the area is being designed. Each question lists 2–3 options, their trade-offs, and a recommendation.
```
