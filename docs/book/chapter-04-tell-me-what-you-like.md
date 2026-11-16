# Building Magpie: A Guided Journey from Empty Folder to Production-Grade System

## Chapter 4 (Week 4): Tell Me What You Like. Interests, Tables That Stay True, and Asking Questions Properly

> **What you will have at the end of this chapter:** a new reader picks topics on a welcome screen, can change them later, and finds them still there after signing out and in. Underneath, you will have designed your first tables of your own, measured what an index buys and costs on five million rows, and given the system one tidy way to run every use case.

| | |
|---|---|
| **Week** | 4 of 26 |
| **Plan step** | Step 3, Interests |
| **Time budget** | About 8 hours |
| **You need** | Chapter 3 finished and running |
| **Repository** | https://github.com/ankitchoubey800/magpie |
| **Code for this chapter** | Commit `13be5ff` on branch `week-04` (pull request #3, stacked on `week-03`). Every link in this chapter points at that exact commit |

---

### How this chapter was checked (read this first)

| What | Where it was run | Result |
|---|---|---|
| .NET: 90 tests (51 unit, 39 against a real PostgreSQL) | GitHub Actions | `Passed! - Failed: 0, Passed: 90, Skipped: 0, Total: 90` |
| Website: lint, 35 tests, build | Author's workspace and GitHub Actions | Passed |
| Browser tests: 5 journeys in real Chromium, including "topics survive signing out and in" | GitHub Actions (Playwright) | `5 passed (7.6s)` |
| The second migration, created by `dotnet ef migrations add` | GitHub Actions; generated files committed | Created; applied by the tests and by the Compose migration job |
| The index experiment on 5,000,000 rows | GitHub Actions, PostgreSQL 18 on a shared runner | Real figures in section 6 |
| **Not yet run:** anything on your own laptop, including your own index figures | | You do this. Your numbers will differ from the runner's; the *shape* should not |

The full evidence is public: https://github.com/ankitchoubey800/magpie/actions/runs/43810317286

Two things broke on the way and are described where they happened: a Docker build that did not know about a new project (Step 9), and an experiment whose first result contradicted its own heading (section 6). The second one is the better lesson.

---

## 1. The big picture

### What are we building this week?

Until now Magpie knows *who* you are. This week it learns *what you care about*, which is the whole reason the product exists.

```mermaid
flowchart LR
    S["Sign in<br/>(Weeks 2-3)"] --> Q{"Has this reader<br/>chosen topics?"}
    Q -->|"no"| O["Onboarding:<br/>pick at least 3"]
    Q -->|"yes"| H["Home"]
    O -->|"saved"| H
    H --> ST["Settings:<br/>change topics"]
    ST -->|"saved"| H
```

**Reading the diagram:** a first-time reader is steered to the welcome screen exactly once. After that the choice lives in the database and follows them to any browser.

The feature is small on purpose. It is the smallest real feature that needs everything a business application needs: tables designed by us, rules that must hold, reads and writes with different shapes, and a screen that stays in step with the server. So this is the week we learn to do those things *properly*, while the feature is still simple enough to see through.

### Why does it matter?

Two reasons, one for the product and one for you.

For the product: every later week reads this data. The collector (Week 5) tags content with these categories. The feed (Week 10) is "newest items in my topics". Notifications (Week 15) need "who follows topic X?".

For you: "what is an index and what does it cost?", "how do you organise data access?" and "what problem does separating reads from writes solve?" are asked in almost every backend interview. After this week you answer with numbers you measured.

> 🧠 **Pause and think**
> A reader follows 5 topics. You could store that as one row per reader with a list (`["Programming","Space",...]`) or as five small rows, one per (reader, topic) pair. Which makes "rename *Space* to *Astronomy*" a one-row change? Which makes "who follows Space?" answerable without reading every reader? (Answer in section 11.)

### This week's deliverables

1. Two tables, `categories` and `user_interests`, created by the second migration, with the starter list of 30 categories.
2. Three addresses: list categories, read my interests, replace my interests.
3. A small **mediator** of our own, and the first commands and queries that use it.
4. Separate **read** and **write** doors to the data.
5. An onboarding page and a settings page, using **TanStack Query** for server data.
6. An **index experiment** with recorded numbers.

---

## 2. Concepts first

### 2.1 Normalised tables: say each thing once

A library keeps one card per book. A loan slip does not copy out the title and author; it says "book #4417, borrowed by member #212". If the book's title is corrected, one card changes and every loan slip is automatically right.

That is **normalisation**: each fact is stored in exactly one place, and other tables point to it by **id**.

```mermaid
erDiagram
    users ||--o{ user_interests : "chooses"
    categories ||--o{ user_interests : "is chosen in"
    categories ||--o{ categories : "group has topics"
    categories {
        int Id PK "chosen by us, never reused"
        string Slug "unique, address-friendly"
        string Name
        int ParentId FK "empty for a group"
        int SortOrder
    }
    user_interests {
        uuid ReaderId PK,FK
        int CategoryId PK,FK
        float Weight "1 = picked by hand"
        timestamptz AddedAtUtc
    }
```

**Reading the diagram:** `categories` points at itself: a topic's `ParentId` is its group. `user_interests` is a **join table**: it holds no facts of its own except "this reader, this topic". Its key is the **pair** of ids.

Three promises the database itself now keeps, with no code of ours:

| Promise | How |
|---|---|
| A reader cannot have the same topic twice | The pair `(ReaderId, CategoryId)` is the **primary key** |
| A choice cannot point at a topic that does not exist | **Foreign key** to `categories` |
| Deleting a reader removes their choices; deleting a topic people follow is refused | **Cascade** on one foreign key, **restrict** on the other |

Rules enforced by the database cannot be skipped by a buggy service, a script, or a future developer in a hurry.

### 2.2 Indexes: the book's index, with a price

To find every page mentioning "Kafka" in a book you can read all 400 pages, or look in the index at the back. A database **index** is the same: a sorted side-list that says where rows with a given value live.

A table has two natural questions here:

- "What does **reader X** follow?" The primary key `(ReaderId, CategoryId)` is itself an index sorted by reader first. Fast for free.
- "Who follows **topic X**?" The primary key is no help: it is sorted by reader, like a phone book when you only know the first name. This needs its **own** index on `CategoryId`.

And the price, which tutorials skip:

| An index costs... | Why |
|---|---|
| Disk space | It is a second copy of the indexed columns, sorted |
| Time on **every** insert, update and delete | The side-list must be kept in order |
| Thought | An index nobody uses is pure cost |

So the rule is: **index for the questions you actually ask, and measure.** Section 6 does exactly that.

### 2.3 Reading a query plan

Before running a query, PostgreSQL decides *how* (its **plan**). `EXPLAIN ANALYZE` runs the query and shows the plan with real timings. Four words are enough to read one:

| You see | It means |
|---|---|
| `Seq Scan` | Read every row of the table ("read the whole book") |
| `Index Scan` / `Bitmap Heap Scan` | Use an index to find where matching rows are, then fetch those rows from the table |
| `Index Only Scan` | The index alone contains everything asked for; the table is not touched |
| `Buffers: ... read=N` | How many 8-kilobyte pages were read. Often more telling than the time |

### 2.4 Repository and unit of work (and why we do not write them)

Two classic patterns:

- A **repository** makes stored objects feel like an in-memory collection: "give me", "add", "remove".
- A **unit of work** remembers everything you changed during one task and saves it together, all or nothing.

Older tutorials have you write `IRepository<T>` and `IUnitOfWork` classes. Here is the lesson of the week in one sentence: **EF Core already is both.** A `DbSet<T>` is a repository. The `DbContext` tracks your changes and `SaveChangesAsync()` writes them in one **transaction** (all or nothing). Wrapping them again in generic classes of your own adds a layer that only hides useful features.

What we write instead are **small, specific ports**: an interface that names exactly the questions and changes one feature needs.

### 2.5 CQRS in its light form

**CQRS** stands for Command Query Responsibility Segregation. Behind the long name is a plain observation: *reading for a screen* and *changing data* want different things.

| | Query (read) | Command (write) |
|---|---|---|
| Purpose | Fill a screen | Change something, safely |
| Shape of the answer | Whatever the screen needs, ready-made | Almost nothing: "done" or "no, because..." |
| Uses domain rules? | No | Yes |
| EF Core mode | No tracking (faster) | Tracking (so changes can be saved) |
| May change data? | **Never** | Yes |

A restaurant analogy: the menu (reading) and the kitchen order slip (changing) are different documents for different jobs, even though both are about the same food.

"Light" means: same database, two doors. The "full" form, with a separately stored read copy, arrives in Week 10 when the feed needs it.

### 2.6 A mediator

We now have a growing pile of use cases. Endpoints currently name each handler class and `Program.cs` registers each one by hand. A **mediator** is a switchboard: the endpoint hands over a small **request** object ("set these interests for this reader") and the mediator finds the one **handler** that knows how.

```mermaid
flowchart LR
    E["Endpoint"] -->|"SendAsync(request)"| M["Mediator"]
    M -->|"finds by request type"| H1["SetMyInterestsHandler"]
    M -.-> H2["GetCategoriesHandler"]
    M -.-> H3["GetMyInterestsHandler"]
```

**Reading the diagram:** the endpoint depends on one thing, the mediator, however many use cases exist. Each request type has exactly one handler.

What it buys: endpoints stop knowing handler classes; new handlers are discovered automatically; every use case has the same shape (a request record in, a result out), which later makes it easy to wrap all of them with the same extra step.

What it costs: one more hop to follow when reading the code ("who handles this?"). That is why it should stay small. Ours is about forty lines. The popular libraries moved to commercial licences in 2025, and we need a fraction of what they offer.

### 2.7 "Who owns this?": authorization by construction

So far the only rule was "must be signed in". Interests are the first data that belongs to *a particular* reader. The classic bug is an address like `/readers/42/interests` where the server checks that you are signed in but not that you are reader 42. Change the number, read someone else's data.

Our addresses are `/api/accounts/me/interests`. There is **no id in the address**. The reader id comes from the session cookie and nowhere else. One reader cannot even *ask* for another's interests. The safest permission check is the one you never have to write.

### 2.8 Server state versus screen state, and TanStack Query

A React app holds two very different kinds of state:

| | Screen state | Server state |
|---|---|---|
| Example | Which boxes are ticked right now | Which topics are saved |
| Who owns the truth? | This component | The server; we hold a **copy** |
| Can it be out of date? | No | Always, potentially |

Our hand-written hooks from Weeks 1 and 2 treated server data like screen state. That works for one call. It does not answer: two components need the same data (fetch twice?), when is our copy stale, what happens after we save?

**TanStack Query** is a cache for server state. You describe *what* to fetch under a **key**; it handles sharing, loading and error states, retrying, and refreshing. Changes go through a **mutation**, after which you tell the cache what is now true.

### 2.9 Side lesson: vertical slices

We organise by layer: a feature's files are spread over Domain, Application, Infrastructure and Api. **Vertical-slice architecture** organises by feature instead: one folder, `Interests/`, holding the endpoint, the handler, the data access and the tests.

| | Layers (ours) | Vertical slices |
|---|---|---|
| A new feature touches | Four projects | One folder |
| Boundaries enforced by | The compiler (project references) | Discipline and code review |
| Best when | Rules are rich and infrastructure will be swapped (our Weeks 20-24) | Many simple, independent features |

Notice we are already halfway: inside each layer, files are grouped by feature (`Application/Interests/`, `web/features/interests/`). Many teams combine the two in exactly this way.

---

## 3. Architecture and decisions

### 3.1 What changes

```mermaid
flowchart TB
    B["Browser<br/>TanStack Query cache"] --> G["gateway"]
    G --> A["accounts"]
    subgraph A2["inside accounts"]
      EP["InterestEndpoints"] --> MED["Mediator"]
      MED --> QH["Query handlers"] --> RD["IInterestQueries<br/>(read door, no tracking)"]
      MED --> CH["Command handler"] --> DOM["InterestSelection<br/>(Domain rule)"]
      CH --> WR["IInterestStore<br/>(write door, one transaction)"]
    end
    A --- A2
    RD --> P[("postgres<br/>categories, user_interests")]
    WR --> P
```

**Reading the diagram:** no new containers. Inside Accounts, requests now pass through the mediator and then split: queries go straight to the read door; the command passes through a Domain rule first and then the write door.

### 3.2 Saving interests, step by step

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser (InterestPicker)
    participant E as Endpoint
    participant M as Mediator
    participant H as SetMyInterestsHandler
    participant D as InterestSelection (Domain)
    participant S as InterestStore
    participant P as PostgreSQL
    B->>E: PUT /api/accounts/me/interests {categoryIds:[101,201,401]} + X-XSRF-TOKEN
    Note over E: reader id comes from the cookie, not the request
    E->>M: SendAsync(SetMyInterestsCommand)
    M->>H: HandleAsync(command)
    H->>S: which topic ids exist?
    S->>P: SELECT ids of topics
    H->>D: TryCreate(chosen, selectable)
    D-->>H: valid selection (or the reason it is not)
    H->>S: ReplaceAsync(reader, selection)
    S->>P: one transaction: DELETE dropped rows, INSERT new rows
    H-->>E: Done
    E-->>B: 204 No Content
    Note over B: mutation succeeded: write the new list into the cache
```

**Reading the diagram:** the rule ("3 to 15 real topics, no repeats") is checked in the Domain, before the store is asked to change anything. The store's change is one transaction: a reader never ends up with half of the old set and half of the new.

### 3.3 Decisions

| # | Decision | Why | Recorded in |
|---|---|---|---|
| 1 | Normalised tables with a join table | Small, exact, asked about from both directions | Design note |
| 2 | Category ids chosen by hand (group × 100 + n), never reused | Readers' choices refer to them; they must be stable across environments | This chapter |
| 3 | The starter list is part of the migration | Every environment gets the same list, in the same change as the tables | This chapter |
| 4 | Explicit index on `CategoryId` | The "who follows topic X?" question; measured in section 6 | This chapter |
| 5 | Specific read and write ports; no generic repository | EF Core already is one | ADR-0005 |
| 6 | Our own small mediator, in a shared project | One shape for every use case; no licence | ADR-0005 |
| 7 | Earlier handlers are left as they are | Rewriting working, tested code for uniformity is churn. They move when next touched | ADR-0005 |
| 8 | `PUT` replaces the whole set | Sending it twice gives the same result, so retries are harmless (**idempotent**) | This chapter |
| 9 | No reader id in the address | Ownership by construction | This chapter |
| 10 | Between 3 and 15 topics | A product decision, encoded as a Domain rule so it has one home | This chapter; yours to change |
| 11 | TanStack Query for server data; the cache is emptied on sign-in and sign-out | Shared cache for server state; one reader must never see another's cached data | This chapter |

### 3.4 Files changed from earlier weeks

| File | Change |
|---|---|
| `Magpie.slnx`, both Dockerfiles | New shared project `Magpie.Mediator` |
| `Magpie.Defaults` | New `MediatorRegistration.cs`; references the mediator |
| `Accounts.Application.csproj` | References the mediator |
| `Infrastructure/Persistence/AccountsDbContext.cs`, `DependencyInjection.cs` | Two tables; two ports registered |
| `Api/Program.cs` | Registers the mediator; maps the new endpoints |
| `Tests/Integration/ApiSession.cs` | `PutAsync`, `SignUpAndSignInAsync` |
| `web`: `apiClient.ts`, `main.tsx`, `AuthProvider.tsx`, `HomePage.tsx`, `App.tsx`, `index.css`, `test/fakeServer.ts`, the two auth test files | `apiPut`; query client; cache clearing; onboarding redirect; routes; shared test renderer |
| `e2e/tests/auth.spec.ts`, `.github/workflows/ci.yml` | Onboarding step; new journey; the experiment |

---

## 4. Implementation guide

Branch: `git switch week-03 && git switch -c week-04`.

### Step 1: The mediator

**What:** a new shared project with five small interfaces and one class, plus a registration helper.
**Why:** one tidy way to invoke every use case from now on.

```bash
dotnet new classlib -n Magpie.Mediator -o src/shared/Magpie.Mediator
rm src/shared/Magpie.Mediator/Class1.cs
dotnet sln add src/shared/Magpie.Mediator
dotnet add src/services/accounts/Magpie.Accounts.Application reference src/shared/Magpie.Mediator
dotnet add src/shared/Magpie.Defaults reference src/shared/Magpie.Mediator
```

The vocabulary:

**`src/shared/Magpie.Mediator/Contracts.cs`**

```csharp
namespace Magpie.Mediator;

/// <summary>
/// A request is a small object that says WHAT the caller wants. It carries the
/// input and names the type of the answer (TResult). It contains no logic.
/// </summary>
public interface IRequest<TResult>;

/// <summary>A request that CHANGES something. By convention it returns very little.</summary>
public interface ICommand<TResult> : IRequest<TResult>;

/// <summary>A request that only READS. It must never change anything.</summary>
public interface IQuery<TResult> : IRequest<TResult>;

/// <summary>The one class that knows how to carry out one kind of request.</summary>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The switchboard. Callers hand it a request; it finds the matching handler
/// and returns the handler's answer. Callers never name a handler class.
/// </summary>
public interface IMediator
{
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}
```

The switchboard:

**`src/shared/Magpie.Mediator/Mediator.cs`**

```csharp
using System.Collections.Concurrent;

namespace Magpie.Mediator;

/// <summary>
/// About forty lines, on purpose. Popular mediator libraries add many features
/// (and, since 2025, commercial licences). We need exactly one: "find the
/// handler for this request and call it".
/// </summary>
public sealed class Mediator(IServiceProvider services) : IMediator
{
    // Working out "which handler type fits this request type" needs reflection,
    // which is slow. We do it once per request type and remember the answer.
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (Invoker<TResult>)Invokers.GetOrAdd(
            request.GetType(),
            static (requestType, resultType) =>
                Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(requestType, resultType))!,
            typeof(TResult));

        return invoker.InvokeAsync(services, request, cancellationToken);
    }

    private abstract class Invoker<TResult>
    {
        public abstract Task<TResult> InvokeAsync(
            IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken);
    }

    private sealed class Invoker<TRequest, TResult> : Invoker<TResult>
        where TRequest : IRequest<TResult>
    {
        public override Task<TResult> InvokeAsync(
            IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken)
        {
            // Ask the container for the handler. If nobody registered one, say so clearly.
            var handler = (IRequestHandler<TRequest, TResult>?)services.GetService(typeof(IRequestHandler<TRequest, TResult>))
                ?? throw new InvalidOperationException(
                    $"No handler is registered for {typeof(TRequest).Name}. " +
                    "Did you pass its assembly to AddMagpieMediator(...)?");

            return handler.HandleAsync((TRequest)request, cancellationToken);
        }
    }
}
```

How to read `SendAsync`: the request's type (say `SetMyInterestsCommand`) decides which handler interface to look for. Working that out needs **reflection** (code inspecting types at run time), which is slow, so it is done once per request type and remembered in a dictionary. After the first call it is a dictionary lookup and a method call.

The registration helper lives in `Magpie.Defaults`, where the other "how we wire services" code is:

**`src/shared/Magpie.Defaults/MediatorRegistration.cs`**

```csharp
using System.Reflection;
using Magpie.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Defaults;

