# Week 3 design note: build login ourselves or buy it?

**Status:** draft written with the chapter. Ankit to check today's prices on the providers' own pricing pages and fill in the cost table before the week closes.

## What we need

Email and password, Google sign-in, email confirmation, password reset,
protection against guessing, and later passkeys and "sign out everywhere".

## Options

| | Build on ASP.NET Core Identity (what we did) | Managed service (Amazon Cognito, Auth0 and similar) |
|---|---|---|
| Effort to start | Two weeks of this project | Hours to days |
| Ongoing effort | We patch, monitor and answer for every flaw | The provider does; we follow their changes |
| Control over each screen and response | Complete | Limited to what the service allows |
| Where reader data lives | Our PostgreSQL | The provider's systems |
| Cost at small scale | Our time; almost no running cost | Often free up to a monthly-active-user allowance |
| Cost at large scale | Mostly flat (a database and our time) | Grows with monthly active users |
| Risk | A mistake of ours is a breach of ours | Outage, price change or lock-in at the provider |
| Extras (multi-factor, breached-password checks, bot detection) | We build each one | Mostly included |

## Rough numbers (to be filled in by Ankit from the pricing pages)

| Monthly active readers | Managed service A | Managed service B | Ours |
|---|---|---|---|
| 1,000 | | | Share of one small database |
| 1,000,000 | | | A larger database, plus engineering time |

## Choice and reason

Build, **for this project**, because understanding sessions, tokens, linking
and rate limits from the inside is the learning goal, and running cost matters
for a solo product. For a real company with paying customers and no security
team, buying is usually the responsible first choice.

## What would make us move to a managed service

A compliance requirement we cannot meet alone, enterprise customers asking for
single sign-on, a security incident, or login work crowding out the product.
The `IReaderAccounts` and `IExternalSignIn` ports are where that swap would happen.
