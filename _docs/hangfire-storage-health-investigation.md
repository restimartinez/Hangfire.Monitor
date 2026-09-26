# Hangfire Storage Health — Technical Investigation

**Status:** Investigation only (no application code)  
**Date:** 2026-09-26  
**Related:** `_docs/architecture.md`, `_docs/hangfire-monitoring-api-investigation.md`, `AGENTS.md`  
**Packages in scope:** Hangfire.Core / Hangfire.SqlServer **1.8.25** (current Monitor dependency)

Legend:

| Label | Meaning |
| --- | --- |
| **Fact** | Verified from Hangfire / Microsoft documentation or source |
| **Assumption** | Reasonable inference not fully proven here |
| **Recommendation** | Proposed approach for Hangfire Monitor |

---

## Executive summary

**Recommendation:** Storage Health should be a **read-only SQL metrics layer** separate from Hangfire job monitoring. Queries return **raw metric values**; a later rules layer maps them to `OK` / `WARNING` / `CRITICAL` / `UNAVAILABLE`. Do not treat permission failures as `CRITICAL`.

MVP metric set (reliable enough to implement later):

| Metric | MVP | Source | Confidence |
| --- | --- | ---: | ---: |
| Schema Version | Yes | `[schema].[Schema].[Version]` | High |
| Active SQL Sessions | Yes | `sys.dm_exec_sessions` | High (with caveats) |
| Active Transactions | Yes | `sys.dm_tran_*` + **database filter** | High (after scoping fix) |
| Data Size / Used | Yes | `sys.database_files` + used-space source | High |
| Transaction Log Size / Used | Yes | `sys.dm_db_log_space_usage` | High |
| Log Reuse Wait | Yes | `sys.databases.log_reuse_wait_desc` | High |
| Total Connections / pool metrics | **No** | — | Low (not a single SQL value) |

This phase does **not** implement readers, rules, or UI.

**Next implementation vertical (when requested):** Schema Version only (`SchemaVersionReader` → metric → health rule + tests).

---

## Principles (accepted)

1. Queries are **read-only**.
2. Never run Hangfire schema migrations from Monitor (`PrepareSchemaIfNecessary = false` already enforced).
3. Never modify Hangfire tables.
4. Do not use `JobStorage.Current`.
5. Do not mix SQL Server instance metrics with Hangfire job metrics without clear labeling.
6. Separate **data acquisition** from **health evaluation** (same pattern as `HangfireStorageReader` vs `ApplicationMonitoringRules`).
7. Permission / access failures → `UNAVAILABLE` with explanation, not `CRITICAL`.
8. Thresholds provisional and later configurable.
9. Prefer small, low-impact queries.
10. Unit-test interpretation rules independently of SQL.
11. SQL readers testable without UI.
12. No single metric alone implies a definitive problem without context.

---

## 1. Schema Version

### Fuente (**Fact**)

Hangfire.SqlServer maintains schema version in:

```text
[{schema}].[Schema].[Version]
```

Default schema name is `HangFire`. Monitor already stores the configured schema on `HangfireApplicationOptions.Schema` (default `HangFire`).

Official `Install.sql` sets:

```sql
SET @TARGET_SCHEMA_VERSION = 9;
```

Hangfire 1.8.x (including Monitor’s pinned **1.8.25**) targets schema **9**. Migration 8→9 adds `IX_HangFire_State_CreatedAt` on `[State](CreatedAt)`.

Hangfire refuses to migrate when current version **>** target (install script rolls back / returns without applying).

### Consulta (**Recommendation**)

```sql
SELECT [Version]
FROM [{schema}].[Schema];
```

Use the **configured** schema (escaped like `LastFailedAtQuery`), never a hardcoded `HangFire` identifier in production code.

### Significado

Integer Hangfire SQL storage schema version present in that database. It is **not** the Hangfire NuGet package version string, though package releases introduce target schema numbers.

### Evaluación propuesta