public static class MediatorRegistration
{
    /// <summary>
    /// Registers the mediator and every request handler found in the given
    /// assemblies, so adding a new handler class needs no extra wiring line.
    /// </summary>
    public static IServiceCollection AddMagpieMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Magpie.Mediator.Mediator>();

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            // A class may implement IRequestHandler<,> for one or more requests.
            foreach (var handlerInterface in type.GetInterfaces().Where(IsRequestHandler))
            {
                services.AddScoped(handlerInterface, type);
            }
        }

        return services;
    }

    private static bool IsRequestHandler(Type candidate) =>
        candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
}
```

> **GitHub (verified at commit `13be5ff`):** [`Contracts.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/shared/Magpie.Mediator/Contracts.cs) · [`Mediator.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/shared/Magpie.Mediator/Mediator.cs) · [`Magpie.Mediator.csproj`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/shared/Magpie.Mediator/Magpie.Mediator.csproj) · [`MediatorRegistration.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/shared/Magpie.Defaults/MediatorRegistration.cs)

**Check:** `dotnet build src/shared/Magpie.Defaults` succeeds. Open `Magpie.Mediator.csproj`: no references at all.

> ❓ **Q&A**
> **Q: Does the Application layer referencing `Magpie.Mediator` break "Application references only Domain"?**
> A: The rule's purpose is to keep frameworks and infrastructure out. `Magpie.Mediator` is five interfaces with no dependencies of its own, and it is ours. The conventions table in section 13 now states the rule precisely: Domain, plus our own dependency-free shared abstractions.
> **Q: `ICommand` and `IQuery` are empty. What are they for?**
> A: For people, today: the type name says whether a request may change data. Later they let us treat the two differently, for example wrapping only commands in an extra step.
> **Q: Why is a missing handler an exception rather than a result?**
> A: It is a programming mistake (you forgot to register an assembly), not something a reader can cause. Mistakes of ours should fail loudly, with a message that says how to fix it. A test checks the message.

### Step 2: Domain, categories and the selection rule

A category:

**`src/services/accounts/Magpie.Accounts.Domain/Category.cs`**

```csharp
namespace Magpie.Accounts.Domain;

/// <summary>
/// One entry in the two-level interest tree. A category with no parent is a
/// GROUP ("Technology"); a category with a parent is a TOPIC ("Programming").
/// Readers choose topics, never groups.
/// </summary>
public sealed class Category
{
    public int Id { get; private set; }

    /// <summary>A short, stable, address-friendly name, for example "ai-machine-learning".</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    public int? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsTopic => ParentId is not null;

    public Category(int id, string slug, string name, int? parentId, int sortOrder)
    {
        Id = id;
        Slug = slug;
        Name = name;
        ParentId = parentId;
        SortOrder = sortOrder;
    }
}
```

The starter list. **This is your product decision to review**; the list below is a reasonable first draft, not a fact.

**`src/services/accounts/Magpie.Accounts.Domain/StarterCategories.cs`**

```csharp
namespace Magpie.Accounts.Domain;

/// <summary>
/// The first category list: 6 groups with 4 topics each (30 entries).
/// This is a PRODUCT decision, not a technical one: too many choices
/// overwhelm a new reader, too few make every feed look the same.
///
/// The ids are written out by hand and must never be reused or renumbered,
/// because readers' choices refer to them. Topics are numbered group x 100 + n.
/// </summary>
public static class StarterCategories
{
    public static readonly IReadOnlyList<Category> All =
    [
        new(1, "technology", "Technology", null, 1),
        new(101, "programming", "Programming", 1, 1),
        new(102, "ai-machine-learning", "AI & Machine Learning", 1, 2),
        new(103, "cloud-devops", "Cloud & DevOps", 1, 3),
        new(104, "security", "Security", 1, 4),

        new(2, "science", "Science", null, 2),
        new(201, "space", "Space", 2, 1),
        new(202, "physics", "Physics", 2, 2),
        new(203, "biology-medicine", "Biology & Medicine", 2, 3),
        new(204, "climate-environment", "Climate & Environment", 2, 4),

        new(3, "business", "Business", null, 3),
        new(301, "startups", "Startups", 3, 1),
        new(302, "finance-markets", "Finance & Markets", 3, 2),
        new(303, "careers", "Careers", 3, 3),
        new(304, "product-design", "Product & Design", 3, 4),

        new(4, "culture", "Culture", null, 4),
        new(401, "books", "Books", 4, 1),
        new(402, "film-tv", "Film & TV", 4, 2),
        new(403, "music", "Music", 4, 3),
        new(404, "gaming", "Gaming", 4, 4),

        new(5, "lifestyle", "Lifestyle", null, 5),
        new(501, "food-cooking", "Food & Cooking", 5, 1),
        new(502, "health-fitness", "Health & Fitness", 5, 2),
        new(503, "travel", "Travel", 5, 3),
        new(504, "personal-finance", "Personal Finance", 5, 4),

        new(6, "world", "World", null, 6),
        new(601, "world-news", "World News", 6, 1),
        new(602, "india", "India", 6, 2),
        new(603, "sport", "Sport", 6, 3),
        new(604, "education", "Education", 6, 4),
    ];
}
```

The rule, as a value object in the same style as `EmailAddress`:

**`src/services/accounts/Magpie.Accounts.Domain/InterestSelection.cs`**

```csharp
namespace Magpie.Accounts.Domain;

/// <summary>
/// A reader's chosen topics, already checked against the rules. Like
/// EmailAddress, once you hold an InterestSelection you know it is valid.
/// </summary>
public sealed class InterestSelection
{
    /// <summary>Fewer than this and the feed has nothing to work with.</summary>
    public const int Minimum = 3;

    /// <summary>More than this and "interested in everything" means interested in nothing.</summary>
    public const int Maximum = 15;

    public IReadOnlySet<int> CategoryIds { get; }

    private InterestSelection(IReadOnlySet<int> categoryIds) => CategoryIds = categoryIds;

    /// <param name="chosen">What the reader sent. May contain repeats or nonsense.</param>
    /// <param name="selectable">The ids of all TOPICS that exist right now.</param>
    public static bool TryCreate(
        IEnumerable<int>? chosen,
        IReadOnlySet<int> selectable,
        out InterestSelection? selection,
        out string? error)
    {
        selection = null;
        var distinct = (chosen ?? []).ToHashSet(); // choosing the same topic twice counts once

        if (distinct.Count < Minimum)
        {
            error = $"Choose at least {Minimum} topics.";
            return false;
        }

        if (distinct.Count > Maximum)
        {
            error = $"Choose at most {Maximum} topics.";
            return false;
        }

        if (!distinct.IsSubsetOf(selectable))
        {
            error = "One or more of the chosen topics do not exist.";
            return false;
        }

        selection = new InterestSelection(distinct);
        error = null;
        return true;
    }
}
```

One stored choice:

**`src/services/accounts/Magpie.Accounts.Domain/ReaderInterest.cs`**

```csharp
namespace Magpie.Accounts.Domain;

/// <summary>
/// One row: "this reader is interested in this topic". Weight is 1 for a topic
/// the reader picked by hand; the recommender (weeks 13 and 14) will later
/// raise or lower it as it learns what they actually read.
/// </summary>
public sealed class ReaderInterest
{
    public Guid ReaderId { get; private set; }

    public int CategoryId { get; private set; }

    public double Weight { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public ReaderInterest(Guid readerId, int categoryId, double weight, DateTimeOffset addedAtUtc)
    {
        ReaderId = readerId;
        CategoryId = categoryId;
        Weight = weight;
        AddedAtUtc = addedAtUtc;
    }
}
```

> **GitHub (verified at commit `13be5ff`):** [`Category.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Domain/Category.cs) · [`StarterCategories.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Domain/StarterCategories.cs) · [`InterestSelection.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Domain/InterestSelection.cs) · [`ReaderInterest.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Domain/ReaderInterest.cs)

**Check:** the Domain project builds and still references nothing.

> ❓ **Q&A**
> **Q: Why does `TryCreate` turn the input into a set before counting?**
> A: So nobody reaches "at least 3" by sending `[101, 101, 101]`. There is a test named for exactly that.
> **Q: Why hand-written ids like 101 instead of letting the database count 1, 2, 3?**
> A: Counted ids depend on the order rows were inserted, which can differ between your laptop, CI and production. These ids are *meaning* that other data refers to, so we fix them ourselves. The numbering also makes a topic's group readable at a glance.
> **Q: What is `Weight` for?**
> A: Nothing yet; every hand-picked topic gets 1. It is the one column added for a known future need: the recommender (Weeks 13-14) will adjust it. Adding a column now is free; adding it later to millions of rows is a migration to plan.

### Step 3: Application, two doors, two queries and a command

The two ports and the screen shapes:

**`src/services/accounts/Magpie.Accounts.Application/Interests/InterestPorts.cs`**

```csharp
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Interests;

// Two ports instead of one, on purpose. This is the "light" form of CQRS
// (Command Query Responsibility Segregation): the code that READS for a screen
// and the code that CHANGES data have different needs, so they get different doors.

/// <summary>
/// The READ door. Returns ready-made shapes for screens. Implementations must
/// never change data, and need none of the machinery that tracks changes.
/// </summary>
public interface IInterestQueries
{
    Task<IReadOnlyList<CategoryGroup>> GetCategoryTreeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetReaderCategoryIdsAsync(Guid readerId, CancellationToken cancellationToken);
}

/// <summary>
/// The WRITE door. Works with domain objects and saves all changes of one
/// use case together, or none of them.
/// </summary>
public interface IInterestStore
{
    Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken);

    /// <summary>Makes the reader's stored interests equal to the selection: adds the new, removes the dropped, keeps the rest.</summary>
    Task ReplaceAsync(Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

// ---- Shapes made for the screen (never the domain objects themselves) ----

public sealed record CategoryGroup(int Id, string Name, IReadOnlyList<CategoryTopic> Topics);

public sealed record CategoryTopic(int Id, string Slug, string Name);

public sealed record MyInterests(IReadOnlyList<int> CategoryIds);
```

Each use case is one file holding the request and its handler together:

**`src/services/accounts/Magpie.Accounts.Application/Interests/GetCategories.cs`**

```csharp
using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Query: "show me the tree of groups and topics".</summary>
public sealed record GetCategoriesQuery : IQuery<IReadOnlyList<CategoryGroup>>;

public sealed class GetCategoriesHandler(IInterestQueries queries)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryGroup>>
{
    public Task<IReadOnlyList<CategoryGroup>> HandleAsync(GetCategoriesQuery request, CancellationToken cancellationToken) =>
        queries.GetCategoryTreeAsync(cancellationToken);
}
```

**`src/services/accounts/Magpie.Accounts.Application/Interests/GetMyInterests.cs`**

