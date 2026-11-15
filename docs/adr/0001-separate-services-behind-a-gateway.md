# ADR-0001: Separate services behind one gateway

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Magpie has two goals: to teach the design decisions expected of a software
architect, and to possibly become a real product run by one developer on a
small budget. The design target is 1,000,000 daily readers (roughly 700 requests
per second on average, 2,000 to 3,500 at peak; see the week 1 design note).

## Options considered

1. **One well-organised application (modular monolith).** One deployable unit
   with strict internal modules. Cheapest to run and simplest to operate. This
   is what a solo developer launching a real product should start with.
2. **Separate services behind a gateway.** Each service owns its data and is
   deployed on its own. Costs more to run and operate, but is the only way to
   practise service boundaries, messaging, partial failure and independent scaling.

## Decision

We build separate services behind a single gateway (YARP), starting with only
two deployable .NET processes: the gateway and the Accounts service. New
services are added one at a time, only when a week's goal needs them.

## Consequences

- Every request crosses at least one extra network hop, and "the other service
  is down" becomes a normal situation that must be handled from week 1.
- Each service must be runnable and testable on its own.
- Week 24 shows how to run the same code as one combined process for a cheap
  launch, which is only possible if services never share a database or reach
  into each other's code.
- Revisit if the split slows learning more than it teaches.
