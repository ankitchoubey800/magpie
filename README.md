# Magpie

An interest-feed platform (articles, videos and posts gathered by topic) built
week by week as a production-grade software architecture learning project.

**Status:** Week 5. Accounts (sign-up, sign-in, Google, reset, interests) and a collector that gathers content from RSS feeds and Hacker News.

## What runs today

```
Browser --> Gateway (YARP) --> Accounts service --> PostgreSQL
               |                      |
               |                      +--> Mailpit (catches emails on the laptop)
               +--> Web (React, served by nginx)
```

## Run everything (needs Docker)

```bash
cp infra/.env.example infra/.env        # then edit the password
docker compose -f infra/docker-compose.yml up --build
```

Open http://localhost:8080 for the website and http://localhost:8025 for the caught emails.

## Run the tests

```bash
dotnet test Magpie.slnx          # needs Docker running (tests start a throwaway PostgreSQL)
cd src/web && npm ci && npm test
```

## Repository map

| Path | What lives there |
|---|---|
| `src/gateway/` | The single front door (YARP reverse proxy) |
| `src/services/accounts/` | Accounts service: Api, Application, Domain, Infrastructure, Tests |
| `src/services/collector/` | Collector worker: Domain, Application, Infrastructure, Worker, Tests |
| `src/shared/Magpie.Mediator/` | The small mediator used to dispatch use cases |
| `e2e/` | Browser tests (Playwright) |
| `src/shared/Magpie.Defaults/` | Logging, error format and health checks shared by every service |
| `src/web/` | React + TypeScript website |
| `infra/` | Docker Compose for the laptop, seed data, smoke scripts and experiments |
| `docs/adr/` | Architecture decision records |
| `docs/design-notes/` | One-page weekly design notes |
