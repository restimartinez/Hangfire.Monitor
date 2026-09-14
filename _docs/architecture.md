# Hangfire Monitor — Architecture notes

**Status:** Living notes  
**Related:** `_docs/SPEC.md`, `_docs/plan.md`, `AGENTS.md`

---

## Solution layout

```text
Hangfire.Monitor.Web            → Hangfire.Monitor.Infrastructure
Hangfire.Monitor.Web            → Hangfire.Monitor.Domain
Hangfire.Monitor.Infrastructure → Hangfire.Monitor.Domain
Hangfire.Monitor.Tests          → Hangfire.Monitor.Web
Hangfire.Monitor.Tests          → Hangfire.Monitor.Domain
```

| Project | Role |
| --- | --- |
| `Hangfire.Monitor.Domain` | Domain/configuration models (POCOs) |
| `Hangfire.Monitor.Infrastructure` | Hangfire SQL Server integration |
| `Hangfire.Monitor.Web` | Hosting, presentation, and application composition |
| `Hangfire.Monitor.Tests` | Automated tests |

---

## Configuration model (HM-010)

Typed options live in `Hangfire.Monitor.Domain`:

| Type | Role |
| --- | --- |
| `HangfireMonitorOptions` | Root (`HangfireMonitor`); holds `Applications` |
| `HangfireApplicationOptions` | One monitored app: `Name`, `ConnectionString`, `Schema` |

### Schema default

`HangfireApplicationOptions.Schema` defaults to `HangFire` via the property initializer (and `DefaultSchema` constant) on the model itself.

- Missing / unset schema → `HangFire` at construction time.
- No extra Schema validation beyond that default.

---

## Configuration binding (HM-011)

`Hangfire.Monitor.Web` binds the `HangfireMonitor` configuration section to `HangfireMonitorOptions` with standard ASP.NET Core Options (`AddOptions` + `Bind`).

Consumers resolve `IOptions<HangfireMonitorOptions>` from DI.

---

## Configuration validation (HM-012)

Validation lives in `Hangfire.Monitor.Web` as `HangfireMonitorOptionsValidator` (`IValidateOptions<HangfireMonitorOptions>`), not in Domain: the Options validation API is a hosting concern and Domain stays free of `Microsoft.Extensions.Options`.

Rules:

- Per application: `Name` and `ConnectionString` required (non-whitespace).
- `Schema` optional (model default `HangFire`).
- Empty `Applications` list is valid.

Registration uses `ValidateOnStart()` so invalid config fails at host startup, before serving requests. `appsettings.json` keeps an empty `Applications` list; real connection strings belong in User Secrets / environment variables (HM-013).

---

## Per-application SqlServerStorage (HM-021)

`Hangfire.Monitor.Infrastructure.Storage.SqlServerStorageFactory` maps `HangfireApplicationOptions` → a new `SqlServerStorage` instance. Hangfire.Core / Hangfire.SqlServer package references live on Infrastructure.

| Setting | Value | Why |
| --- | --- | --- |
| `PrepareSchemaIfNecessary` | `false` | Monitor must never install/migrate Hangfire schema |
| `SchemaName` | `application.Schema` (default `HangFire`) | Match the monitored app’s Hangfire schema |
| `TryAutoDetectSchemaDependentOptions` | `false` | Avoid opening a SQL connection during storage construction |

Multi-app model: one `SqlServerStorage` per configured application. Do **not** use `JobStorage.Current`, `AddHangfire()`, or a Hangfire Server for this purpose. The factory is registered as a singleton in DI (from Web); storages are created on demand, not as a single global storage.

---

## Failed job count (HM-022)

`Hangfire.Monitor.Infrastructure.Storage.FailedJobCountReader` reads the failed-job total from an existing `SqlServerStorage`:

```text
storage.GetMonitoringApi().GetStatistics().Failed
```

Use `IMonitoringApi.GetStatistics().Failed`, **not** `FailedCount()`. On SQL Server storage, `FailedCount()` can be capped by `DashboardJobListLimit` (default 10,000); `GetStatistics().Failed` is the uncapped aggregate count.

Exceptions from storage/monitoring propagate to the caller (no `UNAVAILABLE` mapping yet).

---

## Monitoring error surface (HM-023)

Investigated against **Hangfire.Core / Hangfire.SqlServer 1.8.25** (installed packages), focusing on:

