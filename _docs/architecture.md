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
