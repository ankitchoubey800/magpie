-- Week 4 break-it experiment: what does an index buy, and what does it cost?
--
--   docker compose -f infra/docker-compose.yml exec -T postgres \
--     psql -U magpie -d magpie_accounts < infra/experiments/week-04-index.sql
--
-- Everything happens in a throwaway schema called "lab", which is deleted at
-- the end. Your real tables are not touched.

\timing on
\pset pager off
DROP SCHEMA IF EXISTS lab CASCADE;
CREATE SCHEMA lab;

-- The same shape as the real user_interests table, WITHOUT the index on "CategoryId".
CREATE TABLE lab.user_interests (
    "ReaderId"   uuid             NOT NULL,
    "CategoryId" integer          NOT NULL,
    "Weight"     double precision NOT NULL,
    "AddedAtUtc" timestamptz      NOT NULL,
    PRIMARY KEY ("ReaderId", "CategoryId")
);

\echo
\echo === 1. Load 1,000,000 fake readers with 5 topics each (5,000,000 rows) ===
INSERT INTO lab.user_interests
SELECT ('00000000-0000-0000-0000-' || lpad(to_hex(reader), 12, '0'))::uuid,
       topics.ids[1 + ((reader + pick * 5) % 24)],   -- 5 different topics per reader, spread evenly
       1,
       now()
FROM generate_series(1, 1000000) AS reader,
     generate_series(0, 4) AS pick,
     (SELECT ARRAY[101,102,103,104, 201,202,203,204, 301,302,303,304,
                   401,402,403,404, 501,502,503,504, 601,602,603,604] AS ids) AS topics;

-- Tell the planner what the data looks like, so its choices are fair.
ANALYZE lab.user_interests;

\echo
\echo === 2. The question: "which readers follow topic 204?"  WITHOUT an index ===
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY ON)
SELECT "ReaderId" FROM lab.user_interests WHERE "CategoryId" = 204;

\echo
\echo === 3. A narrower question: "does reader X follow topic 204?" uses the primary key (always fast) ===
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY ON)
SELECT 1 FROM lab.user_interests
WHERE "ReaderId" = '00000000-0000-0000-0000-00000000002a' AND "CategoryId" = 204;

\echo
\echo === 4. Build the index (this is the one-off cost in time) ===
CREATE INDEX ix_user_interests_category ON lab.user_interests ("CategoryId");
-- VACUUM tidies up after the big load and records which pages are "all settled".
-- PostgreSQL needs that record before it will answer a question from an index
-- alone (step 6). On a live database it happens automatically in the background.
VACUUM (ANALYZE) lab.user_interests;

\echo
\echo === 5. The same question as step 2, WITH the index ===
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY ON)
SELECT "ReaderId" FROM lab.user_interests WHERE "CategoryId" = 204;

\echo
\echo === 6. A question the index answers ALONE: "how many readers follow topic 204?" ===
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF, SUMMARY ON)
SELECT count(*) FROM lab.user_interests WHERE "CategoryId" = 204;

\echo
\echo === 7. What the index costs in disk space ===
SELECT pg_size_pretty(pg_relation_size('lab.user_interests'))              AS table_size,
       pg_size_pretty(pg_relation_size('lab.user_interests_pkey'))         AS primary_key_size,
       pg_size_pretty(pg_relation_size('lab.ix_user_interests_category'))  AS category_index_size;

\echo
\echo === 8. What the index costs on every write: 100,000 new rows with the index in place ===
INSERT INTO lab.user_interests
SELECT ('10000000-0000-0000-0000-' || lpad(to_hex(reader), 12, '0'))::uuid, 101, 1, now()
FROM generate_series(1, 100000) AS reader;

\echo
\echo === 9. The same 100,000 rows after dropping the index ===
DROP INDEX lab.ix_user_interests_category;
INSERT INTO lab.user_interests
SELECT ('20000000-0000-0000-0000-' || lpad(to_hex(reader), 12, '0'))::uuid, 101, 1, now()
FROM generate_series(1, 100000) AS reader;

DROP SCHEMA lab CASCADE;