| Condition | Status |
| --- | --- |
| Actual == Expected | `OK` |
| Actual < Expected | `WARNING` (schema behind expected) |
| Actual > Expected | `WARNING` / review (DB newer than expected; Monitor must not migrate) |
| Table missing / query fails (object) | `UNAVAILABLE` or treat as storage unreadability (orchestration decision later) |

**Do not** assume every lower version is incompatible with all Hangfire clients. Older schemas can still run older servers; the rule should compare against an **explicit expected version**.

### Expected version source (**Open decision**)

| Option | Pros | Cons |
| --- | --- | --- |
| A. Constant tied to Monitor’s Hangfire.SqlServer package (9 for 1.8.25) | Simple | Wrong if a monitored app runs a different Hangfire major/schema target |
| B. Per-application config (`ExpectedSchemaVersion`) | Accurate multi-app estates | Extra config |
| C. Default 9 + optional per-app override | Practical MVP | Slight config surface |

**Recommendation:** **C** — default expected version `9` for the current Monitor package era, overridable later per application. Document that expected ≠ “migrate to this”.

### Permisos

Requires `SELECT` on `[{schema}].[Schema]`. No DMV / `VIEW SERVER STATE` needed. Aligns with existing read-only Hangfire table access pattern.

### Limitaciones / falsos positivos

- Empty result / missing table → schema never installed or wrong schema name (config mismatch), not necessarily “version 0”.
- Multiple rows: Hangfire uses a single version row as primary key on `Version`; treat unexpected cardinality as anomaly.
- Actual > Expected can be healthy if apps were upgraded ahead of Monitor’s default expectation.

### Utilidad diagnóstica

**High** for detecting schema drift before blaming job failures on storage layout. Complements existing failed-job monitoring; does not replace it.

### Connection path (**Recommendation**)

Reuse `SqlServerStorageDb.UseConnection` (same as `LastFailedAtReader`) so Monitor does not open a parallel ADO.NET path or set `JobStorage.Current`.

---

## 2. Active SQL Sessions

### Fuente (**Fact**)

`sys.dm_exec_sessions` — one row per authenticated session. Relevant columns: `session_id`, `database_id`, `login_name`, `host_name`, `program_name`, `login_time`, `status`, `open_transaction_count`, `is_user_process`.

### Consulta inicial (**Recommendation**)

```sql
SELECT COUNT(*) AS ActiveSessions
FROM sys.dm_exec_sessions
WHERE database_id = DB_ID()
  AND is_user_process = 1;
```

Diagnostic detail (not required for the aggregate metric):

```sql
SELECT
    session_id,
    status,
    login_name,
    host_name,
    program_name,
    login_time,
    open_transaction_count
FROM sys.dm_exec_sessions
WHERE database_id = DB_ID()
  AND is_user_process = 1
ORDER BY login_time;
```

### Naming (**Decision**)

Call this **Active SQL Sessions**, **not** “Hangfire Connections”.

Sessions with `database_id = DB_ID()` may include Hangfire workers, Dashboard, Hangfire Monitor itself, SSMS, agents, and other apps sharing the database.

### Significado (**Fact** + caveat)

`database_id` is the session’s **current database context**, not “has ever touched this database” and not “ADO.NET pool size”.

| Risk | Effect |
| --- | --- |
| Session in `master` running three-part-name queries against Hangfire DB | **Under-count** |
| Idle pooled sessions still showing Hangfire DB context | Count includes sleeping pool members |
| Monitor’s own session | Included in the count |
| Without server-state permission | Often only **own** session visible → misleading low count |

### Permisos (**Fact**)

| Platform | Permission to see all sessions |
| --- | --- |
| SQL Server ≤ 2019 | `VIEW SERVER STATE` |
| SQL Server 2022+ | `VIEW SERVER PERFORMANCE STATE` |
| Azure SQL Database | `VIEW DATABASE STATE` (to see all connections to current DB) |

Without elevated permission: everyone can see **their own** session → metric may look “healthy” while blind.

**Recommendation:** if the query succeeds but elevated visibility is known-absent, prefer `UNAVAILABLE` or an explicit “partial visibility” flag rather than a confident low count. Exact detection strategy left to implementation (permission probe vs documenting required grants).