```csharp
using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Query: "which topics has this reader chosen?"</summary>
public sealed record GetMyInterestsQuery(Guid ReaderId) : IQuery<MyInterests>;

public sealed class GetMyInterestsHandler(IInterestQueries queries)
    : IRequestHandler<GetMyInterestsQuery, MyInterests>
{
    public async Task<MyInterests> HandleAsync(GetMyInterestsQuery request, CancellationToken cancellationToken) =>
        new(await queries.GetReaderCategoryIdsAsync(request.ReaderId, cancellationToken));
}
```

**`src/services/accounts/Magpie.Accounts.Application/Interests/SetMyInterests.cs`**

```csharp
using Magpie.Accounts.Domain;
using Magpie.Mediator;

namespace Magpie.Accounts.Application.Interests;

/// <summary>Command: "from now on, these are this reader's topics".</summary>
public sealed record SetMyInterestsCommand(Guid ReaderId, IReadOnlyList<int>? CategoryIds) : ICommand<SetMyInterestsResult>;

/// <summary>A command returns little: did it work, and if not, why.</summary>
public sealed record SetMyInterestsResult(bool Saved, string? Error)
{
    public static readonly SetMyInterestsResult Done = new(true, null);

    public static SetMyInterestsResult Invalid(string error) => new(false, error);
}

public sealed class SetMyInterestsHandler(IInterestStore store, TimeProvider timeProvider)
    : IRequestHandler<SetMyInterestsCommand, SetMyInterestsResult>
{
    public async Task<SetMyInterestsResult> HandleAsync(SetMyInterestsCommand request, CancellationToken cancellationToken)
    {
        var selectable = await store.GetSelectableCategoryIdsAsync(cancellationToken);

        // The rule itself lives in the Domain. The handler only coordinates.
        if (!InterestSelection.TryCreate(request.CategoryIds, selectable, out var selection, out var error))
        {
            return SetMyInterestsResult.Invalid(error!);
        }

        await store.ReplaceAsync(request.ReaderId, selection!, timeProvider.GetUtcNow(), cancellationToken);
        return SetMyInterestsResult.Done;
    }
}
```

> **GitHub (verified at commit `13be5ff`):** [`InterestPorts.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Application/Interests/InterestPorts.cs) · [`GetCategories.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Application/Interests/GetCategories.cs) · [`GetMyInterests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Application/Interests/GetMyInterests.cs) · [`SetMyInterests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Application/Interests/SetMyInterests.cs)

**Check:** `dotnet build src/services/accounts/Magpie.Accounts.Application` succeeds.

> ❓ **Q&A**
> **Q: The two query handlers do nothing but call the port. Isn't that the "ceremony" we avoided with sign-out in Week 2?**
> A: A fair challenge. The difference is that these are now part of a uniform system: the endpoint talks only to the mediator, and every use case, however thin, has the same shape and is found the same way. When a query later needs a rule (say, hiding topics behind a paid plan in Week 16), the handler is already there to hold it. Thin is fine; *inconsistent* is what costs.
> **Q: Why does the command carry `ReaderId` instead of the handler looking up "the current user"?**
> A: So the handler has no idea HTTP exists and can be tested by passing any id. The endpoint is the only place that knows where the id comes from (the cookie).

### Step 4: Infrastructure, tables and the two doors

The DbContext gains two tables (whole file shown). Every line in the two `Entity<...>` blocks is one of the promises from 2.1:

**`src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/AccountsDbContext.cs`**

```csharp
using Magpie.Accounts.Domain;
using Magpie.Accounts.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// EF Core's view of the Accounts database. A DbContext is one short "work
/// session" with the database: it tracks the objects you load and writes your
/// changes back in one go.
///
/// IdentityUserContext brings the table definitions for users (and their
/// tokens, claims and outside logins) without the roles tables, which we do
/// not use.
/// </summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ReaderInterest> ReaderInterests => Set<ReaderInterest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Friendlier table names than the "AspNetUsers" defaults.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<Category>(category =>
        {
            category.ToTable("categories");
            category.HasKey(c => c.Id);
            category.Property(c => c.Id).ValueGeneratedNever(); // we choose the ids ourselves
            category.Property(c => c.Slug).HasMaxLength(64);
            category.Property(c => c.Name).HasMaxLength(64);
            category.HasIndex(c => c.Slug).IsUnique();

            // A topic points at its group. Restrict: a group that still has topics cannot be deleted.
            category.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

            // The starter list travels with the schema: it is part of the migration.
            category.HasData(StarterCategories.All.Select(c => new
            {
                c.Id, c.Slug, c.Name, c.ParentId, c.SortOrder,
            }));
        });

        builder.Entity<ReaderInterest>(interest =>
        {
            interest.ToTable("user_interests");

            // One row per (reader, topic) pair. Making the PAIR the key means the
            // database itself refuses to store the same choice twice.
            interest.HasKey(i => new { i.ReaderId, i.CategoryId });

            // Deleting a reader deletes their choices with them.
            interest.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.ReaderId).OnDelete(DeleteBehavior.Cascade);

            // A topic that readers have chosen cannot be deleted by accident.
            interest.HasOne<Category>().WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);

            // The key above already makes "all topics of reader X" fast.
            // THIS index makes the opposite question fast: "all readers of topic X".
            interest.HasIndex(i => i.CategoryId).HasDatabaseName("ix_user_interests_category");
        });
    }
}
```

The read door:

**`src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/InterestQueries.cs`**

```csharp
using Magpie.Accounts.Application.Interests;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// The READ door. Every query uses AsNoTracking(): EF Core then skips the
/// bookkeeping it needs for saving changes, because nothing here is ever saved.
/// </summary>
internal sealed class InterestQueries(AccountsDbContext database) : IInterestQueries
{
    public async Task<IReadOnlyList<CategoryGroup>> GetCategoryTreeAsync(CancellationToken cancellationToken)
    {
        // Thirty rows: load them in one query and shape the tree in memory.
        var all = await database.Categories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);

        return all
            .Where(category => !category.IsTopic)
            .Select(group => new CategoryGroup(
                group.Id,
                group.Name,
                all.Where(topic => topic.ParentId == group.Id)
                    .Select(topic => new CategoryTopic(topic.Id, topic.Slug, topic.Name))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetReaderCategoryIdsAsync(Guid readerId, CancellationToken cancellationToken) =>
        await database.ReaderInterests
            .AsNoTracking()
            .Where(interest => interest.ReaderId == readerId)
            .OrderBy(interest => interest.CategoryId)
            .Select(interest => interest.CategoryId) // ask the database for ONE column, not whole rows
            .ToListAsync(cancellationToken);
}
```

The write door. Read its opening comment; it is ADR-0005 in six lines:

**`src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/InterestStore.cs`**

```csharp
using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// The WRITE door. Notice what is NOT here: no "generic repository" class and
/// no "unit of work" class of our own. EF Core already is both: a DbSet is a
/// repository, and the DbContext saves all tracked changes in one transaction
/// when SaveChangesAsync is called. Wrapping them again would only hide them.
/// </summary>
internal sealed class InterestStore(AccountsDbContext database) : IInterestStore
{
    public async Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken) =>
        (await database.Categories
            .AsNoTracking()
            .Where(category => category.ParentId != null)
            .Select(category => category.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task ReplaceAsync(
        Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Tracked this time: we are about to change these rows.
        var existing = await database.ReaderInterests
            .Where(interest => interest.ReaderId == readerId)
            .ToListAsync(cancellationToken);

        // Remove what was dropped. Keep what stayed (and with it the original "added" date).
        database.ReaderInterests.RemoveRange(
            existing.Where(interest => !selection.CategoryIds.Contains(interest.CategoryId)));

        // Add what is new.
        var alreadyThere = existing.Select(interest => interest.CategoryId).ToHashSet();
        database.ReaderInterests.AddRange(selection.CategoryIds
            .Where(categoryId => !alreadyThere.Contains(categoryId))
            .Select(categoryId => new ReaderInterest(readerId, categoryId, weight: 1.0, nowUtc)));

        // One call, one transaction: all the removals and additions happen, or none do.
        await database.SaveChangesAsync(cancellationToken);
    }
}
```

Register both in `DependencyInjection.cs` (whole file linked below):

```csharp
        services.AddScoped<IInterestQueries, InterestQueries>();
        services.AddScoped<IInterestStore, InterestStore>();
```

> **GitHub (verified at commit `13be5ff`):** [`AccountsDbContext.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/AccountsDbContext.cs) · [`InterestQueries.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/InterestQueries.cs) · [`InterestStore.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/InterestStore.cs) · [`DependencyInjection.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/DependencyInjection.cs)

> ❓ **Q&A**
> **Q: `ReplaceAsync` removes some rows and adds others. What if the service crashes halfway?**
> A: It cannot be halfway. Nothing is sent to the database until `SaveChangesAsync`, and that one call wraps all the deletes and inserts in a single transaction: all happen, or none do. That is the unit of work, provided by EF Core.
> **Q: Why keep the rows that did not change instead of deleting everything and inserting the new set?**
> A: Delete-all-insert-all is simpler but destroys `AddedAtUtc` and, later, the learned `Weight` of topics the reader kept. Comparing the sets costs four lines and preserves history.
> **Q: Why does the category tree load all rows and build the tree in C#?**
> A: There are thirty. One trivial query and a loop is clearer than a clever SQL statement. If there were thirty thousand, the answer would change. Design for the numbers you have.

### Step 5: Api, three addresses

A helper that is the single source of the current reader's id:

**`src/services/accounts/Magpie.Accounts.Api/Security/ClaimsPrincipalExtensions.cs`**

```csharp
using System.Security.Claims;

namespace Magpie.Accounts.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in reader's id, read from the session cookie. This is the ONLY
    /// place a reader id may come from when an endpoint acts "on my own data".
    /// </summary>
    public static Guid ReaderId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("This endpoint requires a signed-in reader."));
}
```

The endpoints. They know the mediator and nothing else:

**`src/services/accounts/Magpie.Accounts.Api/Endpoints/InterestEndpoints.cs`**

```csharp
using System.Security.Claims;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.Interests;
using Magpie.Mediator;

namespace Magpie.Accounts.Api.Endpoints;

public sealed record SetInterestsRequest(IReadOnlyList<int>? CategoryIds);

public static class InterestEndpoints
{
    public static IEndpointRouteBuilder MapInterestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").AddEndpointFilter<AntiforgeryFilter>();

        // The list of topics is the same for everyone and is not a secret.
        group.MapGet("/categories", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.SendAsync(new GetCategoriesQuery(), cancellationToken)));

        // "MY interests". There is deliberately no reader id in the address.
        // The id comes from the session cookie, so one reader cannot even ASK
        // for another reader's interests. The safest permission check is the
        // one you never need to write.
        group.MapGet("/me/interests", async (ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.SendAsync(new GetMyInterestsQuery(user.ReaderId()), cancellationToken)))
            .RequireAuthorization();

        // PUT = "replace the whole set with this one". Sending the same request
        // twice leaves the same result, which makes retries harmless.
        group.MapPut("/me/interests", async (
                SetInterestsRequest request, ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.SendAsync(
                    new SetMyInterestsCommand(user.ReaderId(), request.CategoryIds), cancellationToken);

                return result.Saved
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["categoryIds"] = [result.Error!] });
            })
            .RequireAuthorization();

        return app;
    }
}
```

`Program.cs` gains two lines (whole file shown):

**`src/services/accounts/Magpie.Accounts.Api/Program.cs`**

```csharp
using Magpie.Accounts.Api.Endpoints;
using Magpie.Accounts.Api.ErrorHandling;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Application.PasswordReset;
using Magpie.Accounts.Application.Ping;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Application.SignIn;
using Magpie.Accounts.Infrastructure;
using Magpie.Accounts.Infrastructure.Persistence;
using Magpie.Defaults;

// This file is the "composition root": the one place where the pieces of the
// Accounts service are wired together. Nothing else in the service uses "new"
// to create a service or knows which implementation it receives.

var builder = WebApplication.CreateBuilder(args);

// --- 1. Register services (what exists) ------------------------------------

// Specific error handlers go BEFORE the defaults: handlers run in the order
// they are registered, and the defaults add the catch-everything handler.
builder.Services.AddExceptionHandler<DependencyUnavailableExceptionHandler>();

// Logging, Problem Details and health checks, the same in every Magpie service.
builder.AddMagpieDefaults(GetPingHandler.ServiceName);

// This service sits behind the gateway: believe its X-Forwarded-... headers.
builder.TrustGatewayForwardedHeaders();

// Infrastructure: PostgreSQL, EF Core, Identity, email.
builder.Services.AddAccountsInfrastructure();

// Cookie sign-in, anti-forgery and the keys that protect them.
builder.AddAccountsSecurity();

// Application: use cases. TimeProvider.System is the real clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GetPingHandler>();
builder.Services.AddScoped<RegisterReaderHandler>();
builder.Services.AddScoped<ResendConfirmationHandler>();
builder.Services.AddScoped<ConfirmEmailHandler>();
builder.Services.AddScoped<SignInHandler>();
builder.Services.AddScoped<RequestPasswordResetHandler>();
builder.Services.AddScoped<ResetPasswordHandler>();

// From week 4, use cases are dispatched by the mediator. This one line finds and
// registers every handler in the Application project.
builder.Services.AddMagpieMediator(typeof(GetCategoriesQuery).Assembly);

var app = builder.Build();

// --- 2. One-off jobs ---------------------------------------------------------

// "dotnet Magpie.Accounts.Api.dll --migrate" updates the database tables and
// exits. It never runs as part of normal start-up: with several copies of the
// service starting at once, they would all try to change the tables together.
if (args.Contains("--migrate"))
{
    await app.Services.MigrateAccountsDatabaseAsync();
    return;
}

// --- 3. Build the request pipeline (what happens to each request) ----------
// Order matters: each line wraps everything below it.

app.UseForwardedHeaders(); // learn the reader's real address from the gateway
app.UseMagpieDefaults();   // errors become Problem Details
app.UseRateLimiter();      // too many attempts from one address? stop here
app.UseAuthentication();   // read the session cookie: who is this?
app.UseAuthorization();    // is that person allowed to call this endpoint?

app.MapMagpieHealthEndpoints();
app.MapPingEndpoints();
app.MapAuthEndpoints();
app.MapExternalSignInEndpoints();
app.MapInterestEndpoints();

app.Run();
```

| Address | Who | Success | Failure |
|---|---|---|---|
| `GET /api/accounts/categories` | Anyone | 200, the tree | |
| `GET /api/accounts/me/interests` | Signed in | 200 `{categoryIds:[...]}` | 401 |
| `PUT /api/accounts/me/interests` | Signed in | 204 | 400 with a message; 401 |