```text
SqlServerStorage.GetMonitoringApi()
→ IMonitoringApi.GetStatistics()
→ StatisticsDto.Failed
```

### What Hangfire does on this path

| Step | Behavior (1.8.25) |
| --- | --- |
| `GetMonitoringApi()` | Constructs `SqlServerMonitoringApi` in memory. Does **not** open a SQL connection and does **not** wrap failures. |
| `GetStatistics()` | Opens a connection via `SqlServerStorage.UseConnection` / `CreateAndOpenConnection`, runs a multi-result `SELECT` batch (Dapper `QueryMultiple`), then may also enumerate queue providers for `Queues`. **No try/catch** around the statistics queries. |
| Connection open failure | `CreateAndOpenConnection` catches only to dispose the connection, then **rethrows the original exception** (no Hangfire wrapper). |
| Success result | Always a normal `StatisticsDto`. There is **no** null, status flag, or sentinel value meaning “storage unavailable”. |

So unavailability is expressed **only by throwing**, never by a special return value from `GetStatistics()`.

### Exceptions identified

**Relevant to SQL Server access failures on this path (typical):**

- `System.Data.Common.DbException` and derived provider exceptions (e.g. `Microsoft.Data.SqlClient.SqlException` / `System.Data.SqlClient.SqlException` once a SQL client package is present): connection failures, login failures, timeouts surfaced by the provider, missing schema/objects, permission errors, query failures.
- Any other exception thrown by the ADO.NET provider or Dapper while opening/querying — Hangfire does **not** map these to a Hangfire-specific type on the monitoring statistics path.

**Hangfire-specific exception types that are not the monitoring-count failure surface:**

| Type | Relevance to `GetStatistics().Failed` |
| --- | --- |
| `Hangfire.Common.JobLoadException` | Used when deserializing job payloads in list/detail APIs — **not** on the `GetStatistics()` aggregate path. |
| `Hangfire.BackgroundJobClientException` / client create failures | Client enqueue path — **not** monitoring. |
| `Hangfire.SqlServer.SqlServerDistributedLockException` | Distributed lock path — **not** `GetStatistics()`. |

**Programming / hosting defects (must not be treated as per-app `UNAVAILABLE`):**

- `ArgumentNullException` from our own null guards.
- `InvalidOperationException` from Hangfire when no `SqlClientFactory` is configured (`Microsoft.Data.SqlClient` / `System.Data.SqlClient` missing) — deployment/configuration problem for the monitor host, not an individual monitored storage being down.

### Decision (boundary contract for later `UNAVAILABLE`)

1. **Representation at the Hangfire-integration boundary:** keep **exception propagation**. `FailedJobCountReader` does not catch or translate errors. No result-type / `UNAVAILABLE` mapping inside the reader (that belongs to later orchestration, e.g. HM-050).
2. **Do not invent** a Hangfire Monitor–specific exception hierarchy for SQL monitoring failures; Hangfire 1.8.25 does not provide a reliable dedicated type for this path.
3. **Future translation to `UNAVAILABLE` (planned, not implemented here):** at the multi-app orchestration boundary, treat **data-access / connection failures** as unavailable storage. Prefer catching **`DbException`** as the stable, provider-agnostic signal for SQL Server access/query failures. Do **not** catch blanket `Exception` unless a later task proves a concrete gap (and documents it).
4. **Do not map** configuration/programming errors (`ArgumentNullException`, missing SQL client factory, etc.) to `UNAVAILABLE`.
5. **Limitation:** without a live SQL Server, the exact provider exception type for every failure mode cannot be enumerated exhaustively here; the important fact from 1.8.25 source is that Hangfire **rethrows** underlying access errors rather than wrapping them for `GetStatistics()`.

Existing unit test `FailedJobCountReaderTests.GetFailedCount_PropagatesException_FromMonitoringApi` documents the current boundary: monitoring failures bubble unchanged.

---

## FailedAt timestamp semantics (HM-030)

Verified against **Hangfire.Core / Hangfire.SqlServer 1.8.25** (tag `v1.8.25` / installed NuGet packages). This closes the SPEC verification item: do **not** assume the mapping until checked against this version.

### Code path inspected

```text
IMonitoringApi.FailedJobs(from, count)
  → SqlServerMonitoringApi.FailedJobs
  → GetJobs(..., stateName: FailedState.StateName ("Failed"), descending: true, selector)
  → SQL over [schema].Job + [schema].State
  → SqlJob.StateChanged
  → FailedJobDto.FailedAt = sqlJob.StateChanged
```

