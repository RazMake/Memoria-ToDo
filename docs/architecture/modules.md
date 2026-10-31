# How code is split into modules

Code is split into modules, one per capability, so a change or a mistake stays inside one module. This page owns the module rules and the module map; the [code design rules](../../AGENTS.md#code-design) cover the components inside a module.

## A module is one capability behind a small interface

- **One capability, one design file.** A module does one thing a user would name. Its design file, under `docs/architecture/`, is its spec.
- **One folder, named after the module**, holds its code. Its tests live in a matching folder and use only its public interface.
- **A small public interface.** Other code uses the module only through its public interface. Everything else in the module is private, even where the language would allow access.
- **It owns its data.** Its stored data, messages, and screen state are its own. Other modules reach them only through its public interface.
- **It keeps its failures.** A module reports its own failures, and the rest of the application keeps working.

## The platform serves every module and knows none

- **The platform is the code outside module folders**: what every module shares, such as startup, settings, and storage access. It never names a module.
- **Code shared by two modules moves to the platform**, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) requires. Code shared only inside one module stays in that module.
- **Only the composition root names modules.** It creates each module's parts and connects them, and it's the only place that chooses concrete implementations.

## Modules depend only on what the map allows

- **A module uses the platform and the modules the [map](#module-map) lists for it**, only through their public interfaces.
- **Dependencies never form a cycle.**
- **Core rules own the interfaces they need.** Code for I/O, frameworks, and external services implements those interfaces; core rules never depend on it.

## What a change may touch

Who decides a change depends on what it touches:

| A change touches | Kind | Who decides |
| --- | --- | --- |
| One module's private code and its tests | Implementation | The agents; the reviewer checks it |
| The composition root, to register a module's parts | Implementation | The agents; the reviewer checks it |
| The platform, any module's public interface, or the module map | Architecture | You, through [Changing the design](../contributing/process.md#changing-the-design) |

## Module map

Each row is one module, its design file, and the modules it may use. Adding, removing, or changing a row is an architecture change.

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| None yet | | | |

## Open questions

- **How does a module mark its public interface?** Not decided: it depends on the technology stack ([backlog](README.md#design-backlog) rows 2 and 3). The answer must let a reviewer list every public-interface file in a change without reading the code.
