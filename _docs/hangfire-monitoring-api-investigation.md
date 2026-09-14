# Hangfire Monitoring API — Technical Investigation

**Status:** Investigation only (no application code)  
**Date:** 2026-09-14  
**Sources:** HangfireIO/Hangfire source (`main`), Hangfire documentation (`docs.hangfire.io`), NuGet package metadata for Hangfire 1.8.25

Legend used below:

| Label | Meaning |
| --- | --- |
| **Fact** | Verified from Hangfire source and/or official documentation |
| **Assumption** | Reasonable inference not fully proven in this investigation |
| **Recommendation** | Proposed approach for Hangfire Monitor |

---

## Executive summary

**Recommendation:** Use Hangfire’s public monitoring abstractions for **failed-job count**, and a **narrow read-only SQL query** for exact **`MAX(FailedAt)`**. Do not define “last failure” as highest Job Id, and do not rely on `FailedJobs(0, 1)` for chronological correctness.

Clarified MVP definition:

> “Last failure” = the most recent actual failure timestamp = `MAX(FailedAt)` among jobs currently in the Failed state.

For each configured application:

| Need | Approach | Notes |
| --- | --- | --- |
| Failed job count | `GetStatistics().Failed` via `IMonitoringApi` | Efficient, uncapped `COUNT` in SQL Server implementation |
| Exact latest failure timestamp | Narrow SQL `MAX(State.CreatedAt)` for current Failed jobs | Public Monitoring API has **no efficient** `MAX(FailedAt)` operation |

Independent `SqlServerStorage` instances can still be created from connection strings **without** starting a Hangfire server. Keep `PrepareSchemaIfNecessary = false`.