Sources (v1.8.25):

- `Hangfire.SqlServer/SqlServerMonitoringApi.cs` — `FailedJobs`, `GetJobs`
- `Hangfire.SqlServer/Entities/SqlJob.cs` — `StateChanged` property
- `Hangfire.Core/Storage/Monitoring/FailedJobDto.cs` — `FailedAt`
- `Hangfire.Core/States/FailedState.cs` — `FailedAt = DateTime.UtcNow` + `SerializeData()["FailedAt"]`
- `Hangfire.SqlServer/SqlServerWriteOnlyTransaction.cs` — `SetJobState` inserts `State.CreatedAt`
- `Hangfire.SqlServer/Install.sql` — `[State].[CreatedAt] [datetime] NOT NULL`

### SQL column and alias

In `GetJobs`, the monitoring query selects:

```sql
s.CreatedAt as StateChanged
```

joined as the **current** state row:

```sql
left join [{schema}].State s
  on j.StateId = s.Id and j.Id = s.JobId
```

with jobs filtered by:

```sql
where j.StateName = @stateName  -- "Failed" for FailedJobs
```

Dapper maps the alias `StateChanged` onto `SqlJob.StateChanged`. The Failed selector then sets:

```csharp
FailedAt = sqlJob.StateChanged
```

| Name seen by code | Physical column |
| --- | --- |
| `FailedJobDto.FailedAt` | — (DTO property) |
| `SqlJob.StateChanged` | SQL alias |
| `s.CreatedAt` / `[State].[CreatedAt]` | Physical column |

**Not** used for `FailedJobDto.FailedAt` on this path:

- `[Job].[CreatedAt]` (job creation time)
- parsing `State.Data` JSON key `FailedAt` (that value exists, but list monitoring does not read it for the DTO timestamp)

### Semantic meaning

`[State].[CreatedAt]` is the timestamp when the **state history row** was inserted. For a job whose **current** state is Failed, that is the moment the job **entered the Failed state** (state transition time), not job creation and not an arbitrary later update.

When Hangfire persists a state (`SetJobState`), it writes:

```csharp
@createdAt = DateTime.UtcNow
```

into `[State].[CreatedAt]`. Independently, `FailedState` sets `FailedAt = DateTime.UtcNow` at construction and stores it inside `State.Data["FailedAt"]`. Those two `UtcNow` calls are sequential and can differ by milliseconds; **SQL Server Monitoring’s public DTO uses `State.CreatedAt`**, not the JSON field.

### Conclusion for MVP `last failure`

`FailedJobDto.FailedAt` **does** represent the failure-time semantics the MVP needs (time of entry into Failed for jobs currently failed).

The Monitoring-equivalent aggregate is:

```text
MAX([State].[CreatedAt])
```

among jobs **currently** in Failed, i.e. constrained like Hangfire’s join to the current state (via `Job.StateId` / `Job.StateName = N'Failed'`), **not** `MAX(Job.Id)` and **not** `FailedJobs(0, 1)`.

---

## Latest failure SQL (HM-031)

Split of read paths:

| Concern | Mechanism |
| --- | --- |
| Failed job **count** | Monitoring API: `GetStatistics().Failed` (`FailedJobCountReader`) |
| Latest failure **timestamp** | Narrow read-only SQL: `MAX(State.CreatedAt)` (`LastFailedAtReader`) |

`Hangfire.Monitor.Infrastructure.Storage.LastFailedAtReader` runs:

```sql
SELECT MAX(s.[CreatedAt])
FROM [{schema}].[Job] AS j
INNER JOIN [{schema}].[State] AS s
    ON j.[StateId] = s.[Id] AND j.[Id] = s.[JobId]
WHERE j.[StateName] = N'Failed'
```

Semantics: `null` when no job is currently Failed; otherwise the storage timestamp of the most recent current Failed state (no business timezone conversion).

Schema identifiers follow Hangfire.SqlServer 1.8.25 quoting (`]` → `]]` inside `[...]`). The schema comes from the `SqlServerStorage` options (configured via `HangfireApplicationOptions.Schema`).

Connection: Hangfire.SqlServer **1.8.25** does not expose a public `DbConnection` opener on `SqlServerStorage` (`UseConnection` / `CreateAndOpenConnection` are internal; `GetConnection()` is Hangfire’s `IStorageConnection`, not ADO.NET). Do not use `JobStorage.Current`.