### Evaluación MVP

No absolute threshold for session count in the first rules proposal. Treat as **informational** until Hangfire-specific session identification is studied. Do not alert on count alone.

### Utilidad

**Medium** for capacity / “who is connected” diagnosis; **low** as a lone health signal.

---

## 3. Total Connections (excluded)

### Decisión (**Accepted**)

**Out of MVP.** There is no single SQL value that equals:

- historical connections created;
- ADO.NET connection pool size / usage;
- Hangfire process pool limits;
- “Hangfire connections” as a product concept.

SQL Server exposes **sessions** (and separately **connections** via `sys.dm_exec_connections`), which still are not the .NET pool.

**Recommendation:** keep **Active SQL Sessions** only; revisit Hangfire-filtered sessions later via `program_name` / login / host heuristics (with documented false positives).

---

## 4. Active Transactions

### Fuentes (**Fact**)

| View | Role |
| --- | --- |
| `sys.dm_tran_active_transactions` | Instance-level active transactions (`transaction_begin_time`, `transaction_type`, `transaction_state`) |
| `sys.dm_tran_session_transactions` | Map transaction ↔ session (a transaction may map to multiple sessions) |
| `sys.dm_tran_database_transactions` | **Database-scoped** involvement (`database_id`, log bytes, begin time in DB) |
| `sys.dm_exec_sessions` | Session attributes |

`transaction_state = 2` means **active** (**Fact**, Microsoft docs).

Microsoft notes `open_transaction_count` on sessions may **not** match `sys.dm_tran_session_transactions`.

### Critical correction vs naive instance count (**Fact**)

`sys.dm_tran_active_transactions` is **instance-scoped**.  
`COUNT(*) ... WHERE transaction_state = 2` without a database filter counts transactions for **all databases** on the instance → **false positives** for Hangfire storage health.

### Consulta inicial recomendada (database-scoped)

```sql
SELECT COUNT(*) AS ActiveTransactions
FROM sys.dm_tran_active_transactions AS at
INNER JOIN sys.dm_tran_database_transactions AS dt
    ON dt.transaction_id = at.transaction_id
WHERE at.transaction_state = 2
  AND dt.database_id = DB_ID();
```

Derived metrics:

```sql
SELECT
    COUNT(*) AS ActiveTransactions,
    MIN(at.transaction_begin_time) AS OldestTransactionBegin,
    MAX(DATEDIFF(SECOND, at.transaction_begin_time, SYSUTCDATETIME())) AS OldestDurationSeconds
FROM sys.dm_tran_active_transactions AS at
INNER JOIN sys.dm_tran_database_transactions AS dt
    ON dt.transaction_id = at.transaction_id
WHERE at.transaction_state = 2
  AND dt.database_id = DB_ID();
```

Prefer `SYSUTCDATETIME()` / document clock basis; Hangfire storage timestamps elsewhere are UTC-oriented.

Diagnostic (optional detail query):

```sql
SELECT
    at.transaction_id,
    at.transaction_begin_time,
    DATEDIFF(SECOND, at.transaction_begin_time, SYSUTCDATETIME()) AS DurationSeconds,
    at.transaction_type,
    at.transaction_state,
    st.session_id,
    s.login_name,
    s.host_name,
    s.program_name,
    s.status
FROM sys.dm_tran_active_transactions AS at
INNER JOIN sys.dm_tran_database_transactions AS dt
    ON dt.transaction_id = at.transaction_id
LEFT JOIN sys.dm_tran_session_transactions AS st
    ON st.transaction_id = at.transaction_id
LEFT JOIN sys.dm_exec_sessions AS s
    ON s.session_id = st.session_id
WHERE at.transaction_state = 2
  AND dt.database_id = DB_ID()
ORDER BY at.transaction_begin_time;
```

### Métricas derivadas (**Recommendation**)

| Metric | Why |
| --- | --- |
| Active Transaction Count | Context only |
| Oldest Transaction Begin | Absolute time |
| Oldest Transaction Duration | **Primary alert input** |

