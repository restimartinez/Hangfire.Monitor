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
