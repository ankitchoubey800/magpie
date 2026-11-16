# ADR-0006: The collector is a separate worker with its own database and plug-in sources

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

Content must arrive from many outside sources of different kinds, on a
schedule, without any one source being able to harm the others or the rest of
the system. More source types are coming (YouTube and Bluesky in week 6).

## Options considered

1. **Collect inside the Accounts service.** No new service. But a slow or
   hostile outside site would then share threads, memory and a database with
   sign-in.
2. **One collector program per source type.** Maximum isolation; a lot of
   repeated scheduling, storage and safety code.
3. **One collector worker, with each source type as a plug-in** behind a single
   interface, its own database, and every safety rule (time limits, size
   limits, retries, back-off) written once around the plug-ins.

## Decision

Option 3. `IContentSource` is the plug-in contract (Strategy). Each plug-in
adapts its source's shape to `RawItem` (Adapter). `IContentSourceFactory`
chooses the plug-in for a source row (Factory) and hands it out wrapped in
`RetryingContentSource` (Decorator). The worker owns the database
`magpie_collector`; no other service reads it, and it refers to categories
only by slug text.

## Consequences

- Adding a source type is one class, one registration line and one switch line.
- A plug-in cannot forget a safety rule, because it does not implement any.
- Each source visit runs in its own scope with its own time budget; failures
  are recorded on the source's row and make that source back off.
- The collector's category hints can drift from the Accounts category list.
  Accepted for now; the tagger (week 9) is the real authority on categories.
- Collected items sit in a holding table until Kafka replaces it in week 7.
- The worker runs as a small web host only so it can serve the standard
  health endpoints.
