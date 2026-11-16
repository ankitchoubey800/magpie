# Week 5 design note: how do we add a new source in under an hour?

**Status:** draft written with the chapter. Ankit to edit after trying exercise 1 (adding a plug-in) and timing it.

## What we need

New kinds of source will keep arriving. Adding one must be quick, must not
touch scheduling or storage code, and must not be able to break existing sources.

## Rough numbers

- Design target: 5,000 sources x about 20 items a day = 100,000 items a day.
- At a 60-minute average interval that is about 83 visits a minute, or 1.4 a second.
- With 4 visits in parallel and about 1 second per visit, one collector handles
  about 240 visits a minute: roughly three times the target, on one small process.
- Today: 8 starter sources.

## The plug-in contract

A plug-in is one class implementing `IContentSource.FetchAsync(request)`.

**It must:** call its source through `HttpFetcher`; translate the answer into
`RawItem`s; report trouble as `SourceUnavailableException` (worth retrying) or
`SourceContentException` (not worth retrying).

**It must not:** touch the database; decide when it runs; retry; catch and hide
errors; keep state between calls; store more than a title, a short snippet and a link.

## Steps to add one (the checklist to time yourself against)

1. Add a value to `SourceType`.
2. Write the plug-in class in `Infrastructure/Sources/`.
3. Register it and add one line to the switch in `ContentSourceFactory`.
4. Unit-test it against `StubWeb` with a sample of the source's real answer.
5. Add a row to the seed file.

No migration, no change to the loop, the handler or the stores.

## Options rejected

- **A big `switch` on source type inside the fetch loop.** Works for two types;
  by the fifth, every change risks every source.
- **Configuration-only sources (no code).** Attractive, but every real source
  has quirks that end up needing code anyway.
- **Loading plug-ins from separate files at run time.** Real plug-in systems do
  this; it buys independence we do not need and costs versioning problems.

## What would change our mind

Hundreds of source types maintained by other people, or source types that need
very different run-time environments. Then separate deployable collectors per
type (and the Lambda collector of week 6 is a first step in that direction).
