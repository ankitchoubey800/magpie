# ADR-0002: Clean Architecture layers inside each .NET service

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Each service will grow for months and will have its database, message broker
and cloud provider swapped between "laptop", "cloud" and "lean launch" setups.

## Options considered

1. **One project per service, folders by technical type.** Fast to start, but
   nothing stops business rules from calling the database library directly.
2. **Vertical slices (one folder per feature).** Low ceremony, good for small
   services; boundaries rely on discipline rather than the compiler.
3. **Clean Architecture: Api, Application, Domain, Infrastructure projects.**
   More projects, but the compiler enforces that dependencies point inward.

## Decision

Option 3. `Domain` references nothing. `Application` references only `Domain`.
`Infrastructure` implements interfaces declared in `Application`. `Api` wires
everything together.

## Consequences

- A simple feature touches three or four projects. That is the price.
- Business rules and use cases are testable without a database or web server.
- Swapping PostgreSQL for a stand-in (week 24) changes only `Infrastructure`.
- Vertical-slice architecture is studied as a side lesson in week 4.
