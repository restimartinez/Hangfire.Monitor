# Hangfire Monitor — Product Specification

**Status:** Initial MVP specification  
**Date:** 2026-09-14  
**Related:** `_docs/hangfire-monitoring-api-investigation.md`, `AGENTS.md`

This document defines what Hangfire Monitor is for the first MVP. It does not describe implementation steps or application code.

---

## 1. Product

### 1.1 Name

Hangfire Monitor

### 1.2 Purpose

A lightweight web application for monitoring the health of multiple Hangfire applications/storages.

The first MVP is intentionally small: show, for each configured Hangfire application, how many jobs are currently failed and when the most recent actual failure occurred.

### 1.3 Users

Operators and developers who need a single place to see failed-job health across several Hangfire SQL Server storages.

---

## 2. Functional requirements

### 2.1 Multi-application monitoring

The MVP must support multiple Hangfire applications from the first version.

Applications are configured externally. There is **no** configuration administration UI.

Each configured application has:

| Field | Required | Description |
| --- | --- | --- |
| `Name` | Yes | Human-readable application label shown in the UI |
| `ConnectionString` | Yes | Hangfire SQL Server connection string for that application |
| `Schema` | No | Hangfire SQL schema name; defaults to `HangFire` |

Configuration conceptually follows:

```json
{
  "HangfireMonitor": {
    "Applications": [
      {
        "Name": "App1 Name",
        "ConnectionString": "...",
        "Schema": "HangFire"
      }
    ]
  }
}
```

