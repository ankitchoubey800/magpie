# Week 4 design note: normalise or duplicate?

**Status:** draft written with the chapter. Ankit to add his own machine's numbers from the index experiment before the week closes.

## What we need

Store which topics each reader follows, change them safely, and answer two
questions: "what does reader X follow?" (every page load) and "who follows
topic X?" (notifications in week 15, analytics).

## Rough numbers

- 1,000,000 readers x about 5 topics = 5,000,000 rows.
- One row is two ids, a number and a date: tens of bytes. The whole table is a
  few hundred megabytes including indexes (measured below).
- Reads vastly outnumber writes: interests change rarely.

## Options

1. **Normalised (chosen).** `categories` holds each topic once. `user_interests`
   holds one row per (reader, topic) pair and refers to both by id. Renaming a
   topic is one row changed. The database enforces "no duplicates" (the pair is
   the key) and "no choices for topics that do not exist" (foreign key).
2. **Duplicated.** Store the chosen topic names as an array on the user row.
   One read, no join. Renaming a topic means rewriting every reader who chose
   it; "who follows topic X?" has no cheap answer; nothing stops a typo.

## Choice and reason

Normalised. This data is small, changes rarely, must stay exact, and is asked
about from both directions. Those are the conditions under which a relational
design is at its best.

## The index experiment (from the CI run; replace with your own)

See Chapter 4, section 6 for the figures.

## Where we WILL duplicate on purpose, and why (a prediction)

- **Week 8, content items in DynamoDB:** each item will carry its source name
  and category name, so listing a category is one read with no joins.
- **Week 10, feeds in Valkey:** a reader's ready-made feed is a copy of data
  that lives elsewhere, kept because it must answer in milliseconds.
- The rule of thumb: duplicate when reads must be very fast and the copied
  value rarely changes; keep one copy when correctness matters more than speed.