> **GitHub (verified at commit `13be5ff`):** [`ClaimsPrincipalExtensions.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Api/Security/ClaimsPrincipalExtensions.cs) · [`InterestEndpoints.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Api/Endpoints/InterestEndpoints.cs) · [`Program.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Api/Program.cs)

> ❓ **Q&A**
> **Q: Does the anti-forgery filter cover `PUT`?**
> A: Yes. It skips only `GET`, `HEAD` and `OPTIONS`; everything else needs the token. A test confirms a `PUT` without it gets the `antiforgery` error.
> **Q: Why `PUT` and not `POST`?**
> A: `PUT` means "make it exactly this". Repeat it and the result is the same. `POST` means "do this" and might do it twice. Choosing the verb that matches the behaviour tells every client, proxy and future developer that a retry is safe.
> **Q: The gateway did not change again?**
> A: Correct. Everything is still under `/api/accounts/`. A gateway that needs editing for every feature would be doing too much.

### Step 6: The second migration

```bash
dotnet ef migrations add AddInterests \
  --project src/services/accounts/Magpie.Accounts.Infrastructure \
  --startup-project src/services/accounts/Magpie.Accounts.Api \
  --output-dir Persistence/Migrations
```

Expected last line (as printed on the GitHub runner): `Done. To undo this action, use 'ef migrations remove'`

Read the generated `Up` method. In the repository's copy it contains, in order: `CreateTable` for `categories`, `CreateTable` for `user_interests`, one `InsertData` with the thirty starter rows, and three `CreateIndex` calls (the unique slug, the parent link, and `ix_user_interests_category`).

This is the first migration applied to a database that **already has data** (your users). It only adds things, so existing rows are untouched. That will not always be true; the Q&A below says what to watch for.

> **GitHub (verified at commit `13be5ff`):** [`20261116051854_AddInterests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/Migrations/20261116051854_AddInterests.cs) · [`AccountsDbContextModelSnapshot.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Infrastructure/Persistence/Migrations/AccountsDbContextModelSnapshot.cs)

**Check:** `docker compose -f infra/docker-compose.yml up --build` and then:

```bash
docker compose -f infra/docker-compose.yml exec postgres psql -U magpie -d magpie_accounts \
  -c 'SELECT "Id", "Name", "ParentId" FROM categories ORDER BY "Id" LIMIT 6;'
```

Expected: `Technology` with an empty `ParentId`, then groups 2 to 6. Your existing users are still there.

> ❓ **Q&A**
> **Q: How do you change a table safely while the application is live?** (This week's interview question.)
> A: In small steps that old and new code can both live with. *Adding* a table or a nullable column is safe. *Renaming or removing* a column is not, because the old code is still running during the rollout. The safe recipe is "expand, then contract": add the new column; release code that writes both; copy old data across; release code that reads the new one; only then remove the old column, in a later migration. Never one big step.
> **Q: Can I edit a migration after generating it?**
> A: Before it has been applied anywhere but your laptop, yes (or remove and regenerate). After it has been applied to a shared database, never: write a new migration instead. The history must be the same everywhere.
> **Q: How would I undo this one?**
> A: Its `Down` method drops the two tables. Note what that means once readers have chosen topics: undoing is data loss. "Can it be rolled back?" must be asked before applying, not after.

### Step 7: Tests for the service

A fake write door:

**`src/services/accounts/Magpie.Accounts.Tests/Fakes/FakeInterestStore.cs`**

```csharp
using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Fakes;

/// <summary>An in-memory stand-in for the write door.</summary>
public sealed class FakeInterestStore : IInterestStore
{
    public HashSet<int> Selectable { get; } = [101, 102, 103, 104, 201, 202];

    public Dictionary<Guid, IReadOnlySet<int>> Saved { get; } = [];

    public Task<IReadOnlySet<int>> GetSelectableCategoryIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(Selectable);

    public Task ReplaceAsync(Guid readerId, InterestSelection selection, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Saved[readerId] = selection.CategoryIds;
        return Task.CompletedTask;
    }
}
```

Unit tests for the rule, the mediator and the command:

**`src/services/accounts/Magpie.Accounts.Tests/Unit/InterestSelectionTests.cs`**

```csharp
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Unit;

public class InterestSelectionTests
{
    private static readonly IReadOnlySet<int> Topics = Enumerable.Range(101, 20).ToHashSet();

    [Fact]
    public void Three_real_topics_are_accepted()
    {
        var accepted = InterestSelection.TryCreate([101, 102, 103], Topics, out var selection, out var error);

        Assert.True(accepted);
        Assert.Null(error);
        Assert.Equal(3, selection!.CategoryIds.Count);
    }

    [Fact]
    public void Choosing_the_same_topic_twice_counts_once()
    {
        var accepted = InterestSelection.TryCreate([101, 101, 102, 102, 103], Topics, out var selection, out _);

        Assert.True(accepted);
        Assert.Equal(3, selection!.CategoryIds.Count);
    }

    [Fact]
    public void Repeats_cannot_be_used_to_reach_the_minimum()
    {
        var accepted = InterestSelection.TryCreate([101, 101, 101], Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("at least 3", error);
    }

    [Fact]
    public void Nothing_at_all_is_rejected()
    {
        Assert.False(InterestSelection.TryCreate(null, Topics, out _, out var error));
        Assert.Contains("at least 3", error);
    }

    [Fact]
    public void More_than_the_maximum_is_rejected()
    {
        var tooMany = Enumerable.Range(101, InterestSelection.Maximum + 1);

        var accepted = InterestSelection.TryCreate(tooMany, Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("at most 15", error);
    }

    [Fact]
    public void An_id_that_is_not_a_topic_is_rejected()
    {
        // 1 could be a GROUP id, or simply made up. Either way it is not selectable.
        var accepted = InterestSelection.TryCreate([101, 102, 1], Topics, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("do not exist", error);
    }
}
```

**`src/services/accounts/Magpie.Accounts.Tests/Unit/MediatorTests.cs`**

```csharp
using Magpie.Defaults;
using Magpie.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Accounts.Tests.Unit;

public class MediatorTests
{
    // A tiny request and handler that exist only for these tests.
    public sealed record AddNumbers(int Left, int Right) : IQuery<int>;

    public sealed class AddNumbersHandler : IRequestHandler<AddNumbers, int>
    {
        public Task<int> HandleAsync(AddNumbers request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Left + request.Right);
    }

    public sealed record NobodyHandlesThis : ICommand<string>;

    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddMagpieMediator(typeof(MediatorTests).Assembly);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task A_request_reaches_its_handler_and_the_answer_comes_back()
    {
        var answer = await BuildMediator().SendAsync(new AddNumbers(2, 3));

        Assert.Equal(5, answer);
    }

    [Fact]
    public async Task The_same_kind_of_request_can_be_sent_many_times()
    {
        var mediator = BuildMediator();

        Assert.Equal(3, await mediator.SendAsync(new AddNumbers(1, 2)));
        Assert.Equal(30, await mediator.SendAsync(new AddNumbers(10, 20)));
    }

    [Fact]
    public async Task A_request_with_no_handler_fails_with_a_message_that_names_it()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuildMediator().SendAsync(new NobodyHandlesThis()));

        Assert.Contains(nameof(NobodyHandlesThis), error.Message);
    }
}
```

**`src/services/accounts/Magpie.Accounts.Tests/Unit/SetMyInterestsHandlerTests.cs`**

```csharp
using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests.Unit;

public class SetMyInterestsHandlerTests
{
    private readonly FakeInterestStore _store = new();
    private readonly Guid _reader = Guid.NewGuid();

    private Task<SetMyInterestsResult> SendAsync(params int[] categoryIds) =>
        new SetMyInterestsHandler(_store, TimeProvider.System)
            .HandleAsync(new SetMyInterestsCommand(_reader, categoryIds), CancellationToken.None);

    [Fact]
    public async Task A_valid_choice_is_saved_for_that_reader()
    {
        var result = await SendAsync(101, 102, 103);

        Assert.True(result.Saved);
        Assert.Equal([101, 102, 103], _store.Saved[_reader].Order().ToArray());
    }

    [Fact]
    public async Task An_invalid_choice_is_reported_and_nothing_is_saved()
    {
        var result = await SendAsync(101, 102);

        Assert.False(result.Saved);
        Assert.Contains("at least 3", result.Error);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task A_topic_that_does_not_exist_is_refused()
    {
        var result = await SendAsync(101, 102, 999);

        Assert.False(result.Saved);
        Assert.Empty(_store.Saved);
    }
}
```

`ApiSession` gains `PutAsync` and a one-line way to get a signed-in reader (whole file shown):

**`src/services/accounts/Magpie.Accounts.Tests/Integration/ApiSession.cs`**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Magpie.Accounts.Tests.Fakes;

namespace Magpie.Accounts.Tests.Integration;

/// <summary>
/// One pretend browser session. It does what the React API client does:
/// fetch an anti-forgery token, then send it with every POST.
/// </summary>
public sealed partial class ApiSession(HttpClient client)
{
    private string? _antiforgeryToken;

    public HttpClient Client => client;

    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null)
    {
        _antiforgeryToken ??= await FetchAntiforgeryTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("X-XSRF-TOKEN", _antiforgeryToken);
        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PutAsync(string path, object body)
    {
        _antiforgeryToken ??= await FetchAntiforgeryTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", _antiforgeryToken);
        return await client.SendAsync(request);
    }

    /// <summary>Registers, confirms and signs in: a ready-to-use signed-in reader.</summary>
    public async Task SignUpAndSignInAsync(CapturingEmailSender emails, string email, string password)
    {
        await RegisterAndConfirmAsync(emails, email, password);
        (await PostAsync("/api/accounts/sign-in", new { email, password })).EnsureSuccessStatusCode();
        ForgetAntiforgeryToken();
    }

    /// <summary>Call after sign-in or sign-out: the token is tied to who is signed in.</summary>
    public void ForgetAntiforgeryToken() => _antiforgeryToken = null;

    private async Task<string> FetchAntiforgeryTokenAsync()
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/accounts/antiforgery-token");
        return response.GetProperty("token").GetString()!;
    }

    /// <summary>Registers, reads the confirmation email, confirms. Returns nothing: the reader can now sign in.</summary>
    public async Task RegisterAndConfirmAsync(CapturingEmailSender emails, string email, string password)
    {
        (await PostAsync("/api/accounts/register", new { email, password })).EnsureSuccessStatusCode();

        var (readerId, token) = ReadConfirmationLink(emails.SentTo(email)[^1].TextBody);
        (await PostAsync("/api/accounts/confirm-email", new { readerId, token })).EnsureSuccessStatusCode();
    }

    public static (string ReaderId, string Token) ReadConfirmationLink(string emailBody) =>
        ReadLink(emailBody, "confirm-email");

    public static (string ReaderId, string Token) ReadResetLink(string emailBody) =>
        ReadLink(emailBody, "reset-password");

    private static (string ReaderId, string Token) ReadLink(string emailBody, string page)
    {
        var match = AccountLink().Match(emailBody);
        if (!match.Success || match.Groups["page"].Value != page)
        {
            throw new InvalidOperationException($"No {page} link found in: {emailBody}");
        }

        return (Uri.UnescapeDataString(match.Groups["id"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"/(?<page>confirm-email|reset-password)\?readerId=(?<id>[^&\s]+)&token=(?<token>\S+)")]
    private static partial Regex AccountLink();
}
```

Integration tests against the real tables:

**`src/services/accounts/Magpie.Accounts.Tests/Integration/InterestsTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Magpie.Accounts.Application.Interests;

namespace Magpie.Accounts.Tests.Integration;

[Collection(AccountsApiCollection.Name)]
public class InterestsTests(AccountsApiFactory factory)
{
    private const string Password = "correct horse battery";

    private static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private async Task<ApiSession> NewSignedInReaderAsync(string? email = null)
    {
        var session = new ApiSession(factory.CreateBrowserLikeClient());
        await session.SignUpAndSignInAsync(factory.Emails, email ?? NewEmail(), Password);
        return session;
    }

    private static async Task<int[]> MyInterestsAsync(ApiSession session) =>
        [.. (await session.Client.GetFromJsonAsync<MyInterests>("/api/accounts/me/interests"))!.CategoryIds];

    private static async Task AssertInterestsAsync(ApiSession session, params int[] expected) =>
        Assert.Equal(expected, await MyInterestsAsync(session));

    [Fact]
    public async Task The_category_tree_has_six_groups_of_four_topics_from_the_migration()
    {
        using var client = factory.CreateBrowserLikeClient();

        var groups = await client.GetFromJsonAsync<List<CategoryGroup>>("/api/accounts/categories");

        Assert.Equal(6, groups!.Count);
        Assert.All(groups, group => Assert.Equal(4, group.Topics.Count));
        Assert.Equal("Technology", groups[0].Name);
        Assert.Equal("programming", groups[0].Topics[0].Slug);
    }

    [Fact]
    public async Task A_new_reader_has_no_interests_yet()
    {
        var session = await NewSignedInReaderAsync();

        Assert.Empty(await MyInterestsAsync(session));
    }

    [Fact]
    public async Task Saved_interests_come_back_and_survive_signing_out_and_in_again()
    {
        var email = NewEmail();
        var session = await NewSignedInReaderAsync(email);

        var save = await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 103, 101, 204 } });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        await AssertInterestsAsync(session, 101, 103, 204);

        await session.PostAsync("/api/accounts/sign-out");
        var later = new ApiSession(factory.CreateBrowserLikeClient());
        await later.PostAsync("/api/accounts/sign-in", new { email, password = Password });

        await AssertInterestsAsync(later, 101, 103, 204);
    }

    [Fact]
    public async Task Saving_again_replaces_the_whole_set()
    {
        var session = await NewSignedInReaderAsync();
        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 103, 401, 402, 403 } });

        await AssertInterestsAsync(session, 103, 401, 402, 403);
    }

    [Fact]
    public async Task Sending_the_same_choice_twice_is_harmless()
    {
        var session = await NewSignedInReaderAsync();
        var body = new { categoryIds = new[] { 101, 102, 103 } };

        var first = await session.PutAsync("/api/accounts/me/interests", body);
        var second = await session.PutAsync("/api/accounts/me/interests", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        await AssertInterestsAsync(session, 101, 102, 103);
    }

    [Theory]
    [InlineData(new[] { 101, 102 }, "at least 3")]          // too few
    [InlineData(new[] { 101, 102, 999 }, "do not exist")]   // made-up topic
    [InlineData(new[] { 101, 102, 1 }, "do not exist")]     // 1 is a GROUP, not a topic
    public async Task An_invalid_choice_is_refused_and_changes_nothing(int[] categoryIds, string expectedMessage)
    {
        var session = await NewSignedInReaderAsync();
        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 201, 202, 203 } });

        var response = await session.PutAsync("/api/accounts/me/interests", new { categoryIds });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(expectedMessage, problem.GetProperty("errors").GetProperty("categoryIds")[0].GetString());
        await AssertInterestsAsync(session, 201, 202, 203);
    }

    [Fact]
    public async Task One_readers_choices_are_invisible_to_another()
    {
        var asha = await NewSignedInReaderAsync();
        var ben = await NewSignedInReaderAsync();

        await asha.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Empty(await MyInterestsAsync(ben));
    }

    [Fact]
    public async Task Interests_need_a_signed_in_reader()
    {
        var anonymous = new ApiSession(factory.CreateBrowserLikeClient());

        var read = await anonymous.Client.GetAsync("/api/accounts/me/interests");
        var write = await anonymous.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task Changing_interests_needs_the_antiforgery_token_like_every_other_change()
    {
        var session = await NewSignedInReaderAsync();

        var response = await session.Client.PutAsJsonAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("antiforgery", problem.GetProperty("code").GetString());
    }
}
```

```
Passed!  - Failed:     0, Passed:    90, Skipped:     0, Total:    90, Duration: 19 s - Magpie.Accounts.Tests.dll (net10.0)
```

> **GitHub (verified at commit `13be5ff`):** [`FakeInterestStore.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Fakes/FakeInterestStore.cs) · [`InterestSelectionTests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Unit/InterestSelectionTests.cs) · [`MediatorTests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Unit/MediatorTests.cs) · [`SetMyInterestsHandlerTests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Unit/SetMyInterestsHandlerTests.cs) · [`ApiSession.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Integration/ApiSession.cs) · [`InterestsTests.cs`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Magpie.Accounts.Tests/Integration/InterestsTests.cs)

**Check:** 90 passed on your machine.

> ❓ **Q&A**
> **Q: The rule is tested in `InterestSelectionTests` *and* in `InterestsTests`. Duplication?**
> A: Different questions. The unit tests ask "is the rule right?" in milliseconds, with every edge case. The integration test asks "is the rule actually *wired in*, and does a refusal leave the stored data unchanged?". One of each kind is the pyramid at work.
> **Q: A test was written as `Assert.Equal([101, 103, 204], await MyInterestsAsync(session))` and the compiler refused it.**
> A: It did, in CI, with `error CS4007: Instance of type 'System.ReadOnlySpan<int>' cannot be preserved across 'await'`. The `[...]` list was being turned into a special fast type that cannot live across an `await`. The fix was to do the `await` on its own line first (see the small `AssertInterestsAsync` helper). The compiler found it, not a test; that is the cheapest place a mistake can be found.

### Step 8: The web app

```bash
cd src/web
npm install @tanstack/react-query
mkdir -p src/features/interests
```

The API client learns `PUT` (whole file shown; `apiPost` and `apiPut` now share one `send` function):

**`src/web/src/shared/api/apiClient.ts`**

```ts
// The ONLY place in the web app that calls fetch().
// Every request goes to a relative "/api/..." address, so the browser always
// talks to the site it was loaded from (the gateway). No backend address is
// ever written into the React code.
//
// Because the address is same-site, the browser attaches the session cookie
// to every call by itself. This file never sees, stores or sends a password
// or login token: that is the whole point of cookie sign-in.

/** The standard error shape (RFC 9457 "Problem Details") every Magpie service returns. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Added by Magpie.Defaults: which service produced the error. */
  service?: string
  /** Added by Magpie.Defaults: the id to search for in the logs. */
  traceId?: string
  /** A short machine-readable reason, when the server provides one. */
  code?: string
  /** Validation messages, grouped by field or topic. */
  errors?: Record<string, string[]>
}

/** One error type for every failed call, so screens handle failures the same way. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

/** Turns whatever was thrown into an ApiError, so screens only ever handle one type. */
export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError(0, { title: 'Unexpected error', detail: String(error) })
}

const API_BASE = '/api'
const ANTIFORGERY_HEADER = 'X-XSRF-TOKEN'
const ANTIFORGERY_PROBLEM_CODE = 'antiforgery'

// ---------------------------------------------------------------------------
// Reading
// ---------------------------------------------------------------------------

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, undefined, signal)
}

