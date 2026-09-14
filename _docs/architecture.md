# Hangfire Monitor — Architecture notes

**Status:** Living notes  
**Related:** `_docs/SPEC.md`, `_docs/plan.md`, `AGENTS.md`

---

## Solution layout (HM-010)

```text
Hangfire.Monitor.Web  →  Hangfire.Monitor.Domain
Hangfire.Monitor.Tests → Hangfire.Monitor.Domain
Hangfire.Monitor.Tests → Hangfire.Monitor.Web
```

`Hangfire.Monitor.Domain` holds Hangfire Monitor’s own concepts as simple POCOs. No Infrastructure project yet; Hangfire/SQL Server integration placement is deferred.

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