See [Determining the actual latest failure](#determining-the-actual-latest-failure) for the detailed alternatives analysis.

---

## Relevant Hangfire APIs

### 1. `JobStorage` (**Fact**)

- Abstract base type in `Hangfire.Core` (`Hangfire.JobStorage`).
- Key members for this investigation:
  - `abstract IMonitoringApi GetMonitoringApi()`
  - `abstract IStorageConnection GetConnection()`
  - `static JobStorage Current` — global singleton used by static Hangfire APIs; **not required** for monitoring if you hold concrete storage instances.
- SQL Server concrete type: `Hangfire.SqlServer.SqlServerStorage : JobStorage`.

### 2. `JobStorage.GetMonitoringApi()` (**Fact**)

- Abstract factory for the storage-specific monitoring implementation.
- `SqlServerStorage` returns `new SqlServerMonitoringApi(this, _options.DashboardJobListLimit)`.

### 3. `IMonitoringApi` (**Fact**)

Public interface in `Hangfire.Storage` (package `Hangfire.Core`). Methods relevant to the MVP:

| Method | Role |
| --- | --- |
| `long FailedCount()` | Count of jobs currently in Failed state |
| `JobList<FailedJobDto> FailedJobs(int from, int count)` | Page of failed jobs |
| `StatisticsDto GetStatistics()` | Aggregate counters including `Failed` |
| `IDictionary<DateTime, long> FailedByDatesCount()` | Daily failed timeline aggregates (not a single “latest” timestamp) |
| `IDictionary<DateTime, long> HourlyFailedJobs()` | Hourly failed timeline aggregates |

There is also `JobStorageMonitor` (abstract class implementing `IMonitoringApi`) used as the base for storage-specific monitors.

### 4. `FailedCount()` (**Fact** — SQL Server behavior)

In `SqlServerMonitoringApi`:

- Queries `Job` where `StateName = Failed`.
- If `DashboardJobListLimit` has a value, the count uses `TOP (@limit)` and therefore **may be capped**.
- Default `SqlServerStorageOptions.DashboardJobListLimit` is **10000**.

So `FailedCount()` is **not** always the true total under default options.

### 5. `FailedJobs(int from, int count)` (**Fact** — SQL Server behavior)

- Returns `JobList<FailedJobDto>`.
- SQL Server implementation calls `GetJobs(..., FailedState.StateName, descending: true, ...)`.
- Ordering is `ORDER BY j.Id desc` (job identity), **not** `FailedAt` / state change time.
- `FailedAt` is populated from `State.CreatedAt` (`sqlJob.StateChanged`).

### 6. `GetStatistics()` (**Fact** — SQL Server behavior)

- Returns `StatisticsDto` with `Failed` among other counters.
- Failed count query is an uncapped:

  `select count(Id) from [{schema}].Job ... where StateName = N'Failed'`

- Prefer this over `FailedCount()` when the true failed total matters and default dashboard limits remain enabled.

### 7. `FailedJobDto` (**Fact**)

Properties include:

- `DateTime? FailedAt`
- `string Reason`, `ExceptionType`, `ExceptionMessage`, `ExceptionDetails`
- `Job Job`, `InvocationData InvocationData`, `JobLoadException LoadException`
- `bool InFailedState`, `IDictionary<string, string> StateData`

**Assumption:** Deserializing `Job` may fail in a monitor process that does not reference the monitored apps’ job assemblies; `FailedAt` and exception fields should still be usable because they come from state data, not method resolution.

### 8. `JobList<T>` (**Fact**)

- Type: `JobList<TDto> : List<KeyValuePair<string, TDto>>`
- Key = job id string; Value = DTO instance.
- For failed jobs: `JobList<FailedJobDto>`.

### 9. Instantiating / configuring SQL Server storage (**Fact**)

Documented and implemented constructors include:

```csharp
new SqlServerStorage(connectionString);
new SqlServerStorage(connectionString, options);
new SqlServerStorage(connectionFactory, options);
```

Extension methods on `GlobalConfiguration` (`UseSqlServerStorage(...)`) create a `SqlServerStorage` and register it as current storage. That registration path is optional for a multi-storage monitor.

Important options:

| Option | Default | Relevance |
| --- | --- | --- |
| `PrepareSchemaIfNecessary` | `true` | Can install/migrate Hangfire SQL objects on construction |
| `SchemaName` | `"HangFire"` | Must match monitored apps |
| `DashboardJobListLimit` | `10000` | Caps `FailedCount()` (and related list counts) |
| `TryAutoDetectSchemaDependentOptions` | `true` | Reads schema version on startup (read) |
| `SqlClientFactory` | auto-detect | Needs `Microsoft.Data.SqlClient` or `System.Data.SqlClient` |

Official docs: [Using SQL Server](https://docs.hangfire.io/en/latest/configuration/using-sql-server.html).

---

## SQL Server implementation details

### Monitoring query characteristics (**Fact**)

For failed count / failed jobs / statistics failed count, `SqlServerMonitoringApi` issues **SELECT** statements (often with `nolock`).

Examples:

- Failed list: CTE over `Job` filtered by `StateName`, join to `State` for reason/data/`CreatedAt`.
- Statistics failed: direct `count(Id)` on failed jobs.

### Schema preparation risk (**Fact**)

If `PrepareSchemaIfNecessary` is `true` (default), `SqlServerStorage.Initialize()` runs `SqlServerObjectsInstaller.Install(...)`, which can create/upgrade Hangfire schema objects.

For a read-only monitor against existing production storages, this default is unsafe.

### Server processes (**Fact**)

`SqlServerStorage` exposes storage-wide processes (expiration manager, counters aggregator, heartbeat). Those run when a **Hangfire server** hosts the storage — not merely because `GetMonitoringApi()` was called.

### Multi-storage precedent (**Fact** / community usage)

Hangfire Dashboard is storage-backed via `JobStorage`. Community discussions (e.g. multi-tenant / multi-environment dashboard scenarios) reinforce using concrete `JobStorage` instances rather than only `JobStorage.Current`.

---

## Proposed usage model

**Recommendation:** Treat each configured monitored application as one `JobStorage` + `IMonitoringApi` pair.

Conceptual flow (illustrative, not implementation):

1. For each configured app, create:

   `var storage = new SqlServerStorage(connectionString, options);`

2. Obtain monitoring API:

   `var monitor = storage.GetMonitoringApi();`

3. Read MVP metrics:

   - Count: `monitor.GetStatistics().Failed` (**public API**)
   - Exact last failure: narrow read-only SQL for `MAX(FailedAt)` / `MAX(State.CreatedAt)` among Failed jobs (**justified fallback**; see below)

4. Do **not** call `UseHangfireServer` / `AddHangfireServer` for monitoring.
5. Avoid relying on `JobStorage.Current` when monitoring multiple storages concurrently.
6. Configure options defensively:

   - `PrepareSchemaIfNecessary = false`
   - `SchemaName` from configuration when not default
   - Consider `DashboardJobListLimit = null` if `FailedCount()` will be used later
   - Prefer read-only SQL credentials (**Assumption:** sufficient for SELECT on Hangfire schema)

### Multi-provider future (**Fact** + **Recommendation**)

- **Fact:** Any Hangfire storage implements `JobStorage.GetMonitoringApi()` and thus `IMonitoringApi`.
- **Recommendation:** Code against `JobStorage` / `IMonitoringApi` for app-level monitoring logic; keep SQL Server construction behind a narrow factory. Adding Redis/other providers later becomes a storage-construction concern, not a rewrite of failed-job metric logic — subject to provider-specific semantics (ordering, limits, caps).

---

## Package dependencies

### Is `Hangfire.Core` alone sufficient? (**Fact**)

**No** for SQL Server monitoring.

- `Hangfire.Core` provides `JobStorage`, `IMonitoringApi`, DTOs (`FailedJobDto`, `StatisticsDto`, `JobList<T>`).
- SQL Server storage and `SqlServerMonitoringApi` live in **`Hangfire.SqlServer`**.

### What does `Hangfire.SqlServer` provide? (**Fact**)

- `SqlServerStorage`
- `SqlServerStorageOptions`
- `SqlServerMonitoringApi`
- Schema installer / SQL templates
- Dependency on matching `Hangfire.Core` version (`=` exact version on NuGet)

### Recommended packages for a .NET 9 monitor (**Recommendation**)

| Package | Version | Required? | Purpose |
| --- | --- | --- | --- |
| `Hangfire.Core` | **1.8.25** (pin exact) | Yes | Monitoring abstractions |
| `Hangfire.SqlServer` | **1.8.25** (same as Core) | Yes | SQL Server `JobStorage` + monitoring impl |
| `Microsoft.Data.SqlClient` | current stable (e.g. 5.x/6.x) | Yes (practical) | SQL client factory used by Hangfire.SqlServer |

**Not required for MVP monitoring-only usage:**

- `Hangfire` meta-package (convenience; pulls SqlServer — less explicit)
- `Hangfire.AspNetCore` / `Hangfire.NetCore` (DI/server/dashboard integration)
- Hangfire server hosting packages

**Fact:** `Hangfire.SqlServer` targets `netstandard2.0` (and older TFMs); compatible with .NET 9 via .NET Standard.

---

## Version considerations

| Topic | Assessment |
| --- | --- |
| Core ↔ SqlServer pairing | **Fact:** NuGet dependency is exact (`Hangfire.Core = 1.8.25` for SqlServer 1.8.25). Keep versions identical. |
| Monitor vs monitored app versions | **Assumption:** Monitoring a Hangfire 1.7/1.8 schema with Hangfire 1.8.25 client libraries is usually workable for read APIs, but schema/option differences exist. Verify against real target databases. |
| Schema auto-detect | **Fact:** On storage construction, Hangfire may query `[Schema].Version` and adjust options when `TryAutoDetectSchemaDependentOptions` is true. |
| Future Hangfire 2.0 | **Assumption:** Breaking changes are possible; pin 1.8.x until an upgrade decision is documented. |
| Dashboard list limit semantics | **Fact:** Behavior depends on options, not only package version. |

**Recommendation:** Pin `1.8.25` (or the latest 1.8.x deliberately chosen together) for both packages; document the monitored apps’ Hangfire versions as an operational compatibility matrix.

---

## Read/write considerations

### Do MVP monitoring calls modify storage? (**Fact** for SQL Server read path)

For `GetStatistics()`, `FailedCount()`, and `FailedJobs(...)`, the SQL Server monitoring implementation performs **SELECT** queries. No updates/deletes were observed in those methods.

### Can storage construction modify the database? (**Fact**)

**Yes**, by default:

- `PrepareSchemaIfNecessary = true` may install/migrate schema.

**Recommendation:** Always set `PrepareSchemaIfNecessary = false` in Hangfire Monitor.

### Does monitoring require a running Hangfire server? (**Fact**)

**No.** `GetMonitoringApi()` reads storage. A `BackgroundJobServer` is only needed to process jobs / run server processes.

### Isolation / consistency (**Fact** + **Assumption**)

- Monitoring queries commonly use `nolock`.
- **Assumption:** Counts/lists may reflect in-flight/uncommitted states under concurrent workers; acceptable for a monitor MVP, but not a transactional audit snapshot.

---

## Determining the actual latest failure

**Requirement (product decision):** “Last failure” means `MAX(FailedAt)` among jobs currently in the Failed state — **not** the highest Hangfire Job Id.

What Hangfire stores as failure time (**Fact**):

- `FailedState.FailedAt` is set to `DateTime.UtcNow` when the failed state is created and serialized into `State.Data["FailedAt"]`.
- SQL Server Monitoring maps `FailedJobDto.FailedAt` from **`State.CreatedAt`** (`s.CreatedAt as StateChanged`), not by parsing JSON `FailedAt` in list queries.
- **Assumption for MVP SQL:** `MAX(State.CreatedAt)` for the job’s **current** Failed state is the Monitoring-equivalent of `MAX(FailedAt)` and is the practical target. Parsing JSON `FailedAt` is possible but heavier and usually redundant.

### 1. `IMonitoringApi.FailedJobs(...)` (**Fact** + assessment)

| Aspect | Finding |
| --- | --- |
| Provides `FailedAt`? | Yes, per returned DTO |
| Returns `MAX(FailedAt)` directly? | **No** |
| SQL Server page order | `ORDER BY Job.Id DESC` |
| First page (`from: 0, count: 1`) | Highest **Id** among failed jobs, not guaranteed chronological max |
| Exact max via API? | Only by retrieving **all** failed jobs (all pages) and computing `Max(FailedAt)` client-side |

**Verdict:** Suitable as a DTO source, **not** as an efficient or first-page-safe `MAX(FailedAt)` API.

### 2. `IMonitoringApi.GetStatistics()` (**Fact**)

- Exposes `StatisticsDto.Failed` (count only).
- No timestamp fields.

**Verdict:** Excellent for failed count; **irrelevant** for latest failure time.

### 3. Other public Hangfire monitoring APIs (**Fact**)

| API | Useful for `MAX(FailedAt)`? |
| --- | --- |
| `FailedCount()` | Count only (and possibly capped) |
| `FailedByDatesCount()` | Daily aggregate buckets, not an exact latest timestamp |
| `HourlyFailedJobs()` | Hourly aggregate buckets; can hint “activity in hour H” but not exact `MAX(FailedAt)` |
| `JobDetails(jobId)` | Per-job only; requires already knowing the job id |

**Verdict:** No other public Monitoring API returns a maximum failure timestamp.

### 4. Direct SQL Server access as a fallback (**Recommendation**)

Illustrative read-only query aligned with SQL Server Monitoring’s join shape:

```sql
SELECT MAX(s.CreatedAt) AS MaxFailedAt
FROM [{schema}].[Job] j WITH (NOLOCK, FORCESEEK)
INNER JOIN [{schema}].[State] s WITH (NOLOCK, FORCESEEK)
    ON j.StateId = s.Id AND j.Id = s.JobId
WHERE j.StateName = N'Failed';
```

Why this is justified for MVP:

- Matches how `FailedJobDto.FailedAt` is populated in `SqlServerMonitoringApi`.
- Uses existing Hangfire tables/indexes (`IX_HangFire_Job_StateName` helps locate Failed jobs).
- Single round-trip aggregate; does not deserialize invocation payloads.
- Remains read-only when credentials and schema-prep settings are correct.

Risks: schema-name coupling; Hangfire schema evolution; not portable to Redis/other storages without a different strategy.

### 5. Does Hangfire expose any public API that efficiently returns `MAX(FailedAt)`? (**Fact**)

**No.** There is no `IMonitoringApi` method such as `GetLastFailedAt()` / `MaxFailedAt()`.

Cross-storage note (**Fact**): Redis-based monitoring historically retrieves failed jobs from a sorted set scored by failure time, so “first failed job” can coincide with chronological newest **on that provider**. That coincidence is **provider-specific**, not a Core API guarantee, and does **not** help SQL Server.

### 6. Performance implications of retrieving all failed jobs (**Fact** + **Assumption**)

Failed jobs **do not expire by default** and can accumulate indefinitely (Dashboard warns about this).

Scanning via `FailedJobs`:

- **O(N / pageSize)** SQL round-trips
- Each page joins `Job` + `State` and deserializes invocation data into `Job` objects
- Memory grows with page buffering / aggregation
- Cost grows without bound as failed jobs accumulate

**Assumption:** Unacceptable as the default MVP path for multi-application polling.

### 7. Can `FailedJobs` pagination safely determine the maximum timestamp? (**Fact**)

| Strategy | Safe for exact `MAX(FailedAt)`? |
| --- | --- |
| `FailedJobs(0, 1)` only | **No** (Id order ≠ time order on SQL Server) |
| First *k* pages, then `Max(FailedAt)` | **No** unless *k* covers the entire failed set |
| All pages until exhausted, then `Max(FailedAt)` | **Yes**, but expensive |
| Using `FailedCount()` as loop bound | **Risky** if count is capped by `DashboardJobListLimit` |

Pagination is safe for exact max **only** with a complete scan and an uncapped/accurate total (or end-of-data detection by empty page).

### 8. Is Job Id ordering guaranteed by the public API or only an implementation detail? (**Fact**)

- `IMonitoringApi.FailedJobs` XML/API docs do **not** specify ordering.
- SQL Server implementation orders by `Job.Id` descending.
- Hangfire maintainers have discussed ordering as Dashboard UX convenience (“reversed” lists for Failed/Succeeded/Deleted) and noted **inconsistencies across storage providers** ([issue #2160](https://github.com/HangfireIO/Hangfire/issues/2160)).

**Verdict:** Job Id descending order is an **implementation / Dashboard convention**, **not** a portable public contract for chronological “last failure”.

### 9. What does the Hangfire Dashboard assume? (**Fact**)

`FailedJobsPage` does:

1. `monitor.FailedCount()` for paging
2. `monitor.FailedJobs(pager.FromRecord, pager.RecordsPerPage)`
3. Render each row’s `FailedAt` as relative time

It does **not** sort client-side by `FailedAt`, and it does **not** compute `MAX(FailedAt)`. The Dashboard presents whatever order the storage’s `FailedJobs` implementation returns (on SQL Server: newer **Ids** first). That is **not** the same product semantic as Hangfire Monitor’s clarified MVP requirement.

### 10. Supporting both exact latest timestamp and efficient failed count (**Recommendation**)

Use a **hybrid** approach:

| Metric | Mechanism | Characteristics |
| --- | --- | --- |
| Failed count | `IMonitoringApi.GetStatistics().Failed` | Public API; cheap aggregate; Dashboard-aligned |
| Exact last failure | Narrow SQL `MAX(State.CreatedAt)` for current Failed jobs | Exact chronological max; cheap aggregate; SQL Server-specific |

This preserves Hangfire API alignment where the API is fit for purpose, and uses the smallest justified SQL footprint where the API cannot meet the requirement efficiently.

Alternatives considered and rejected for MVP default:

| Alternative | Why rejected |
| --- | --- |
| API-only `FailedJobs(0, 1)` | Does not satisfy `MAX(FailedAt)` on SQL Server |
| API-only full scan | Correct but poor / unbounded performance |
| Relax to Dashboard Job Id semantics | Explicitly rejected by MVP definition |
| Timeline APIs only | Insufficient precision |

### MVP recommendation for latest failure

1. **Exact `MAX(FailedAt)` cannot be fulfilled efficiently using only public Hangfire APIs.**  
   - Public APIs can fulfill it **only** via full `FailedJobs` enumeration + client-side max.
2. **Direct SQL is justified** for the SQL Server MVP to obtain exact `MAX(FailedAt)` (via `MAX(State.CreatedAt)` for current Failed jobs).
3. **Do not relax** the requirement to Dashboard-style Job Id semantics.
4. **Expected performance characteristics:**
   - Failed count (`GetStatistics`): typically one multi-statement read batch; cheap.
   - Last failure (narrow `MAX` SQL): one aggregate query over Failed jobs’ current state rows; cheap relative to list materialization.
   - Full `FailedJobs` scan: expensive, worsens as failed jobs accumulate; reserve only as a last-resort/debug approach.

---

## Risks and limitations

1. **No efficient public `MAX(FailedAt)` API** (**Fact**): exact chronological “last failure” requires either full failed-job scans via `FailedJobs` or narrow SQL.
2. **`FailedJobs(0, 1)` is not `MAX(FailedAt)` on SQL Server** (**Fact**): ordering is by Job Id descending (implementation detail / Dashboard convenience).
3. **`FailedCount()` can be capped** (**Fact**) at `DashboardJobListLimit` (default 10,000). Prefer `GetStatistics().Failed` for totals.
4. **Schema preparation default is write-capable** (**Fact**): must disable for safe monitoring.
5. **Custom schema / table prefix** (**Fact**): default schema is `HangFire`; misconfiguration yields empty/wrong results or errors.
6. **Direct SQL couples the monitor to SQL Server schema** (**Fact** / product risk): acceptable for current MVP storage choice; document and isolate it.
7. **Failed jobs do not expire by default** (**Fact**): full API scans become increasingly expensive as failed sets grow.
8. **Job deserialization noise** (**Assumption**): relevant only if loading full `FailedJobDto.Job` graphs; not needed for count + `MAX(FailedAt)`.
9. **Multi-storage operational concerns** (**Assumption**): connection storms, timeouts, and partial failures when polling many apps need product-level handling later.
10. **Version/schema drift** across monitored estates (**Assumption**).
11. **Licensing** (**Fact**): Hangfire packages are LGPL-3.0 (also commercial dual-license). This repository currently uses GPL-3.0; distribution/contribution posture should be confirmed if the tool joins the Hangfire ecosystem.

---

## Answers to investigation questions

### A. Can we implement the MVP using the Hangfire Monitoring API?

**Partially.** **Recommendation:** use `IMonitoringApi` for failed **count**; use narrow read-only SQL for exact **`MAX(FailedAt)`**. The Monitoring API alone cannot efficiently satisfy the clarified last-failure definition on SQL Server.

### B. What is the simplest way to obtain the failed job count?

**Recommendation:** `storage.GetMonitoringApi().GetStatistics().Failed`  
**Why:** uncapped count in SQL Server implementation; simpler than relying on `FailedCount()` under default dashboard limits.

Alternative: `FailedCount()` after setting `DashboardJobListLimit` to `null` (or accepting the cap).

### C. What is the simplest and most reliable way to obtain the latest failed job date?

Given the clarified requirement (`MAX(FailedAt)`):

**Recommendation:** narrow read-only SQL for `MAX(State.CreatedAt)` among current Failed jobs (same timestamp source SQL Server Monitoring uses for `FailedJobDto.FailedAt`).

**Not recommended for this definition:** `FailedJobs(0, 1)` (Job Id ordering, not chronological max).

**Technically correct but impractical via public API only:** page all `FailedJobs` and compute `Max(FailedAt)` client-side.

Details: [Determining the actual latest failure](#determining-the-actual-latest-failure).

### D. Can we create one independent Hangfire storage/monitoring instance per configured application?

**Yes (**Fact**).** Construct one `SqlServerStorage` (and thus one `IMonitoringApi`) per connection string/options. Do not route multi-app monitoring solely through `JobStorage.Current`.

### E. Does monitoring require a running Hangfire server?

**No (**Fact**).**

### F. Does the monitoring API modify Hangfire storage?

**MVP read methods: no writes observed (**Fact**).**  
**Storage construction with default options: can write schema (**Fact**).** Disable schema prep.

### G. What NuGet packages and versions would you recommend for a .NET 9 application?

**Recommendation:**

- `Hangfire.Core` **1.8.25**
- `Hangfire.SqlServer` **1.8.25**
- `Microsoft.Data.SqlClient` (current stable)

Do not take `Hangfire.AspNetCore` solely for monitoring.

### H. What are the main limitations or risks of this approach?

See [Risks and limitations](#risks-and-limitations). Highest impact for MVP: **Id-based ordering for “latest failure”**, **schema migration default**, and **FailedCount caps**.

### I. Would this architecture make sense if Hangfire Monitor eventually became part of the Hangfire ecosystem?

**Mostly yes (**Recommendation**).** Prefer public `IMonitoringApi` wherever it is semantically sufficient (counts, lists, dashboard-aligned views). A narrow SQL gap for `MAX(FailedAt)` is an honest limitation of the current public API surface and could motivate a future Hangfire API addition. Avoid broad ad-hoc SQL; keep the fallback minimal and documented.

---

## Recommendation

Proceed with a **hybrid** MVP data-access model:

1. **`Hangfire.Core` + `Hangfire.SqlServer`** (pinned matching 1.8.x versions) for storage construction and public monitoring count APIs
2. **One `SqlServerStorage` per monitored application**
3. **`GetStatistics().Failed` for failed-job counts** (efficient public API)
4. **Narrow read-only SQL for exact `MAX(FailedAt)`**, using the same timestamp source as SQL Server Monitoring (`State.CreatedAt` for the current Failed state)
5. **`PrepareSchemaIfNecessary = false`** mandatory
6. **No Hangfire server** in the monitor process
7. **Do not** use `FailedJobs(0, 1)` as “last failure” under the clarified MVP definition
8. **Do not** relax the requirement to Dashboard-style Job Id ordering

Explicit answers for the clarified requirement:

| Decision | Answer |
| --- | --- |
| Exact `MAX(FailedAt)` using **only** public Hangfire APIs efficiently? | **No** |
| Exact `MAX(FailedAt)` using only public APIs at all? | **Only** by scanning all failed jobs via `FailedJobs` pagination — correct but impractical |
| Is direct SQL justified? | **Yes**, for SQL Server MVP chronological accuracy |
| Relax to Dashboard-style Job Id semantics? | **No** (rejected by MVP definition) |
| Expected performance | Count: cheap aggregate; last failure SQL: one aggregate join; avoid full failed-list materialization |

Keep SQL narrowly scoped and documented so the rest of the architecture remains Hangfire-API-aligned and portable where the public API is sufficient.

---

## Open questions

1. For SQL `MAX`, is `State.CreatedAt` (Monitoring-equivalent) acceptable as `FailedAt`, or must the JSON `State.Data.FailedAt` value be used even if slightly more expensive to extract?
2. Will all monitored apps use the default `HangFire` schema, or must schema be configurable per app?
3. What Hangfire versions/schemas exist in the target estate (1.7 vs 1.8, schema version)?
4. Should SQL credentials be read-only by policy?
5. Should the monitor tolerate partial failures when one of N storages is unreachable?
6. Is exception detail needed in the first MVP screen, or only count + last failure timestamp?
7. Should `DashboardJobListLimit` be nulled globally for accurate `FailedCount()` if that API is used later?
8. License posture if contributing near the Hangfire ecosystem (this repo’s GPL-3.0 vs Hangfire LGPL/commercial dual-license)?
9. Acceptable upper bound for failed-job volume per storage (informs whether any API-scan fallback is ever viable)?

---

## Source references

- [`JobStorage.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/JobStorage.cs)
- [`IMonitoringApi.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/Storage/IMonitoringApi.cs)
- [`FailedJobDto.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/Storage/Monitoring/FailedJobDto.cs)
- [`JobList.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/Storage/Monitoring/JobList.cs)
- [`StatisticsDto.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/Storage/Monitoring/StatisticsDto.cs)
- [`FailedState.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/States/FailedState.cs)
- [`FailedJobsPage.cshtml`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/Dashboard/Pages/FailedJobsPage.cshtml)
- [`SqlServerStorage.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/SqlServerStorage.cs)
- [`SqlServerStorageOptions.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/SqlServerStorageOptions.cs)
- [`SqlServerMonitoringApi.cs`](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/SqlServerMonitoringApi.cs)
- [`Install.sql` / schema indexes](https://github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.SqlServer/Install.sql)
- Issue discussion on monitoring order inconsistencies: [Hangfire#2160](https://github.com/HangfireIO/Hangfire/issues/2160)
- [Using SQL Server (docs)](https://docs.hangfire.io/en/latest/configuration/using-sql-server.html)
- [NuGet: Hangfire.SqlServer 1.8.25](https://www.nuget.org/packages/Hangfire.SqlServer/1.8.25)
- [Hangfire licenses](https://www.hangfire.io/licenses.html)
