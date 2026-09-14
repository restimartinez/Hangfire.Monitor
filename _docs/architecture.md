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
- Blank/whitespace schema normalization and required-field validation are deferred to HM-012.
- Configuration binding is deferred to HM-011.