// ---------------------------------------------------------------------------
// Changing things (needs the anti-forgery token)
// ---------------------------------------------------------------------------

// The token is kept in memory only. It is not a secret that signs anyone in;
// it only proves "this request was made by a page from our own site".
let antiforgeryToken: string | null = null

/**
 * Forget the token. Call this whenever the signed-in reader changes
 * (sign-in, sign-out), because a token is tied to who was signed in when it
 * was issued.
 */
export function resetAntiforgeryToken(): void {
  antiforgeryToken = null
}

async function getAntiforgeryToken(): Promise<string> {
  if (antiforgeryToken === null) {
    const response = await apiGet<{ token: string }>('/accounts/antiforgery-token')
    antiforgeryToken = response.token
  }
  return antiforgeryToken
}

export function apiPost<T = void>(path: string, body?: unknown): Promise<T> {
  return send<T>('POST', path, body)
}

/** PUT means "replace the whole thing with this". Sending it twice gives the same result as once. */
export function apiPut<T = void>(path: string, body: unknown): Promise<T> {
  return send<T>('PUT', path, body)
}

async function send<T>(method: 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
  try {
    return await request<T>(method, path, body, undefined, await getAntiforgeryToken())
  } catch (error) {
    // The token can go stale (for example, the reader signed in from another
    // tab). When the server says so, fetch a fresh one and try exactly once more.
    if (error instanceof ApiError && error.problem.code === ANTIFORGERY_PROBLEM_CODE) {
      resetAntiforgeryToken()
      return await request<T>(method, path, body, undefined, await getAntiforgeryToken())
    }
    throw error
  }
}

// ---------------------------------------------------------------------------
// Shared plumbing
// ---------------------------------------------------------------------------

async function request<T>(
  method: 'GET' | 'POST' | 'PUT',
  path: string,
  body?: unknown,
  signal?: AbortSignal,
  antiforgery?: string,
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (antiforgery !== undefined) {
    headers[ANTIFORGERY_HEADER] = antiforgery
  }

  let response: Response
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // The caller cancelled the request on purpose: let that pass through untouched.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    // fetch() itself failed: no network, or nothing is listening at the address.
    throw new ApiError(0, {
      title: 'Cannot reach Magpie',
      detail: 'Check that the gateway is running, then try again.',
    })
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  // 202 Accepted and 204 No Content carry no body: there is nothing to parse.
  const text = await response.text()
  return (text === '' ? undefined : JSON.parse(text)) as T
}

// A failed response SHOULD carry Problem Details, but we never assume it does
// (a proxy in between may answer with an empty body or an HTML page).
async function readProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('application/problem+json')) {
    try {
      return (await response.json()) as ProblemDetails
    } catch {
      // Body was not valid JSON after all: fall through to the generic message.
    }
  }
  return {
    status: response.status,
    title: 'Something went wrong',
    detail: `The server answered with status ${response.status}.`,
  }
}
```

The cache, created by a small function so tests can make a fresh one each:

**`src/web/src/app/queryClient.ts`**

```ts
import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../shared/api/apiClient.ts'

/**
 * The query client is the cache for everything we fetch from the server.
 * One per browser tab; a fresh one per test.
 */
export function createQueryClient(options: { forTests?: boolean } = {}): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // For 30 seconds a fetched answer counts as fresh: moving between
        // pages re-uses it instead of asking the server again.
        staleTime: 30_000,
        // Retry a failed fetch twice, but never when the server gave a clear
        // "no" (4xx): asking again will not change a 401 or a 404.
        retry: options.forTests
          ? false
          : (failureCount, error) =>
              failureCount < 2 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
      },
    },
  })
}
```

**`src/web/src/main.tsx`**

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import './index.css'
import App from './app/App.tsx'
import { createQueryClient } from './app/queryClient.ts'
import { AuthProvider } from './features/auth/AuthProvider.tsx'

const queryClient = createQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
```

**An easy-to-miss privacy bug, fixed before it happens.** The cache outlives sign-out unless told otherwise. On a shared computer the next reader could briefly see the previous reader's topics. `AuthProvider` now empties the cache whenever the signed-in reader changes (whole file shown):

**`src/web/src/features/auth/AuthProvider.tsx`**

```tsx
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { resetAntiforgeryToken } from '../../shared/api/apiClient.ts'
import * as authApi from './api.ts'
import { AuthContext, type AuthContextValue, type AuthState } from './AuthContext.ts'

/**
 * The ONE place that knows who is signed in. It never stores a password or a
 * token. It simply asks the server "who am I?"; the browser attaches the
 * session cookie to that question by itself.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })
  const queryClient = useQueryClient()

  // On first load: is there already a valid session cookie?
  useEffect(() => {
    const controller = new AbortController()

    authApi
      .fetchCurrentReader(controller.signal)
      .then((reader) => setState(reader ? { status: 'signedIn', reader } : { status: 'signedOut' }))
      .catch(() => {
        if (!controller.signal.aborted) {
          // The server could not be reached. Treat the visitor as signed out;
          // the sign-in page will show the real error if they try.
          setState({ status: 'signedOut' })
        }
      })

    return () => controller.abort()
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    await authApi.signIn(email, password)
    // The signed-in reader changed, so the old anti-forgery token is no longer valid.
    resetAntiforgeryToken()
    // A different person may now be using this tab: forget everything fetched before.
    queryClient.clear()
    const reader = await authApi.fetchCurrentReader()
    setState(reader ? { status: 'signedIn', reader } : { status: 'signedOut' })
  }, [queryClient])

  const signOut = useCallback(async () => {
    await authApi.signOut()
    resetAntiforgeryToken()
    // Without this, the NEXT reader to sign in on this computer could briefly
    // see the previous reader's data from the cache.
    queryClient.clear()
    setState({ status: 'signedOut' })
  }, [queryClient])

  const value = useMemo<AuthContextValue>(() => ({ state, signIn, signOut }), [state, signIn, signOut])

  return <AuthContext value={value}>{children}</AuthContext>
}
```

The feature. Server calls:

**`src/web/src/features/interests/api.ts`**

```ts
import { apiGet, apiPut } from '../../shared/api/apiClient.ts'

/** Mirrors CategoryGroup and CategoryTopic in Magpie.Accounts.Application. */
export interface CategoryTopic {
  id: number
  slug: string
  name: string
}

export interface CategoryGroup {
  id: number
  name: string
  topics: CategoryTopic[]
}

export function fetchCategories(signal?: AbortSignal): Promise<CategoryGroup[]> {
  return apiGet<CategoryGroup[]>('/accounts/categories', signal)
}

export async function fetchMyInterests(signal?: AbortSignal): Promise<number[]> {
  const response = await apiGet<{ categoryIds: number[] }>('/accounts/me/interests', signal)
  return response.categoryIds
}

export function saveMyInterests(categoryIds: number[]): Promise<void> {
  return apiPut('/accounts/me/interests', { categoryIds })
}
```

The hooks. This file replaces the kind of hand-written state handling you saw in `useSystemStatus.ts`:

**`src/web/src/features/interests/useInterests.ts`**

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchCategories, fetchMyInterests, saveMyInterests } from './api.ts'

// A "query key" is the name of a drawer in the cache. Two components that use
// the same key share one fetch and one copy of the data.
const keys = {
  categories: ['categories'] as const,
  myInterests: ['me', 'interests'] as const,
}

/** The tree of groups and topics. It almost never changes, so keep it for an hour. */
export function useCategories() {
  return useQuery({
    queryKey: keys.categories,
    queryFn: ({ signal }) => fetchCategories(signal),
    staleTime: 60 * 60 * 1000,
  })
}

/** The ids of the topics the signed-in reader has chosen. */
export function useMyInterests() {
  return useQuery({
    queryKey: keys.myInterests,
    queryFn: ({ signal }) => fetchMyInterests(signal),
  })
}

/** Saves a new choice. On success the cache is updated at once, so every screen shows it. */
export function useSaveInterests() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (categoryIds: number[]) => saveMyInterests(categoryIds),
    onSuccess: (_result, categoryIds) => {
      // We know exactly what the server now holds: what we just sent.
      // Writing it into the cache saves a second trip to fetch it back.
      queryClient.setQueryData(keys.myInterests, [...categoryIds].sort((a, b) => a - b))
    },
  })
}
```

The picker, shared by both pages. Notice the split from 2.8: the ticked boxes are **screen state** (`useState`); everything from the server comes through the hooks.

**`src/web/src/features/interests/InterestPicker.tsx`**

```tsx
import { useState, type FormEvent } from 'react'
import { toApiError } from '../../shared/api/apiClient.ts'
import { ErrorNotice } from '../auth/ErrorNotice.tsx'
import { useCategories, useSaveInterests } from './useInterests.ts'

// Shown as a hint only. The server enforces the real rule (InterestSelection in the Domain).
const MINIMUM = 3

interface InterestPickerProps {
  /** Topics that should start ticked. */
  initialSelection: number[]
  saveLabel: string
  onSaved: () => void
}

/**
 * The tick-box list of topics, used by both the onboarding page and the
 * settings page. It owns only "what is ticked right now"; everything that
 * comes from the server is handled by the hooks.
 */
export function InterestPicker({ initialSelection, saveLabel, onSaved }: InterestPickerProps) {
  const categories = useCategories()
  const save = useSaveInterests()
  const [selected, setSelected] = useState<ReadonlySet<number>>(() => new Set(initialSelection))

  if (categories.isPending) {
    return <p aria-busy="true">Loading topics…</p>
  }

  if (categories.isError) {
    return <ErrorNotice error={toApiError(categories.error)} />
  }

  function toggle(topicId: number) {
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(topicId)) {
        next.delete(topicId)
      } else {
        next.add(topicId)
      }
      return next
    })
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    save.mutate([...selected], { onSuccess: onSaved })
  }

  const stillNeeded = MINIMUM - selected.size

  return (
    <form onSubmit={handleSubmit}>
      {categories.data.map((group) => (
        <fieldset key={group.id} className="topic-group">
          <legend>{group.name}</legend>
          {group.topics.map((topic) => (
            <label key={topic.id} className="topic">
              <input type="checkbox" checked={selected.has(topic.id)} onChange={() => toggle(topic.id)} />
              {topic.name}
            </label>
          ))}
        </fieldset>
      ))}
      <p className="muted" role="status">
        {stillNeeded > 0 ? `Choose ${stillNeeded} more to continue.` : `${selected.size} chosen.`}
      </p>
      <ErrorNotice error={save.isError ? toApiError(save.error) : null} />
      <button type="submit" disabled={stillNeeded > 0 || save.isPending}>
        {save.isPending ? 'Saving…' : saveLabel}
      </button>
    </form>
  )
}
```

**`src/web/src/features/interests/OnboardingPage.tsx`**

```tsx
import { useNavigate } from 'react-router'
import { InterestPicker } from './InterestPicker.tsx'

/** The first thing a new reader sees after signing in. */
export function OnboardingPage() {
  const navigate = useNavigate()

  return (
    <section className="card">
      <h2>What are you interested in?</h2>
      <p>Pick at least three topics. Your feed is built from these, and you can change them at any time.</p>
      <InterestPicker initialSelection={[]} saveLabel="Continue" onSaved={() => navigate('/', { replace: true })} />
    </section>
  )
}
```

**`src/web/src/features/interests/InterestsSettingsPage.tsx`**

```tsx
import { useState } from 'react'
import { Link } from 'react-router'
import { toApiError } from '../../shared/api/apiClient.ts'
import { ErrorNotice } from '../auth/ErrorNotice.tsx'
import { InterestPicker } from './InterestPicker.tsx'
import { useMyInterests } from './useInterests.ts'