Example display: `Active Transactions: 2`, `Oldest: 00:07:31`.

### Evaluación provisional (thresholds not final)

| Oldest duration | Status |
| --- | --- |
| &lt; 60s | `OK` |
| 60s–300s | `WARNING` |
| &gt; 300s | `CRITICAL` |

**Do not** alert on absolute count alone. Long transactions can be legitimate; combine with log used % and `log_reuse_wait_desc` when available.

### Permisos (**Fact**)

Same family as other state DMVs: `VIEW SERVER STATE` (≤2019) / `VIEW SERVER PERFORMANCE STATE` (2022+); Azure SQL uses `VIEW DATABASE STATE` / server roles per Microsoft docs.

### Limitaciones

- System / read-only transaction types may appear; consider filtering `transaction_type` later if noisy.
- Orphaned / prepared distributed transactions need careful interpretation.
- Join multiplicity (one transaction, many sessions) can duplicate rows in diagnostic queries — aggregate carefully.

### Utilidad

**High**, especially duration + log reuse correlation.

---

## 5. Data File Size / Used Space

### Fuente (**Fact**)

| Need | Source |
| --- | --- |
| Allocated file size | `sys.database_files` (`type = 0` rows; `size` is 8 KB pages → `/ 128.0` = MB) |
| Used / free within files | Prefer `sys.dm_db_file_space_usage` **or** `FILEPROPERTY(name, 'SpaceUsed')` |

`sys.dm_db_file_space_usage` is documented for **data file** space usage (not log). Useful columns: `total_page_count`, `allocated_extent_page_count`, `unallocated_extent_page_count`.

### Distinción obligatoria (**Decision**)

| Metric | Meaning |
| --- | --- |
| Allocated Data Size | File size on disk / reserved to the DB files |
| Used Data Space | Space in allocated extents (or FILEPROPERTY SpaceUsed) |
| Free Allocated Space | Allocated − used (inside current files) |

**Do not** treat allocated file size alone as “used space”.

### Consultas de referencia

Allocated:

```sql
SELECT
    name,
    type_desc,
    size / 128.0 AS SizeMB,
    max_size,
    growth
FROM sys.database_files
WHERE type = 0;
```

Used (DMV approach):

```sql
SELECT
    SUM(total_page_count) / 128.0 AS TotalMB,
    SUM(allocated_extent_page_count) / 128.0 AS UsedMB,
    SUM(unallocated_extent_page_count) / 128.0 AS FreeMB
FROM sys.dm_db_file_space_usage;
```

Alternative with lower permission surface in some estates:

```sql
SELECT
    SUM(size) / 128.0 AS AllocatedMB,
    SUM(CAST(FILEPROPERTY(name, 'SpaceUsed') AS int)) / 128.0 AS UsedMB
FROM sys.database_files
WHERE type = 0;
```

### Evaluación provisional

Apply % used against **used / allocated** (or used vs max size if autogrowth/`max_size` policy is modeled later):

| Used % of allocated | Status |
| --- | --- |
| &lt; 80% | `OK` |
| 80–90% | `WARNING` |
| &gt; 90% | `CRITICAL` |

**Caveat:** high % of *allocated* space is often normal; databases grow in chunks. Disk volume free space is a different metric (out of scope unless added later). Autogrowth remaining headroom may matter more than allocated fill % — flag as limitation.

### Permisos

| Source | Typical requirement |
| --- | --- |
| `sys.database_files` | Metadata visibility in current DB (`public` / membership) |
| `sys.dm_db_file_space_usage` | Database/server state permissions (platform-dependent) |
| `FILEPROPERTY` | Available when file metadata is visible |

### Utilidad

**Medium–High** for capacity trends; pair with log metrics. Single snapshot without growth history is limited (historical growth explicitly out of scope).

---

## 6. Transaction Log Size / Used Space

### Fuente (**Fact**)

`sys.dm_db_log_space_usage` — combines **all** log files for the current database.

