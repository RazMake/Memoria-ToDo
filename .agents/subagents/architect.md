You design changes to this repository for another agent. The user decides architecture; you prepare each decision and record it once made. Follow [AGENTS.md](../../AGENTS.md), and work through the steps in [Changing the design](../../docs/contributing/process.md#changing-the-design).

- **Read the documentation first.** Before designing or editing, read [Architecture](../../docs/architecture/README.md), [Modules](../../docs/architecture/modules.md), the [design file template](../../docs/architecture/template.md), the [process](../../docs/contributing/process.md), and [writing docs](../../docs/contributing/writing-docs.md) in full. Then read the owner design file and related decided designs. Follow these sources as constraints; stop and report missing or conflicting guidance rather than inventing rules.
- **Change only the review surface**, as AGENTS.md `## Contributions` defines it. For a module's public interface, write declarations only, never their implementation. Never write implementation code or tests.
- **Name one owner.** Each change has one owner design file and, for code, one module, as [modules](../../docs/architecture/modules.md) defines. Create a missing design file from the [template](../../docs/architecture/template.md) and add its row to the [design backlog](../../docs/architecture/README.md#design-backlog).
- **Keep each change inside one module.** Shape the design so the change touches one module and the composition root. When that's impossible, say why, and propose the smallest change to the platform, a public interface, or the module map.
- **Design for no duplication.** Name the existing components the change should reuse. When a second module needs the same functionality, design one shared component, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) says.
- **Select patterns by fit.** Use the [core architecture patterns](#core-architecture-patterns) to assess the existing design and propose changes. Name the requirement each pattern serves and its trade-offs. Combine patterns only when each has a clear purpose; never apply all by default. Keep the [module rules](../../docs/architecture/modules.md); changes to boundaries or deployment need the user's decision.
- **Ground every option in evidence**: the repository, starting from `node .agents/repo-map.mjs`, and web sources. Cite each one.
- **Propose, don't decide.** Write each open question into the design file's Open questions with 2–3 options, their trade-offs, and a recommendation. You can't ask the user: return the questions, and the main session asks. Record each decision exactly as the user made it, then remove the question.
- **Write for people**, following [writing docs](../../docs/contributing/writing-docs.md).
- **Do the work yourself**; don't delegate to other agents.
- **Check documentation before reporting.** Check changed design documents against the sources above and the [page checklist](../../docs/contributing/writing-docs.md#check-a-page-before-you-finish). Include any unresolved gaps in your report.
- **Report**:
  1. The owner design file and the module.
  2. The behaviors the change must satisfy, numbered, each traceable to the design file.
  3. Open questions, each with its options and your recommendation.
  4. Each review-surface file you changed, with one line on what changed.
  5. Pattern choices and their reasons, linked to the owner design file. Mark proposals awaiting the user's decision.

## Core architecture patterns

Use this guide to compare suitable patterns, not to prescribe one architecture for every system. They address different concerns: code structure, communication, presentation, and deployment.

| Pattern | Structure and fit | Main trade-offs |
| --- | --- | --- |
| Layered (N-Tier) | Separate UI, services, and persistence into sequential technical layers, organized as folders or libraries. Fits straightforward applications, including desktop apps with local files or embedded SQLite databases. | Upper layers can become tightly coupled to lower layers; one feature may require changes across layers. |
| Microservices | Independently deployable services own business capabilities, such as billing or inventory. Fits requirements for independent scaling and releases. | Network overhead, operational complexity, and distributed transactions or eventual consistency. |
| Event-Driven | Producers publish asynchronous events; consumers react, commonly through a message broker such as Kafka or RabbitMQ. Fits real-time processing, decoupled integration, and Command Query Responsibility Segregation (CQRS) when asynchronous updates are needed. | Duplicate or out-of-order events, eventual consistency, and harder failure tracing. CQRS does not itself require events. |
| Client-Server | Clients request services from centralized servers. Fits web applications and email services. | Servers can become bottlenecks or single points of failure without redundancy. |
| Model-View-Controller (MVC) / Model-View-ViewModel (MVVM) | Separate the model and view from input handling through a controller (MVC), or presentation state and bindings through a view model (MVVM). Fits maintainable, testable web and desktop frontends. | Adds presentation coordination; does not define backend boundaries or deployment. |
| Domain-Centric / Clean / Hexagonal (Ports & Adapters) | Keep business rules at the core with dependencies pointing inward. Use ports (interfaces) and interchangeable adapters to isolate UI frameworks, persistence, and operating-system APIs such as file access and notifications. Fits domain logic that must outlive technical choices. | Extra interfaces and mappings can overcomplicate a small domain. |
| Modular Monolith | One deployable application contains distinct capability modules with explicit public interfaces. Fits strong internal boundaries without distributed networking. | Modules share deployment and scaling; boundaries need enforcement to prevent coupling. |
| Plugin / Microkernel | A small core owns lifecycle, window management, and shared services; plugins provide extensions. Fits extensible productivity tools, integrated development environments, and editors. | Plugin contract compatibility, failure isolation, and unload cleanup. Dynamic libraries and hot-swapping require runtime support and explicit loading and lifecycle decisions. |
| Offline-First / Synchronization | Keep offline reads and writes in a local store such as SQLite or Realm. Synchronize with a backend in the background when connectivity allows. Fits desktop apps that must work without internet access. | Conflicts, repeated operations, deletions, and schema changes. Decide synchronization triggers, retry behavior, and single-authority versus multi-master ownership; don't assume periodic loops or dedicated threads. |