/** Where a reader changes their topics later. */
export function InterestsSettingsPage() {
  const interests = useMyInterests()
  const [justSaved, setJustSaved] = useState(false)

  return (
    <section className="card">
      <h2>Your topics</h2>
      {interests.isPending && <p aria-busy="true">Loading your topics…</p>}
      {interests.isError && <ErrorNotice error={toApiError(interests.error)} />}
      {interests.isSuccess && (
        // The picker starts from what the server says is saved.
        <InterestPicker
          initialSelection={interests.data}
          saveLabel="Save changes"
          onSaved={() => setJustSaved(true)}
        />
      )}
      {justSaved && <p role="status">Saved.</p>}
      <p>
        <Link to="/">Back to home</Link>
      </p>
    </section>
  )
}
```

The home page sends topic-less readers to onboarding:

**`src/web/src/features/home/HomePage.tsx`**

```tsx
import { Link, Navigate } from 'react-router'
import { useAuth } from '../auth/useAuth.ts'
import { useMyInterests } from '../interests/useInterests.ts'
import { SystemStatusCard } from '../system-status/SystemStatusCard.tsx'

/** The first page behind the login. The feed moves in here in week 10. */
export function HomePage() {
  const { state, signOut } = useAuth()
  const interests = useMyInterests()

  // ProtectedRoute guarantees a signed-in reader; this check satisfies TypeScript.
  if (state.status !== 'signedIn') {
    return null
  }

  // A reader who has not chosen any topics yet does that first.
  if (interests.isSuccess && interests.data.length === 0) {
    return <Navigate to="/onboarding" replace />
  }

  return (
    <>
      <section className="card">
        <h2>Welcome</h2>
        <p>
          Signed in as <strong>{state.reader.email}</strong>
        </p>
        {interests.isSuccess && (
          <p>
            You follow {interests.data.length} topics. <Link to="/settings/interests">Change your topics</Link>
          </p>
        )}
        <button type="button" onClick={() => void signOut()}>
          Sign out
        </button>
      </section>
      <SystemStatusCard />
    </>
  )
}
```

Routes (whole file shown):

**`src/web/src/app/App.tsx`**

```tsx
import { Navigate, Route, Routes } from 'react-router'
import { CheckEmailPage } from '../features/auth/CheckEmailPage.tsx'
import { ConfirmEmailPage } from '../features/auth/ConfirmEmailPage.tsx'
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage.tsx'
import { ProtectedRoute } from '../features/auth/ProtectedRoute.tsx'
import { ResetPasswordPage } from '../features/auth/ResetPasswordPage.tsx'
import { SignInPage } from '../features/auth/SignInPage.tsx'
import { SignUpPage } from '../features/auth/SignUpPage.tsx'
import { HomePage } from '../features/home/HomePage.tsx'
import { InterestsSettingsPage } from '../features/interests/InterestsSettingsPage.tsx'
import { OnboardingPage } from '../features/interests/OnboardingPage.tsx'