Columns:

- `total_log_size_in_bytes`
- `used_log_space_in_bytes`
- `used_log_space_in_percent`
- `log_space_in_bytes_since_last_backup`

### Consulta

```sql
SELECT
    total_log_size_in_bytes / 1024.0 / 1024.0 AS TotalLogMB,
    used_log_space_in_bytes / 1024.0 / 1024.0 AS UsedLogMB,
    (total_log_size_in_bytes - used_log_space_in_bytes)
        / 1024.0 / 1024.0 AS FreeLogMB,
    used_log_space_in_percent AS UsedPercent,
    log_space_in_bytes_since_last_backup
        / 1024.0 / 1024.0 AS SpaceSinceLastBackupMB
FROM sys.dm_db_log_space_usage;
```

### Métricas MVP

| Metric | Required |
| --- | --- |
| Log Size | Yes |
| Log Used | Yes |
| Log Free | Yes |
| Log Used % | Yes |
| Log Growth Since Last Backup | Optional |

### Evaluación provisional

| Used % | Status |
| --- | --- |
| &lt; 70% | `OK` |
| 70–90% | `WARNING` |
| &gt; 90% | `CRITICAL` |

Always interpret together with **Log Reuse Wait** (below). Full recovery + `LOG_BACKUP` with rising used % is an ops issue, not a Hangfire bug.

### Permisos (**Fact**)

| Version | Permission |
| --- | --- |
| SQL Server ≤ 2019 | `VIEW SERVER STATE` |
| SQL Server 2022+ / MI | `VIEW SERVER PERFORMANCE STATE` |
| Azure SQL | `VIEW DATABASE STATE` / `##MS_ServerStateReader##` (tier-dependent) |

### Utilidad

**High** for storage incidents that starve Hangfire (log full → writes fail).

---

## 7. Log Reuse Wait

### Fuente (**Fact**)

`sys.databases.log_reuse_wait_desc` (plus `recovery_model_desc` for context).

```sql
SELECT
    name,
    recovery_model_desc,
    log_reuse_wait_desc
FROM sys.databases
WHERE database_id = DB_ID();
```

Common values: `NOTHING`, `LOG_BACKUP`, `ACTIVE_TRANSACTION`, `DATABASE_MIRRORING`, `AVAILABILITY_REPLICA`, `REPLICATION`, and others.

### Evaluación (**Decision**)

Do **not** treat every non-`NOTHING` value as error.

| Wait | Typical interpretation |
| --- | --- |
| `NOTHING` | Log can reuse; used % may still be high transiently |
| `LOG_BACKUP` | Expected under FULL until log backup; ops concern if stuck |
| `ACTIVE_TRANSACTION` | Correlate with oldest transaction duration |
| AG / mirroring / replication | Often environmental; warn with explanation, don’t blame Hangfire |

Composite diagnosis example (conceptual):

```text
LogUsed% WARNING/CRITICAL
  + LogReuseWait = ACTIVE_TRANSACTION
  + OldestTransactionDuration elevated
→ Investigate oldest transaction blocking log reuse
```

### Permisos

Catalog visibility for current database metadata; usually available to principals that can connect to the DB. Confirm in target environments; permission failure → `UNAVAILABLE`.

### Utilidad

**High** as explanatory companion to log used %.

---

## 8. Permissions model for Monitor

### Decision (**Accepted**)

| Outcome | Meaning |
| --- | --- |
| Metric available | Query succeeded; value returned |
| Metric unavailable | Permission / access denied / unsupported — status `UNAVAILABLE` with reason |
| Never map permission errors to `CRITICAL` | Critical is for observed unhealthy metric values under successful reads |

Existing failed-job monitoring uses `MonitoringStatus`: `OK` | `FAILED` | `UNAVAILABLE`. Storage Health likely needs a **distinct** status set (`OK` | `WARNING` | `CRITICAL` | `UNAVAILABLE`) so job failure (`FAILED`) is not overloaded. Final type placement deferred to implementation.

### Minimum grants (guidance, not implementation)

