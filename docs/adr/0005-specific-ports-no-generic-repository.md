# ADR-0005: Specific read and write ports; no generic repository over EF Core

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

Week 4 adds the first feature with our own tables. We must decide how use
cases reach the database, and how use cases are invoked.

## Options considered

1. **A generic repository and unit of work of our own** (`IRepository<T>`,
   `IUnitOfWork`) on top of EF Core. Familiar from older tutorials. EF Core's
   `DbSet` already is a repository and `DbContext` already is a unit of work,
   so this wraps them a second time and hides useful features (projections,
   no-tracking queries, includes).
2. **Use `DbContext` directly in use cases.** Least code. The Application
   project would then depend on EF Core, breaking the inward-pointing rule.
3. **Specific ports per feature, split into a read door and a write door**
   (`IInterestQueries`, `IInterestStore`), implemented in Infrastructure with
   plain EF Core. Use cases are requests dispatched by a small mediator of our own.

## Decision

Option 3. Reads return screen-shaped records through no-tracking queries.
Writes load tracked objects and save once per use case. Commands and queries
are `IRequest<T>` records handled by `IRequestHandler<,>` classes, found and
invoked by `Magpie.Mediator` (about forty lines; no outside library).

## Consequences

- One small interface pair per feature instead of one generic abstraction.
- Read and write code can evolve separately. Week 10 takes this further with a
  separate read store (the "full" form of CQRS).
- Handlers written before week 4 (ping, registration, sign-in, reset) are
  plain classes called directly; they move to the mediator when next touched.
- If the mediator ever needs cross-cutting steps (logging, validation), they
  are added as decorators around handlers, not by adopting a framework.