// App is the "shell": it maps addresses to pages and owns no business logic.
export default function App() {
  return (
    <main>
      <h1>Magpie</h1>
      <Routes>
        {/* Open to everyone */}
        <Route path="/sign-up" element={<SignUpPage />} />
        <Route path="/check-email" element={<CheckEmailPage />} />
        <Route path="/confirm-email" element={<ConfirmEmailPage />} />
        <Route path="/sign-in" element={<SignInPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />

        {/* Signed-in readers only */}
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/onboarding" element={<OnboardingPage />} />
          <Route path="/settings/interests" element={<InterestsSettingsPage />} />
        </Route>

        {/* Anything else: go home (which sends signed-out visitors to sign-in) */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </main>
  )
}
```

Tests. All app-level tests now share one renderer that includes the cache:

**`src/web/src/test/renderApp.tsx`**

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from '../app/App.tsx'
import { createQueryClient } from '../app/queryClient.ts'
import { AuthProvider } from '../features/auth/AuthProvider.tsx'

/**
 * Renders the whole app at a given address, with the same providers as
 * main.tsx but a test router and a fresh, empty cache for every test.
 */
export function renderAppAt(address: string) {
  return render(
    <QueryClientProvider client={createQueryClient({ forTests: true })}>
      <MemoryRouter initialEntries={[address]}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
```

The pretend server gains `PUT` and two default replies (whole file shown):

**`src/web/src/test/fakeServer.ts`**

```ts
import { vi } from 'vitest'

// A tiny pretend backend for tests. You describe what each address should
// answer; anything you did not describe fails loudly, so a test can never
// pass by accident because of a call you did not expect.

type Reply = { status: number; body?: unknown; problem?: boolean }
type Handler = Reply | ((body: unknown) => Reply)

export interface FakeServer {
  /** Every call made, in order, for assertions. */
  calls: { method: string; path: string; body: unknown; headers: Record<string, string> }[]
  on(method: 'GET' | 'POST' | 'PUT', path: string, handler: Handler): FakeServer
}

/** A small tree in the same shape the real server returns. */
export const sampleCategories = [
  {
    id: 1,
    name: 'Technology',
    topics: [
      { id: 101, slug: 'programming', name: 'Programming' },
      { id: 102, slug: 'ai-machine-learning', name: 'AI & Machine Learning' },
      { id: 103, slug: 'cloud-devops', name: 'Cloud & DevOps' },
      { id: 104, slug: 'security', name: 'Security' },
    ],
  },
  {
    id: 2,
    name: 'Science',
    topics: [
      { id: 201, slug: 'space', name: 'Space' },
      { id: 202, slug: 'physics', name: 'Physics' },
    ],
  },
]

export function startFakeServer(): FakeServer {
  const routes = new Map<string, Handler>()
  const server: FakeServer = {
    calls: [],
    on(method, path, handler) {
      routes.set(`${method} ${path}`, handler)
      return server
    },
  }

  // Sensible defaults that almost every test needs.
  server.on('GET', '/api/accounts/antiforgery-token', { status: 200, body: { token: 'test-token' } })
  server.on('GET', '/api/accounts/external/providers', { status: 200, body: [] })
  // A reader who has already chosen topics, so tests about other things are
  // not sent to the onboarding page. Tests about interests override these.
  server.on('GET', '/api/accounts/me/interests', { status: 200, body: { categoryIds: [101, 102, 103] } })
  server.on('GET', '/api/accounts/categories', { status: 200, body: sampleCategories })

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      const body = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
      server.calls.push({ method, path: input, body, headers: (init?.headers ?? {}) as Record<string, string> })

      const handler = routes.get(`${method} ${input}`)
      if (!handler) {
        throw new Error(`fakeServer: no reply set up for ${method} ${input}`)
      }
      const reply = typeof handler === 'function' ? handler(body) : handler
      const isError = reply.status >= 400
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'content-type': isError ? 'application/problem+json' : 'application/json' },
      })
    }),
  )

  return server
}
```

**`src/web/src/features/interests/interests.test.tsx`**

```tsx
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { startFakeServer } from '../../test/fakeServer.ts'
import { renderAppAt } from '../../test/renderApp.tsx'

const asha = { id: 'reader-1', email: 'asha@example.com' }
const ping = { service: 'accounts', databaseTimeUtc: '2026-11-15T10:00:00+00:00', roundTripMs: 3, isSlow: false }

/** A pretend server that remembers what was saved, like the real one. */
function signedInServer(savedTopics: number[]) {
  let saved = savedTopics
  return startFakeServer()
    .on('GET', '/api/accounts/me', { status: 200, body: asha })
    .on('GET', '/api/accounts/ping', { status: 200, body: ping })
    .on('GET', '/api/accounts/me/interests', () => ({ status: 200, body: { categoryIds: saved } }))
    .on('PUT', '/api/accounts/me/interests', (body) => {
      saved = (body as { categoryIds: number[] }).categoryIds
      return { status: 204 }
    })
}

describe('onboarding', () => {
  it('sends a reader with no topics from the home page to onboarding', async () => {
    signedInServer([])

    renderAppAt('/')

    expect(await screen.findByRole('heading', { name: 'What are you interested in?' })).toBeInTheDocument()
  })

  it('keeps the button disabled until three topics are ticked, then saves and goes home', async () => {
    const server = signedInServer([])

    renderAppAt('/onboarding')
    const button = await screen.findByRole('button', { name: 'Continue' })
    expect(button).toBeDisabled()
    expect(screen.getByRole('status')).toHaveTextContent('Choose 3 more')

    await userEvent.click(screen.getByLabelText('Programming'))
    await userEvent.click(screen.getByLabelText('Space'))
    expect(button).toBeDisabled()
    await userEvent.click(screen.getByLabelText('Security'))
    expect(button).toBeEnabled()
    await userEvent.click(button)

    expect(await screen.findByText('You follow 3 topics.', { exact: false })).toBeInTheDocument()
    const put = server.calls.find((call) => call.method === 'PUT')
    expect([...(put!.body as { categoryIds: number[] }).categoryIds].sort()).toEqual([101, 104, 201])
    expect(put!.headers['X-XSRF-TOKEN']).toBe('test-token')
  })

  it('groups topics under their group name', async () => {
    signedInServer([])

    renderAppAt('/onboarding')

    const technology = await screen.findByRole('group', { name: 'Technology' })
    expect(within(technology).getByLabelText('Programming')).toBeInTheDocument()
    expect(within(technology).queryByLabelText('Space')).not.toBeInTheDocument()
  })

  it('shows the server message when the choice is refused', async () => {
    signedInServer([]).on('PUT', '/api/accounts/me/interests', {
      status: 400,
      body: { title: 'One or more validation errors occurred.', errors: { categoryIds: ['Choose at most 15 topics.'] } },
    })

    renderAppAt('/onboarding')
    for (const topic of ['Programming', 'Space', 'Security']) {
      await userEvent.click(await screen.findByLabelText(topic))
    }
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose at most 15 topics.')
  })
})

describe('changing topics later', () => {
  it('starts with the saved topics ticked and saves the new choice', async () => {
    const server = signedInServer([101, 102, 103])

    renderAppAt('/settings/interests')
    expect(await screen.findByLabelText('Programming')).toBeChecked()
    expect(screen.getByLabelText('Space')).not.toBeChecked()

    await userEvent.click(screen.getByLabelText('Programming')) // untick
    await userEvent.click(screen.getByLabelText('Space')) // tick
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Saved.')).toBeInTheDocument()
    const put = server.calls.find((call) => call.method === 'PUT')
    expect([...(put!.body as { categoryIds: number[] }).categoryIds].sort()).toEqual([102, 103, 201])
  })

  it('shows the new count on the home page without fetching again', async () => {
    const server = signedInServer([101, 102, 103])

    renderAppAt('/settings/interests')
    await userEvent.click(await screen.findByLabelText('Space'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    await screen.findByText('Saved.')
    const fetchesBefore = server.calls.filter((call) => call.method === 'GET' && call.path.endsWith('/me/interests')).length
    await userEvent.click(screen.getByRole('link', { name: 'Back to home' }))

    expect(await screen.findByText('You follow 4 topics.', { exact: false })).toBeInTheDocument()
    const fetchesAfter = server.calls.filter((call) => call.method === 'GET' && call.path.endsWith('/me/interests')).length
    expect(fetchesAfter).toBe(fetchesBefore)
  })
})
```

In `auth.test.tsx` and `passwordAndGoogle.test.tsx`, delete the local `renderAppAt` function and import the shared one instead (both files are linked below).

```
 Test Files  5 passed (5)
      Tests  35 passed (35)
```

> **GitHub (verified at commit `13be5ff`):** [`package.json`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/package.json) · [`apiClient.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/shared/api/apiClient.ts) · [`queryClient.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/app/queryClient.ts) · [`main.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/main.tsx) · [`AuthProvider.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/auth/AuthProvider.tsx) · [`api.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/api.ts) · [`useInterests.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/useInterests.ts) · [`InterestPicker.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/InterestPicker.tsx) · [`OnboardingPage.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/OnboardingPage.tsx) · [`InterestsSettingsPage.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/InterestsSettingsPage.tsx) · [`HomePage.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/home/HomePage.tsx) · [`App.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/app/App.tsx) · [`index.css`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/index.css) · [`renderApp.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/test/renderApp.tsx) · [`fakeServer.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/test/fakeServer.ts) · [`interests.test.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/interests/interests.test.tsx) · [`auth.test.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/auth/auth.test.tsx) · [`passwordAndGoogle.test.tsx`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/web/src/features/auth/passwordAndGoogle.test.tsx)

> ❓ **Q&A**
> **Q: The button is disabled until three boxes are ticked. Isn't that the rule, written twice?**
> A: The browser's copy is a *courtesy* so people are not surprised. The server's copy is the *rule*. If they ever disagree, the server wins and its message is shown (a test covers that). Never rely on the courtesy.
> **Q: After saving, why write into the cache instead of fetching again?**
> A: We know precisely what the server now holds: what we just sent and it accepted. The last test in the file proves the home page shows the new count with no extra request. When you *cannot* know the result, tell the cache to refetch instead (`invalidateQueries`).
> **Q: What happened to `useSystemStatus.ts` from Week 1?**
> A: It still works and is left alone, like the earlier backend handlers. Rewriting it with `useQuery` is this week's first exercise, and a satisfying one: about twenty lines disappear.
> **Q: Why does the retry rule skip 4xx answers?**
> A: A 401 or 404 is the server saying a clear "no". Asking twice more just delays showing the reader what happened. Network failures and 5xx are worth another try.

### Step 9: Browser test, Docker and CI

The browser journeys learn about onboarding, and a fifth one covers this week's "done when" (whole file shown):

**`e2e/tests/auth.spec.ts`**

```ts
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { linkFromEmail, newEmail } from './mailbox.ts'

const PASSWORD = 'correct horse battery'

// Small helpers that read like the steps a person would take.

async function signUpAndConfirm(page: Page, request: APIRequestContext, email: string) {
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible()

  await page.goto(await linkFromEmail(request, email, 'confirm-email'))
  await page.getByRole('button', { name: 'Confirm my email' }).click()
  await expect(page.getByRole('heading', { name: 'Email confirmed' })).toBeVisible()
}

async function signIn(page: Page, email: string, password: string) {
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

/** A brand-new reader is asked for their topics before they see the home page. */
async function chooseTopics(page: Page, ...topics: string[]) {
  await expect(page.getByRole('heading', { name: 'What are you interested in?' })).toBeVisible()
  for (const topic of topics) {
    await page.getByLabel(topic, { exact: true }).check()
  }
  await page.getByRole('button', { name: 'Continue' }).click()
}

test('a signed-out visitor is sent to the sign-in page', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('sign up, confirm by email, sign in, stay signed in, sign out', async ({ page, request, context }) => {
  const email = newEmail()
  await signUpAndConfirm(page, request, email)

  await signIn(page, email, PASSWORD)
  await chooseTopics(page, 'Programming', 'Space', 'Books')
  await expect(page.getByText('Signed in as')).toContainText(email)

  // The three safety flags, as the browser itself reports them.
  const session = (await context.cookies()).find((cookie) => cookie.name === 'magpie.session')
  expect(session, 'the session cookie should exist').toBeDefined()
  expect(session!.httpOnly).toBe(true)
  expect(session!.secure).toBe(true)
  expect(session!.sameSite).toBe('Lax')

  // HttpOnly made visible: page scripts cannot see the cookie at all.
  expect(await page.evaluate(() => document.cookie)).not.toContain('magpie.session')

  // A refresh must not sign the reader out.
  await page.reload()
  await expect(page.getByText('Signed in as')).toContainText(email)

  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('a wrong password shows the generic message and stays on the page', async ({ page, request }) => {
  const email = newEmail()
  await signUpAndConfirm(page, request, email)

  await signIn(page, email, 'definitely not the password')

  await expect(page.getByRole('alert')).toContainText('Sign-in failed')
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('forgot password: reset by email, old password stops working, new one works', async ({ page, request }) => {
  const email = newEmail()
  const newPassword = 'brand new staple gun'
  await signUpAndConfirm(page, request, email)

  await page.goto('/sign-in')
  await page.getByRole('link', { name: 'Forgot your password?' }).click()
  // Wait until the new page is really showing BEFORE typing. Both pages have
  // an "Email" box; without this line the test sometimes typed into the old one.
  await expect(page.getByRole('heading', { name: 'Forgot your password?' })).toBeVisible()
  await page.getByLabel('Email').fill(email)
  await page.getByRole('button', { name: 'Send reset link' }).click()
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible()

  await page.goto(await linkFromEmail(request, email, 'reset-password'))
  await page.getByLabel('New password').fill(newPassword)
  await page.getByRole('button', { name: 'Save new password' }).click()
  await expect(page.getByRole('heading', { name: 'Password changed' })).toBeVisible()

  await signIn(page, email, PASSWORD)
  await expect(page.getByRole('alert')).toContainText('Sign-in failed')

  await signIn(page, email, newPassword)
  await expect(page.getByRole('heading', { name: 'What are you interested in?' })).toBeVisible()
})

test('topics chosen at onboarding survive signing out and in, and can be changed', async ({ page, request }) => {
  const email = newEmail()
  await signUpAndConfirm(page, request, email)
  await signIn(page, email, PASSWORD)
  await chooseTopics(page, 'Programming', 'Space', 'Books')
  await expect(page.getByText('You follow 3 topics.')).toBeVisible()

  // Sign out and in again: straight to the home page, no second onboarding.
  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
  await signIn(page, email, PASSWORD)
  await expect(page.getByText('You follow 3 topics.')).toBeVisible()

  // Change them: the saved ones start ticked.
  await page.getByRole('link', { name: 'Change your topics' }).click()
  await expect(page.getByRole('heading', { name: 'Your topics' })).toBeVisible()
  await expect(page.getByLabel('Programming', { exact: true })).toBeChecked()
  await page.getByLabel('Gaming', { exact: true }).check()
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByText('Saved.')).toBeVisible()

  // A full reload asks the server again: the change really was stored.
  await page.goto('/')
  await expect(page.getByText('You follow 4 topics.')).toBeVisible()
})
```

```
  ✓  1 [chromium] › tests/auth.spec.ts:36:1 › a signed-out visitor is sent to the sign-in page (198ms)
  ✓  2 [chromium] › tests/auth.spec.ts:42:1 › sign up, confirm by email, sign in, stay signed in, sign out (2.6s)
  ✓  3 [chromium] › tests/auth.spec.ts:68:1 › a wrong password shows the generic message and stays on the page (765ms)
  ✓  4 [chromium] › tests/auth.spec.ts:78:1 › forgot password: reset by email, old password stops working, new one works (1.6s)
  ✓  5 [chromium] › tests/auth.spec.ts:104:1 › topics chosen at onboarding survive signing out and in, and can be changed (1.6s)
  5 passed (7.6s)
```

Both .NET Dockerfiles need one new line, because both services reference `Magpie.Defaults`, which now references the mediator:

```dockerfile
COPY src/shared/Magpie.Mediator/Magpie.Mediator.csproj src/shared/Magpie.Mediator/
```

> 🔎 **Found by running it**
> The first push of this week had green unit and integration tests and a red Docker job: `error NETSDK1004: Assets file '/src/src/shared/Magpie.Mediator/obj/project.assets.json' not found`. Our Dockerfiles copy project files one by one before restoring packages (the caching trick from Chapter 1). A new project that is not on that list is invisible to the restore step. The lesson is general: **every optimisation is also a list someone must remember to update.** The whole-system job exists to remember for us.

> **GitHub (verified at commit `13be5ff`):** [`auth.spec.ts`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/e2e/tests/auth.spec.ts) · [`Dockerfile`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/services/accounts/Dockerfile) · [`Dockerfile`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/src/gateway/Dockerfile) · [`week-04-index.sql`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/infra/experiments/week-04-index.sql) · [`ci.yml`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/.github/workflows/ci.yml) · [`0005-specific-ports-no-generic-repository.md`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/docs/adr/0005-specific-ports-no-generic-repository.md) · [`week-04-normalise-or-duplicate.md`](https://github.com/ankitchoubey800/magpie/blob/13be5ffc466dfe8040464f8dba90362b1ed11c9a/docs/design-notes/week-04-normalise-or-duplicate.md)

**Check:** push, open the pull request into `week-03`, three green ticks.

> ❓ **Q&A**
> **Q: Last week's browser tests expected "Signed in as" right after the first sign-in. They would fail now.**
> A: They did need changing: a new reader now lands on onboarding. This is the cost side of end-to-end tests. They follow real journeys, so when a journey changes they must change too. Keeping them few (five) keeps that cost small.

---

## 5. How the pieces fit together

```mermaid
flowchart TB
    subgraph web["src/web"]
      IP["InterestPicker.tsx<br/>(ticked boxes = screen state)"] --> UH["useInterests.ts<br/>(server state, cached)"] --> IA["interests/api.ts"] --> AC["apiClient.ts: apiPut()"]
    end
    AC -->|"PUT /api/accounts/me/interests"| GW["Gateway"] --> EP
    subgraph acc["src/services/accounts"]
      EP["InterestEndpoints.cs"] --> MD["Magpie.Mediator"] --> H["SetMyInterests.cs (handler)"]
      H --> R["InterestSelection.cs (rule)"]
      H -->|"IInterestStore"| ST["InterestStore.cs"]
      ST --> DC["AccountsDbContext.cs"]
    end
    DC --> PG[("user_interests")]
```

**Reading the diagram:** compare with the same picture in Chapters 1 to 3. One new box, the mediator, sits between endpoint and handler. Everything else is the shape you already know, which is the point of having a shape.

---

## 6. Break it on purpose: what does an index buy, and what does it cost?

Run the experiment. It works in a throwaway schema and deletes it afterwards:

```bash
docker compose -f infra/docker-compose.yml exec -T postgres \
  psql -U magpie -d magpie_accounts < infra/experiments/week-04-index.sql
```

It builds 1,000,000 fake readers with 5 topics each (5,000,000 rows), asks questions with and without the index, and measures what the index costs.

> ✏️ **Pencil time**
> Each topic is followed by about 4% of readers, spread evenly through the table. Predict: how many times faster will "who follows topic 204?" be with the index? 2×? 100×? 1000×?

Real figures from the GitHub runner (PostgreSQL 18, shared hardware; yours will differ):

| Step | Question | Plan PostgreSQL chose | Pages read | Time |
|---|---|---|---|---|
| 2 | Who follows topic 204? (208,334 rows) **no index** | `Parallel Seq Scan` | 41,667 | **514 ms** |
| 5 | The same, **with the index** | `Bitmap Index Scan` + `Bitmap Heap Scan` | 41,845 | **128 ms** |
| 6 | *How many* follow topic 204? with the index | `Index Only Scan` | 180 | **19 ms** |
| 3 | Does reader X follow topic 204? (primary key) | `Index Only Scan` | 3 | **0.03 ms** |

And the costs:

| Cost | Figure |
|---|---|
| Building the index on 5,000,000 rows | 1.6 s |
| Disk: table / primary key / our index | 326 MB / 271 MB / **33 MB** |
| Inserting 100,000 rows **with** the index | 307 ms |
| Inserting 100,000 rows **without** it | 208 ms (so the index made writes about 1.5× slower) |

Now read those numbers like an architect, because they are more interesting than "indexes make things fast".

**1. The index helped by 4×, not 1000×.** Look at "pages read" in steps 2 and 5: almost identical. The index told PostgreSQL exactly *which* rows match, but those 208,334 rows are scattered over nearly every page of the table, so it still had to fetch nearly every page. An index shines when a question matches *few* rows. Ours matches 4% spread everywhere. If you guessed 1000×, you assumed a selective question.

**2. The same index answered a slightly different question 27× faster still.** Step 6 only *counts*. The count can be answered from the index alone (180 pages instead of 41,845). The shape of the question matters as much as the existence of the index. If notifications in Week 15 need only "how many?" or "is there anyone?", they will be cheap. If they need every reader id, they will not.

**3. The primary key question is in a different league** (0.03 ms): three pages, because it asks for one specific row.

**4. The index is not free:** 33 MB of disk and writes 1.5× slower, forever, whether or not anyone asks the question.

> 🔎 **The experiment contradicted its own heading**
> Step 6 is titled "a question the index answers ALONE". On the first run it did not: the plan was a `Bitmap Heap Scan` that read 41,848 pages, and it took 95 ms. The heading was wrong, or something was missing. What was missing: after a big load, PostgreSQL does not yet know which pages are "settled" (visible to everyone), and until it does, it must check the table for every row an index finds. A `VACUUM` records that. On a live database this happens by itself in the background; in a script that loads five million rows and immediately asks questions, it has not happened yet. Adding `VACUUM` gave the `Index Only Scan` and 19 ms shown above.
>
> Keep this as a habit: **when a measurement disagrees with your explanation, the measurement is right.** Find out why before you write the explanation down.

**So, was adding the index the right call?** For a table that is read often and written rarely (interests change maybe a few times a year per reader), a 1.5× write cost is irrelevant and a 4× to 27× read gain is real. Yes. On a table written thousands of times a second (reader actions, Week 12), the same index might be the wrong call. Write your own conclusion, with your own numbers, in the design note.

---

## 7. Production considerations

| Area | In place this week | Deliberately later |
|---|---|---|
| **Data integrity** | Rules enforced by the database (composite key, foreign keys); whole-set replacement in one transaction; ids stable across environments | Soft-retiring a topic instead of deleting it, when the product needs to |
| **Security** | No id in the address; reader id only from the cookie; anti-forgery on `PUT`; server-side validation regardless of the browser; client cache emptied when the reader changes | Plan limits on the number of topics (Week 16) |
| **Performance** | No-tracking reads; one-column projection for interest ids; the index is measured, not assumed; category tree cached for an hour in the browser | Cache the category tree at the gateway (Week 17). Readers' interests in Valkey for the feed (Week 10) |
| **Concurrency** | Two saves at once from two tabs: the last one wins, and the key prevents duplicates | If "last write wins" ever becomes a problem, add a version check (optimistic concurrency). Not needed for tick-boxes |
| **Migrations** | Additive only; applied by the one-off job; reviewed before commit | "Expand, then contract" for the first destructive change |
| **Other services** | | Feed and notifications will need to know when interests change. That is the **outbox** in Week 12; nothing announces changes yet |
| **Testing** | Rule, mediator, handler, real tables, component tests, one browser journey | |
| **Maintainability** | One shape for new use cases; handlers discovered automatically; ADR written | Move the earlier handlers to the mediator when next touched |

---

## 8. Architect's perspective

**Risks worth naming**

| Risk | Likely / bad | What we do |
|---|---|---|
| Two styles of handler in one service (direct and through the mediator) | Certain / low | Stated in ADR-0005 with a rule for when the old ones move. A known, bounded inconsistency beats a risky rewrite |
| The mediator hides control flow ("who handles this?") | Likely as it grows / low | One handler per request, request and handler in the same file, and it stays forty lines |
| Category ids are forever | Certain / medium | Documented in the code; retiring a topic will be a flag, not a delete |
| The category list is a guess | Likely / medium | It is a product decision flagged for you; changing it later is a small migration |
| Other services will copy this data and drift | Certain from Week 10 / medium | Planned: events through the outbox (Week 12) |
| Over-indexing later because "indexes make things fast" | Likely / medium | You now have a measured counter-example |

**The design question behind this week: where does each rule live?**

"At least three topics" could live in the browser, the endpoint, the handler, the domain or the database. We put each rule in the *innermost place that can express it*:

| Rule | Home | Why there |
|---|---|---|
| No duplicate (reader, topic) | Database key | Nothing can bypass the database |
| Chosen topic must exist | Database foreign key, *and* the Domain check | The database is the guarantee; the Domain check gives a friendly message before the database has to refuse |
| 3 to 15 topics | Domain (`InterestSelection`) | It is a business decision, not a storage fact; one home, easy to change |
| Only your own interests | Address design + the cookie | Cannot be forgotten |
| Button disabled below three | Browser | Courtesy only |

Being able to place a rule and say why is a large part of what "architect" means day to day.

**What a reviewer would check first**

1. Can a reader reach another reader's data by editing a request? (No id to edit.)
2. Does any query load more than it needs? (Ids only; no tracking.)
3. Is the migration additive, and what does its `Down` destroy? (Yes; the two new tables.)
4. Is the index justified by a real question and a measurement? (Section 6.)
5. Does the cache ever show one reader's data to another? (Emptied on sign-in and sign-out; a line that is easy to forget.)

---

## 9. Alternatives and trade-offs

| Decision | We chose | Alternative | When the alternative is better |
|---|---|---|---|
| Storing choices | Join table | An array or JSON column on the user row | Data read only together with its owner, never searched from the other side, and not referenced by anything |
| Category tree | `ParentId` on each row | A separate groups table; PostgreSQL's tree types; a "path" column | Deep trees that are queried by subtree. Ours has two levels |
| Ids | Hand-chosen integers | Database-generated; slugs as keys | Generated ids for data created by users. Slugs if you are certain they never change |
| Data access | Specific ports over EF Core | Generic repository; DbContext in handlers; Dapper | DbContext-in-handlers is common and fine if you accept EF Core in the Application layer. Dapper for hot read paths |
| Dispatching use cases | Own mediator | Call handlers directly; MediatR or similar | Direct calls for small services. A library when you need its pipeline features and accept its licence |
| Reads vs writes | Two ports, one database | One port for both; separate read database | One port for trivial features. A separate read store when reads need a different shape or scale (Week 10) |
| Replace vs edit | `PUT` the whole set | `POST`/`DELETE` one topic at a time | Very large sets, or when each change must be recorded as an event |
| Seeding | In the migration | A seed script; an admin screen | Data that operators edit at run time |
| Server state in React | TanStack Query | Hand-written hooks; SWR; Redux Toolkit Query | Hand-written for one or two calls. The others are comparable; pick one and be consistent |
| Service boundary | Interests inside Accounts | A separate Profile service | When a second team owns it, or it scales differently. Today it would be a network hop for a join |

**Essential or optional?**

| Essential | Optional enhancements |
|---|---|
| Keys and foreign keys for rules that must hold | Check constraints for value ranges |
| Reading the plan before and after adding an index | Automated slow-query logging (Week 19) |
| Reads that do not track; writes in one transaction | Compiled queries |
| Server-side validation of everything | Sharing validation rules with the browser |
| Emptying the client cache when the reader changes | Optimistic updates (Week 12) |

---

## 10. Common mistakes and troubleshooting

### Mistakes beginners make this week

1. **A generic repository on top of EF Core** that hides projections and no-tracking.
2. **Returning database entities straight to the browser.** The screen then depends on the table shape, and a new column can leak.
3. **Migrations that cannot be rolled back**, or that rename a column in one step.
4. **Trusting an id from the request** for "my" data.
5. **Adding indexes by instinct** and never looking at the plan.
6. **Treating server data as component state**, then fighting stale screens.
7. **Forgetting the client cache at sign-out.**
8. **Validating only in the browser.**
9. **Loading whole rows to use one column.**

### When something goes wrong

| Symptom | Likely cause | Fix |
|---|---|---|
| `No handler is registered for X` | The handler's assembly was not passed to `AddMagpieMediator`, or the class is `abstract`/not implementing `IRequestHandler<,>` | Check the one registration line in `Program.cs` |
| Docker build: `NETSDK1004 ... Magpie.Mediator/obj/project.assets.json not found` | New project not copied before `dotnet restore` in the Dockerfile | Add the `COPY ...Magpie.Mediator.csproj` line (Step 9) |
| `relation "categories" does not exist` | Migration not applied | `docker compose ... up` runs `accounts-migrate`; check it exited 0 |
| `dotnet ef migrations add` produces an **empty** migration | The DbContext did not change, or you built an old copy | Save, rebuild, check `OnModelCreating` |
| `23503: insert or update ... violates foreign key constraint` | A category id that does not exist reached the database | Should be caught earlier by `InterestSelection`; check the `selectable` set |
| `error CS4007: ... ReadOnlySpan<int> cannot be preserved across 'await'` | `Assert.Equal([..], await ...)` in a test | `await` on its own line first (Step 7 Q&A) |
| PUT returns 400 `antiforgery` | Token header missing | Use `apiPut`, not `fetch` directly |
| Onboarding appears again after choosing topics | The save failed and the error was missed, or the cache holds an old empty list | Network tab: did the `PUT` return 204? |
| A different reader's topics flash after sign-in | Cache not cleared | `queryClient.clear()` in `AuthProvider` (Step 8) |
| Home page fetches interests on every visit | `staleTime` is 0 | See `queryClient.ts` |
| A component test fails with "No QueryClient set" | Rendered without the provider | Use `renderAppAt` from `src/test/renderApp.tsx` |
| Index experiment: step 6 shows `Bitmap Heap Scan`, not `Index Only Scan` | No `VACUUM` since the load | See the callout in section 6 |
| Index experiment is slow or runs out of disk | 5,000,000 rows need about 700 MB for a minute | Reduce `1000000` in the script; the shape of the result stays the same |
| `EXPLAIN` shows a `Seq Scan` on a tiny table that has an index | Correct: for a few rows, reading everything is cheaper | Nothing. The planner chooses by cost |

---

## 11. Knowledge check

### Pause-and-think answer (from section 1)

Five small rows per reader that point at a `categories` row by id. Renaming *Space* is one row in `categories`. "Who follows Space?" is a lookup by `CategoryId` (with an index). With lists inside each reader's row, a rename rewrites every reader who chose it, and the second question means reading every reader.

### Concept questions

1. What does "normalised" mean? Name the three promises the database keeps for `user_interests` and what enforces each.
2. Why is the primary key `(ReaderId, CategoryId)` fast for one of our two questions and useless for the other?
3. Name three costs of an index.
4. In section 6, why did the index speed the first question up only 4×, and the counting question 27× more?
5. What do "repository" and "unit of work" mean, and why did we not write classes with those names?
6. What is different about a query and a command, in purpose, answer, and how EF Core is used?
7. Why is there no reader id in `/api/accounts/me/interests`?
8. What is the difference between screen state and server state?

### Practical exercises

1. **Retire a hook.** Rewrite `useSystemStatus.ts` with `useQuery`. Keep its tests passing.
2. **Move a handler.** Convert `SignInHandler` to a request and handler dispatched by the mediator. What changes in `Program.cs`, the endpoint and the tests?
3. **Add a column safely.** Add `Description` to `Category` as a *nullable* column with a new migration. Then describe, in steps, how you would later make it required without downtime.
4. **Ask a third question.** Extend the experiment: "which readers follow topic 204 *and* topic 101?" Look at the plan. Does a second index help?
5. **Change the product rule.** Make the minimum 5 topics. Which files change? Which tests fail first? (If more than one production file changes, something is in the wrong place.)
6. **Break the cache on purpose.** Remove the two `queryClient.clear()` lines, sign in as two readers in turn in one tab, and watch.
7. **Read your own plan.** Run `EXPLAIN ANALYZE SELECT "CategoryId" FROM user_interests WHERE "ReaderId" = '<your id>';` on the real table. Which index was used?

### Interview-style questions with model answers

**Q1. What is an index and what does it cost?**
A sorted side-structure that lets the database find rows by a column's value without reading the whole table. It costs disk space, and it slows every insert, update and delete on the table because it must be kept in order. In an experiment on five million rows, mine cost 33 MB and made inserts about 1.5 times slower. It is worth it when the question is asked often, matches relatively few rows, and the table is read more than written. I check with `EXPLAIN ANALYZE` rather than assuming.

**Q2. You added an index and the query is only a little faster. Why might that be?**
The query probably matches too large a share of the table, scattered across most pages, so the database still reads nearly everything. I saw exactly this: 4% of rows, spread evenly, gave a 4× gain with the same number of pages read. Options: ask a narrower question; ask one the index can answer alone (an index-only scan, which was 27× faster again for a count); include the needed columns in the index; or accept it.

**Q3. What problem does separating reads from writes solve?**
Reads want a ready-made shape for a screen, as fast as possible, with no business rules. Writes want rules enforced and changes saved atomically. One model for both ends up poor at each: reads drag along change-tracking and objects they do not need; writes are tempted to skip rules. Separating them lets each be simple. Started lightly, as two interfaces over one database, it also prepares for a separately stored read model when scale demands it.

**Q4. How do you change a table safely while the app is live?**
Additive changes first, destructive ones last, in separate releases ("expand, then contract"): add the new column or table; release code that writes both old and new; backfill; switch reads; stop writing the old; only then drop it. Each step must be compatible with the code version before and after it, because both run during a rollout. Migrations are applied by a separate step, reviewed, and I know what `Down` would destroy before applying.

**Q5. Would you use a generic repository over EF Core?**
No. `DbSet` is already a repository and `DbContext` a unit of work. A generic wrapper mostly removes features (projections, no-tracking, includes) and adds a layer to maintain. I define small ports that name what each feature needs, which keeps the application layer free of EF Core without hiding it from the code that uses it.

**Q6. How do you make sure users can only access their own data?**
Preferably by construction: "my" endpoints take no id; it comes from the authenticated session. Where an id must be in the request, the query itself filters by owner (so a wrong id simply finds nothing), and there is a test that tries someone else's id. Never "is signed in" alone.

**Q7. Why a mediator, and what are its downsides?**
It gives every use case one shape and one entry point, so endpoints depend on a single interface and handlers are discovered rather than wired by hand. Cross-cutting steps can later wrap all of them. The downside is indirection: you cannot click from caller to handler. I keep it tiny, one handler per request, in the same file as the request.

### Self-marking

Mark each concept question **solid**, **shaky** or **gap**.

---

## 12. Completion checklist

**Built**

- [ ] `dotnet test Magpie.slnx` shows 90 passed on your machine
- [ ] `npm test` in `src/web` shows 35 passed
- [ ] `npx playwright test` in `e2e` shows 5 passed
- [ ] In your browser: a new reader is sent to onboarding, saves topics, and still has them after signing out and in
- [ ] The index experiment run on your machine, with your figures written down
- [ ] The pull request's three checks are green

**Understood**

- [ ] Normalised tables, keys, foreign keys
- [ ] What an index is and its three costs
- [ ] The four words of a query plan
- [ ] Why we wrote no repository or unit-of-work classes
- [ ] Queries versus commands
- [ ] What the mediator does and costs
- [ ] Server state versus screen state

**The five closing items**

- [ ] 1. **Design note** "Normalise or duplicate?" with your own experiment figures (`docs/design-notes/week-04-normalise-or-duplicate.md` is a draft)
- [ ] 2. **Working code and tests on your machine**, output saved
- [ ] 3. **Break-it experiment** (section 6) run by you, with your prediction and your conclusion
- [ ] 4. **Explain-back**, 150 to 300 words, in `docs/explain-back/week-04.md`
- [ ] 5. **Interview questions** answered without notes and marked

**Non-technical**

- [ ] **Decide the first category list.** Edit `StarterCategories.cs` *before* this reaches a shared database; afterwards, changes need a new migration
- [ ] First checkpoint of the CV: add a "current project" line

When every box is ticked: merge (merge commit) and `git tag week-04-done && git push --tags`.

---

## 13. Cumulative architecture overview (version 4, after Week 4)

### System map

```mermaid
flowchart TB
    B["Browser<br/>(TanStack Query cache)"] --> G["Gateway (YARP)"]
    G --> W["Web (React, nginx)"]
    G --> A["Accounts service<br/>mediator → handlers"]
    A --> P[("PostgreSQL<br/>users, categories, user_interests")]
    A --> M["Mailpit (laptop)"]
    A -.-> GG["Google (optional)"]
    J["accounts-migrate job"] --> P
    E2E["Playwright browser tests"] --> G

    classDef planned stroke-dasharray: 5 5,color:#888
    C["Collector (Week 5)"]:::planned -.->|"tags content with categories"| P2[("its own database")]:::planned
    G -.-> F["Feed (Week 10)"]:::planned
```

**Reading the diagram:** still one service of ours behind the gateway. It now owns three groups of tables. Next week the second service arrives, with a database of its own.

### What exists

| Component | Responsibility today | Added | Changed |
|---|---|---|---|
| Gateway | Routes; forwards the reader's address, scheme and host | Week 1 | |
| Accounts service | Accounts, sign-in, reset, Google; **categories and reader interests** | Week 1 | Weeks 2, 3, 4 |
| Web | Auth pages; **onboarding and topic settings** | Week 1 | Weeks 2, 3, 4 |
| PostgreSQL | `users` and helpers; **`categories`, `user_interests`** | Week 1 | Weeks 2, 4 |
| Magpie.Defaults | Logging, errors, health, forwarded headers, **mediator registration** | Week 1 | Weeks 2, 3, 4 |
| **Magpie.Mediator** | **Requests, handlers and the switchboard between them** | Week 4 | |
| Tests | Unit, integration, component, browser | Week 1 | every week |

### Conventions (additions this week in **bold**)

| Topic | Convention |
|---|---|
| Public API addresses | `/api/<service>/...`, forwarded unchanged. **Data that belongs to the signed-in reader lives under `/me/` and never takes a reader id** |
| Errors | Problem Details with `service`, `traceId`, optional `code` and `errors` |
| Layers | `Domain` references nothing. **`Application` references `Domain` and our own dependency-free shared abstractions (`Magpie.Mediator`), nothing else.** `Infrastructure` implements ports. `Api` is the composition root |
| **Use cases** | **New ones are a request record (`ICommand<T>` or `IQuery<T>`) and a handler in one file, sent through `IMediator`. Earlier handlers move when next touched** |
| **Data access** | **Specific ports per feature: a read door (no tracking, screen-shaped records, never changes data) and a write door (domain objects, one `SaveChangesAsync` per use case). No generic repository** |
| **Rules** | **Each rule lives in the innermost place that can express it: database constraint, then Domain, then courtesy in the browser** |
| **HTTP verbs** | **`PUT` for "replace with exactly this" (safe to repeat); `POST` for "do this"** |
| **Reference data** | **Ids chosen by hand and never reused; seeded by the migration** |
| **Indexes** | **Added for a named question, with the plan checked before and after** |
| Sign-in | Cookie `magpie.session`; no login secret in JavaScript |
| Changing data | With `X-XSRF-TOKEN`; a `GET` never changes anything (except the outside sign-in return leg) |
| Settings | Typed options validated at start-up |
| Behind the gateway | Trust forwarded headers; the gateway never does |
| Database changes | Migrations, applied only by `--migrate`. **Additive first; destructive changes by "expand, then contract"** |
| Frontend | Feature folders; all calls through `apiClient.ts`; sign-in state only in `AuthProvider`. **Server data through TanStack Query hooks, one hooks file per feature; the cache is emptied when the reader changes. Ticked boxes and typed text are component state** |
| Tests | `Unit/`, `Integration/`, `e2e/`. **App-level web tests use `renderAppAt`** |
| Git | One branch and pull request per week, stacked; merge commits |

### Decision log

| ADR | Decision | Week |
|---|---|---|
| 0001 | Separate services behind one gateway | 1 |
| 0002 | Clean Architecture layers inside each .NET service | 1 |
| 0003 | Cookie sessions for the browser | 2 |
| 0004 | An outside sign-in is never linked by email alone | 3 |
| 0005 | Specific read and write ports; no generic repository; our own mediator | 4 |

---

## 14. Before we start Week 5

### Things only you can do

1. Run everything locally, including the experiment, and write down your own figures and conclusion.
2. Decide the category list.
3. Design note, explain-back, interview questions, CV line.
4. Merge the stacked pull requests in order (#1, #2, #3), each with a merge commit.

### Differences from the weekly guide (to record in the progress log)

| What the guide said | What we built | Why |
|---|---|---|
| "For example 30 categories in a two-level tree" | 6 groups + 24 topics = 30 entries; readers choose among the 24 topics | Groups are headings, not choices |
| "Insert 1,000,000 fake readers with interests" into the tables | The same volume in a throwaway `lab` schema of identical shape | Does not pollute real tables; can be run repeatedly and in CI |
| "Time the query with and without the index" | Also measured pages read, an index-only question, disk size and write cost | The time alone hides the most useful lesson |
| "Our own small mediator (about 50 lines)" | About 40 lines, in its own shared project | Later services will use it too |
| Repository and Unit of Work | Not written; EF Core's are used behind specific ports | The guide's own lesson |

### Carried into Week 5 (collectors, part 1)

| Item | Why it matters |
|---|---|
| **A second service with its own database** | First real test of "services do not share tables". The collector will refer to categories by id or slug, not join to them |
| **A background worker**, not a web API | New hosting shape; health checks still apply |
| **Strategy, Adapter, Factory, Decorator** | The first week where classic design patterns earn their place |
| **Calling other people's servers** | Timeouts, retries with backoff and jitter, politeness, and failure isolation |
| **The mediator in a second service** | Proves it was right to make it shared |

### Carried further ahead

| Item | When |
|---|---|
| Announce "interests changed" to other services (outbox) | Week 12 |
| Readers' interests cached for the feed | Week 10 |
| Plan limits on topics | Week 16 |
| Move earlier handlers to the mediator | When next touched |
| Everything still open from Chapters 2 and 3 | As listed there |

### Sources checked while writing this chapter

- TanStack Query version installed: see `src/web/package.json` in the repository.
- The mediator, its registration and the domain rule were compiled and run locally before the first push; everything touching EF Core was first compiled on GitHub.
- All figures in section 6: the run linked at the top of this chapter. The first, contradictory result is from the run before it and is described in the callout.