| Metric | Likely minimum |
| --- | --- |
| Schema Version | `SELECT` on Hangfire `Schema` table |
| Data allocated size | Read `sys.database_files` |
| Sessions / transactions / log DMVs | Server or database **state/performance state** as per SQL version |

**Assumption:** many production Hangfire app logins will **not** have `VIEW SERVER STATE`. Schema Version may be the only metric universally available until ops grants elevated read rights for Monitor’s login.

---

## 9. Conceptual result shape

Readers return **data**, not statuses:

```text
SchemaVersion = 9
ActiveSessions = 7
ActiveTransactions = 2
OldestTransactionDurationSeconds = 482
DataAllocatedMB = 2048
DataUsedMB = 1350
LogSizeMB = 1024
LogUsedMB = 845
LogUsedPercent = 82.5
LogReuseWait = ACTIVE_TRANSACTION
```

A later rules layer produces human-readable health, e.g. `WARNING` with explanation linking log % + reuse wait + oldest transaction.

---

## 10. Alignment with current architecture

| Existing piece | Reuse for Storage Health |
| --- | --- |
| `HangfireApplicationOptions.Schema` | Schema Version identifier |
| `SqlServerStorageFactory` (`PrepareSchemaIfNecessary = false`) | Connection/storage construction |
| `SqlServerStorageDb.UseConnection` | Execute read-only SQL |
| Domain rules pattern (`ApplicationMonitoringRules`) | Mirror with `SchemaVersionHealthRule` etc. |
| `ConfiguredApplicationsMonitor` + `DbException` → `UNAVAILABLE` | Same isolation idea for storage health orchestration later |

Do **not** use Hangfire `IMonitoringApi` for these SQL Server DMV metrics — they are outside Hangfire’s monitoring DTO surface.

Rejected for this feature:

- Writing / migrating schema
- `JobStorage.Current`
- Treating ADO.NET pool stats as SQL metrics
- Instance-wide active transaction counts without `database_id` filter

---

## 11. Proposed implementation order (future tasks)

When implementation is requested, prefer this vertical order:

1. **Schema Version** — highest confidence, lowest permission bar, no DMVs  
   Types sketched in the request: `SchemaVersionReader`, `StorageMetric`, `StorageHealthResult`, `SchemaVersionHealthRule` + unit tests.
2. **Data + Log + Log Reuse Wait** — capacity story; permission-sensitive.
3. **Active Transactions** (database-scoped) + oldest duration rules.
4. **Active SQL Sessions** as informational; Hangfire-specific filtering later.
5. Connection pool / Total Connections — only after a validated identification strategy.

---

## 12. Open questions

1. Should `ExpectedSchemaVersion` be global, per application, or both (default + override)? **Lean: default 9 + optional override.**
2. Should Storage Health statuses share `MonitoringStatus` or a new enum? **Lean: new enum** (`OK`/`WARNING`/`CRITICAL`/`UNAVAILABLE`).
3. How to detect “DMV partially visible (own session only)” vs true full session list without a separate permission probe?
4. For data used %, is the denominator allocated file size, max size, or volume free space? **MVP lean: used/allocated; document limitation.**
5. Azure SQL vs on-prem permission matrix — confirm against each target estate before requiring DMV metrics in UI.

---

## Sources

- Hangfire `Install.sql` / `DefaultInstall.sql` (`@TARGET_SCHEMA_VERSION = 9`) — HangfireIO/Hangfire
- Hangfire docs: Using SQL Server; Upgrading to Hangfire 1.8 (Schema 8/9)
- Microsoft Learn: `sys.dm_exec_sessions`, `sys.dm_tran_active_transactions`, `sys.dm_tran_database_transactions`, `sys.dm_db_log_space_usage`, `sys.dm_db_file_space_usage`, `sys.database_files`, DMV permission model (VIEW SERVER STATE / VIEW SERVER PERFORMANCE STATE)
- Current repo: `_docs/architecture.md` (HM-021 connection/schema decisions), Hangfire.SqlServer **1.8.25** package pin