#### Reflection on Hangfire internals (maintenance risk)

Against **Hangfire.SqlServer 1.8.25**, `SqlServerStorageDb` uses reflection to call these non-public members:

```text
SqlServerStorage.UseConnection<TResult>(
    DbConnection,
    Func<SqlServerStorage, DbConnection, TResult>)

SqlServerStorage.Options
```

**Why:** reuse Hangfire’s managed connection path (factory, open/release, existing-connection handling) instead of opening a separate ADO.NET connection and adding another SQL client package.

**Isolation:** reflection is confined to `SqlServerStorageDb`; query construction (`LastFailedAtQuery`) and the public reader (`LastFailedAtReader`) do not reach into Hangfire internals.

**Upgrade risk:** if Hangfire.SqlServer is upgraded later, review this dependency — renames, signature changes, or removal of those internal members can break the latest-failure integration.

---

## Per-application failure read (HM-032)

`Hangfire.Monitor.Infrastructure.Storage.HangfireStorageReader` combines the two read paths for **one** `HangfireApplicationOptions`:

1. `SqlServerStorageFactory.Create` → one `SqlServerStorage`
2. `FailedJobCountReader.GetFailedCount(storage)`
3. `LastFailedAtReader.GetLastFailedAt(storage)` on that **same** instance

Returns Infrastructure `HangfireApplicationFailureInfo` (`FailedCount`, `LastFailedAt`). Exceptions propagate unchanged (no `UNAVAILABLE` mapping yet). No shared transaction between the two reads.

---

## Monitoring status / result model (HM-040)

Domain types (no Hangfire / Infrastructure references):

| Type | Role |
| --- | --- |
| `MonitoringStatus` | `OK`, `FAILED`, `UNAVAILABLE` |
| `ApplicationMonitoringResult` | Per-app outcome: `ApplicationName`, `Status`, `FailedCount`, `LastFailedAt` |

Infrastructure `HangfireApplicationFailureInfo` stays a technical read model. Mapping into `ApplicationMonitoringResult` (including when to set each status) is HM-041+. No error/detail field on the result yet — deferred until `UNAVAILABLE` mapping needs it.

---

## Monitoring business rules (HM-041)

`Hangfire.Monitor.Domain.ApplicationMonitoringRules` maps primitive failure data → `ApplicationMonitoringResult` (no Hangfire / Infrastructure types):

| Input | Status | `FailedCount` | `LastFailedAt` |
| --- | --- | --- | --- |
| `failedCount == 0` | `OK` | `0` | always `null` (even if a timestamp was supplied) |
| `failedCount > 0` | `FAILED` | as supplied | as supplied (not recalculated) |
| `Unavailable(name)` | `UNAVAILABLE` | `0` | `null` |

`Unavailable` is an explicit constructor for that status only. HM-041 does **not** catch SQL/`DbException`; orchestration that decides unavailability comes later.

---

## Multi-application monitoring (HM-050)

`Hangfire.Monitor.Infrastructure.Monitoring.ConfiguredApplicationsMonitor` iterates configured applications in **configuration order** (no alphabetical re-sort):

1. `HangfireStorageReader.Read(application)` → `HangfireApplicationFailureInfo`
2. `ApplicationMonitoringRules.FromFailureInfo(...)` → `OK` / `FAILED`
3. On **`DbException` only** → `ApplicationMonitoringRules.Unavailable(name)` for that app, then continue

| Exception | Behavior |
| --- | --- |
| `DbException` (and derived) | Per-app `UNAVAILABLE`; remaining apps still monitored |
| Other exceptions (`ArgumentNullException`, `InvalidOperationException`, …) | Propagate; do not map to `UNAVAILABLE` |

Results are independent per application: a storage failure for B does not skip C. No DI registration in this task.

### UTC / local time notes

- Hangfire writes `[State].[CreatedAt]` with `DateTime.UtcNow`.
- `FailedState.FailedAt` is also `DateTime.UtcNow` (serialized into state data; not what Monitoring maps to the DTO).
- Schema type is SQL `datetime` (no time-zone info). Values are UTC wall-clock instants as stored by Hangfire; ADO.NET typically surfaces `DateTimeKind.Unspecified` unless the consumer treats them as UTC.
- Per SPEC: preserve the storage timestamp internally; do not apply business-level timezone conversion in monitoring logic. UI local formatting remains a later presentation concern.
