# Week 1 design note: microservices or one well-organised application?

**Status:** draft written with the chapter. Ankit to redo the arithmetic by hand and edit before the week closes.

## What we need

A reader opens a website, sees a personal feed built from many outside sources,
searches it, and reacts to items. We also need the project to exercise the
decisions an architect is interviewed on.

## Rough numbers (design target)

| Quantity | Working | Result |
|---|---|---|
| Requests per day | 1,000,000 readers x 60 requests | 60,000,000 |
| Average requests per second | 60,000,000 / 86,400 seconds | about 700 |
| Peak requests per second | 3 to 5 times the average | about 2,100 to 3,500 |
| New content items per day | 5,000 sources x 20 items | 100,000 |
| Content items per year | 100,000 x 365 | 36,500,000 |
| Content storage per year | 36,500,000 x 2 KB | about 73 GB |
| Reader actions per day | 1,000,000 readers x 30 actions | 30,000,000 |
| Reader actions per second (average) | 30,000,000 / 86,400 | about 350 |

Note: the weekly guide's table says "about 1,500 to 3,500 per second" at peak.
Three times the average is about 2,100, so the lower figure in the guide is
closer to 2 times the average. Recorded for the progress log.

## Options

1. **One application.** A single .NET process with modules. At 700 requests per
   second average, a handful of copies of one well-built application behind a
   load balancer can serve this. Cheapest and simplest.
2. **Microservices.** Separate processes per capability, each with its own data.
   Lets the collector, feed and recommender scale and fail independently, and
   lets one of them be written in Python.

## Choice and reason

Microservices, **for learning**. An honest solo startup would begin with option 1
and split only when a real pressure appears (a team that cannot work in one
codebase, one part needing very different scaling, or a different language).
We accept the extra cost because practising those splits is the purpose of the
project. Week 24 builds the cheap single-server version from the same code.

## What would change our mind

If by checkpoint 1 (after week 6) the service split is consuming most of the
weekly hours without teaching anything new, merge the workers into one process
and record the change in the progress log.