Actual connection strings must not be committed. See [Local development secrets](#36-security-decisions-mvp).

### 2.2 Per-application status data

For each configured application, the MVP must obtain and display:

1. **Application name**
2. **Number of failed jobs**
3. **Date/time of the most recent actual failure**
4. **Monitoring status** (`OK`, `FAILED`, or `UNAVAILABLE`)

### 2.3 Meaning of “most recent actual failure”

“Most recent actual failure” means:

```text
MAX(FailedAt)
```

among jobs currently in the Failed state.

It must **not** mean the failed job with the highest Hangfire Job Id.

#### Zero failures

When there are zero failed jobs, display:

| Field | Display |
| --- | --- |
| Failed jobs | `0` |
| Last failure | `-` |

### 2.4 Monitoring outcomes

A failure to connect to or read one monitored Hangfire database must **not** prevent monitoring of the other applications.

Use these three conceptual statuses:

| Status | Meaning |
| --- | --- |
| `OK` | Storage is available and there are zero failed jobs |
| `FAILED` | Storage is available and one or more failed jobs exist |
| `UNAVAILABLE` | The storage could not be queried |

`FAILED` refers to failed Hangfire **jobs**, not a failure of the Hangfire storage itself. Storage/query failures are represented as `UNAVAILABLE`.

### 2.5 User interface

- Use **ASP.NET Core Razor Pages**.
- The initial page is a simple status table.
- Conceptual columns:

  | Application | Failed jobs | Last failure |

- Status (`OK` / `FAILED` / `UNAVAILABLE`) must be distinguishable in the UI (exact presentation is left to implementation, provided the three outcomes remain clear).
- When a last-failure timestamp exists, display it using a clear local date/time format such as:

  `14/09/2026 11:42:37`

- Prioritize clarity and simplicity.
- No SPA.
- No React, Angular, or Vue.
- No REST API unless explicitly required later.

Visual design details beyond what is needed for a clear MVP table are out of scope for this specification.

---

## 3. Technical decisions

### 3.1 Framework and stack

| Decision | Choice |
| --- | --- |
| Runtime | .NET 9 |
| Language | C# |
| Web framework | ASP.NET Core |
| UI | Razor Pages |
| Monitored storage (MVP) | SQL Server Hangfire storages |
| Test framework | xUnit |

### 3.2 Hangfire integration

Preferred integration for Hangfire concepts and count:

```text
JobStorage → IMonitoringApi
```

| Metric | Decision |
| --- | --- |
| Failed job count | `IMonitoringApi.GetStatistics().Failed` |
| Exact latest failure timestamp | Narrowly scoped **read-only** SQL Server aggregate that yields `MAX(FailedAt)` semantics |

Rationale (from investigation): the public Monitoring API cannot efficiently provide `MAX(FailedAt)`. `FailedJobs(0, 1)` is ordered by Job Id on SQL Server and must not be used as “last failure” for this product.

The SQL query used for last failure must retrieve **only** the aggregate required to determine the latest failure. It must not load job lists, arguments, exceptions, or other job payloads.

#### FailedAt SQL mapping (implementation verification item)

Do **not** treat `State.CreatedAt` → `FailedAt` as permanently established yet.

During implementation, verify against the Hangfire SQL Server implementation/schema that the SQL used for the exact latest failure corresponds to the **same timestamp semantics** as `FailedJobDto.FailedAt`.

Until that verification is complete, the concrete SQL column expression remains an implementation detail constrained by this semantic requirement.

### 3.2.1 Timestamp handling

- Preserve the timestamp returned by the monitoring/storage layer internally.
- Do **not** perform business-level timezone conversions in the monitoring logic.
- For the MVP UI, display the timestamp using a clear local date/time format such as `14/09/2026 11:42:37`.
- Timezone handling can be enhanced later.

### 3.3 Initial package versions

Initial dependency decision (pinned for MVP unless explicitly changed later):

| Package | Version |
| --- | --- |
| Hangfire.Core | 1.8.25 |
| Hangfire.SqlServer | 1.8.25 |
| Microsoft.Data.SqlClient | current stable compatible with the above |

Hangfire.Core and Hangfire.SqlServer versions must remain matched.

### 3.4 Architecture principles

The application should be simple and testable.

Separate concerns at a practical level:

- configuration
- Hangfire monitoring access
- application/domain logic
- presentation (Razor Pages)

Avoid speculative abstractions.

Do **not** introduce repositories, generic data-access layers, CQRS, MediatR, or similar patterns unless a concrete requirement justifies them.

### 3.5 Read-only Hangfire access

The application must **never** modify Hangfire storage.

In particular:

- Do not enqueue, retry, delete, or otherwise mutate jobs.
- Do not install or migrate Hangfire schema objects as part of monitoring (for example `PrepareSchemaIfNecessary` must not be left enabled in a way that writes to monitored databases).
- Prefer read-only database credentials where operationally possible.

### 3.6 Security decisions (MVP)

| Topic | Decision |
| --- | --- |
| Hangfire storage operations | Read-only |
| Connection strings in source control | Never commit connection strings / secrets |
| Authentication | Not required for the first MVP |
| Authorization | Not required for the first MVP |

Authentication may be considered later.

#### Local development secrets

For local development:

- `appsettings.json` may contain non-secret configuration.
- Connection strings and other secrets must use **User Secrets** or **environment variables**.
- Secrets must never be committed to source control.
- Do **not** add production secret-management infrastructure to the MVP.

### 3.7 Testing decisions

- Use **xUnit**.
- Core monitoring logic must be unit-testable without production Hangfire databases.
- Integration tests may be added later.
- Do **not** introduce Testcontainers in the initial implementation unless a concrete testing requirement justifies them.

---

## 4. Constraints

1. MVP scope stays limited to the status table described above.
2. Multiple applications are configured externally; no config UI.
3. Last failure is chronological `MAX(FailedAt)`, not highest Job Id.
4. One unavailable storage must not block the whole page.
5. No writes to Hangfire storage.
6. Connection strings must not be stored in source control.
7. Razor Pages only for UI in the MVP.
8. Keep the design simple, maintainable, and testable; avoid overengineering.
9. Do not implement functionality that has not been requested.

---

## 5. Out of scope (MVP)

The following are **not** part of the MVP:

- job retry
- job deletion
- job enqueueing
- job details
- exception details
- job arguments
- queue management
- authentication
- authorization
- notifications
- email
- Teams/Slack integration
- metrics/history
- charts
- automatic refresh
- configuration UI
- monitoring of non-Hangfire applications
- monitoring of arbitrary HTTP endpoints
- REST API
- SPA
- distributed architecture

---

## 6. Future possibilities

Future versions **may** add:

- richer failed-job information
- recurring job monitoring
- queues
- processing / enqueued / scheduled statistics
- HTTP / application health checks
- notifications
- authentication
- historical metrics
- additional Hangfire storage providers
- possible integration with the wider Hangfire ecosystem

These are possibilities only. They must **not** influence MVP implementation unless explicitly requested.

---

## 7. Acceptance criteria

The MVP is acceptable when all of the following are true:

1. The application can monitor multiple configured Hangfire SQL Server storages.
2. The UI displays each configured application.
3. Failed job count matches Hangfire’s monitoring statistics (`GetStatistics().Failed`).
4. Last failure represents the actual maximum failure timestamp (`MAX(FailedAt)` semantics), not the highest Job Id.
5. One unavailable storage does not prevent other applications from being displayed.
6. Applications are shown with status `OK`, `FAILED`, or `UNAVAILABLE` as defined above.
7. Zero failed jobs display count `0` and last failure `-`.
8. No write operation is performed against Hangfire storage.
9. Connection strings are not stored in source control.
10. Automated tests cover the important application logic.
11. The application builds successfully.
12. The application can run locally with appropriate configuration (User Secrets or environment variables for connection strings).

---

## 8. Closed decisions and remaining verification

### 8.1 Decisions now closed

The following MVP ambiguities are resolved:

1. **Configuration shape** — `HangfireMonitor:Applications[]` with required `Name`, required `ConnectionString`, optional `Schema` (default `HangFire`).
2. **Timestamp display** — preserve storage timestamp internally; no business-level timezone conversion in monitoring logic; MVP UI uses a clear local format such as `14/09/2026 11:42:37`.
3. **Status values** — `OK`, `FAILED`, `UNAVAILABLE` with the meanings defined above.
4. **Zero failures display** — count shows `0`, last failure shows `-`.
5. **Local secrets** — non-secrets may live in `appsettings.json`; connection strings/secrets use User Secrets or environment variables; never commit secrets; no production secret infrastructure in MVP.

### 8.2 Remaining implementation verification item

**FailedAt SQL mapping:** verify during implementation, against the Hangfire SQL Server implementation/schema, that the SQL used for the exact latest failure corresponds to the same timestamp semantics as `FailedJobDto.FailedAt`.

Do not treat `State.CreatedAt` → `FailedAt` as permanently established until that verification is done.
