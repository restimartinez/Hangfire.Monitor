# Storage Health — Investigation to Define Health

**Status:** Investigation complete; **Storage Health v1 implemented** (see [Storage Health Implementation](#storage-health-implementation))  
**Date:** 2026-09-27  
**Related:** `_docs/hangfire-storage-health-investigation.md` (metric acquisition), `_docs/architecture.md` (Storage Health UI), current readers under `Hangfire.Monitor.Infrastructure/Storage/`

Legend used throughout:

| Label | Meaning |
| --- | --- |
| **Fact** | Observed in our 23-DB snapshot, verified in current Monitor code, or stated in Microsoft / Hangfire docs |
| **Interpretation** | Reasonable reading of facts; not yet a product decision |
| **Proposal** | Candidate for review before implementation |

---

## 1. Executive Summary

**Conclusion:** With the metrics we collect today and one snapshot of **23** Hangfire databases, we **cannot** yet justify percentage-based Health thresholds for Data Files, Transaction Log, or Active Transactions. Doing so would invent risk from normal file fill levels.

**What Health should mean (Proposal):** whether Hangfire SQL storage is **able to keep accepting work** without an **imminent capacity or log-reuse failure** — not “how full the allocated files look.”

| Finding | Implication |
| --- | --- |
| Data Used % spans ~2%–98%; several DBs sit above 90% | High `DataUsedPercent` alone is **not** a reliable Risk signal |
| Log Used % spans ~0.5%–22%; almost all `log_reuse_wait_desc = NOTHING` | High-ish log % alone is **not** a problem in this estate |
| One DB shows `ACTIVE_TRANSACTION` with `ActiveTransactionCount = 0` | Log reuse wait ≠ live open-transaction inventory |
| All 23 DBs have `ActiveTransactionCount = 0` | No evidence for duration/count thresholds |
| `ActiveServers` is already excluded from status aggregation | Keep as **operational context**, not universal Health |

**Recommended for a future Health column (after more collection):** explicit, explainable rules — **not** a 0–100 score. Prefer few states. Prefer **including a metric only when evidence exists**.

**Code note (Fact):** `DataFileSpaceHealthRules` previously encoded provisional **80% / 90%** used-of-allocated thresholds. Storage Health v1 **removed** those thresholds; used-% is informational only. Capacity Warning uses headroom MaxSize rules — see [Storage Health Implementation](#storage-health-implementation).

---

## 2. Current Metrics

### 2.1 Shown on Storage Health (index + details)

| Area | Metrics | Source (Fact) | Role today |
| --- | --- | --- | --- |
| Servers | `ServerCount` (Active servers) | Hangfire `StatisticsDto.Servers` | Display + row CSS `status-row-no-servers` when 0; **does not** affect `StorageHealthStatus` |
| Data Files | `AllocatedMB`, `UsedMB`, `FreeMB`, `UsedPercent` | `sys.dm_db_file_space_usage`; % computed in domain as `UsedMB / AllocatedMB` | Index shows %; domain rule evaluates OK/WARNING/CRITICAL (hidden aggregate) |
| Transaction Log | `TotalLogMB`, `UsedLogMB`, `FreeLogMB`, `UsedPercent` | `sys.dm_db_log_space_usage` | Display only (acquisition) |
| Log Reuse | `log_reuse_wait`, `log_reuse_wait_desc`, `recovery_model_desc` | `sys.databases` for `DB_ID()` | Display only |
| Active Transactions | `Count`, `OldestBeginTimeUtc`, `OldestDurationSeconds` | `sys.dm_tran_active_transactions` ⋈ `sys.dm_tran_database_transactions` where `transaction_state = 2` and `database_id = DB_ID()` | Display only |

### 2.2 Also collected / evaluated but outside this Health definition task

| Metric | Notes |
| --- | --- |
| Schema Version | Own rules (`OK` / `WARNING` / `UNAVAILABLE`); badge on index. Separate concern from capacity Health. |

### 2.3 What current metrics measure (capacity vs operational)

| Metric | Category | Instant snapshot? |
| --- | --- | --- |
| Data Allocated / Used / Free / Used% | **Capacity (inside current files)** | Yes — fill of *current* allocated data space |
| Log Total / Used / Free / Used% | **Capacity (inside current log)** | Yes |
| File growth / max size / volume free | **Capacity (headroom)** | **Not collected today** |
| `log_reuse_wait_desc` | **Operational (log truncation blocker)** | Partially — as of last truncation/checkpoint attempt (see §4.4) |
| Active transaction count / oldest duration | **Operational** | Yes — live DMV filter |
| Active Hangfire servers | **Operational (workers)** | Yes — Hangfire heartbeats, not SQL capacity |

---

## 3. Observed Real Data

**Fact:** Snapshot of **23** Hangfire storage databases (estate analysis provided for this investigation). Single point in time; not a trend study.

Applications in examples below are labeled **Application A–H** (generic identifiers only; no real names from configuration).

### 3.1 Log Reuse

| `log_reuse_wait_desc` | Count |
| --- | ---: |
| `NOTHING` | 22 |
| `ACTIVE_TRANSACTION` | 1 |

Only non-`NOTHING` case:

```text
Application A — active transaction wait
Data Used:       ~13.90%
Log Used:        ~16.68%
Log Reuse:       ACTIVE_TRANSACTION
Active Tx:       0  (OldestBeginTime / OldestDurationSeconds = NULL)
Recovery:        SIMPLE
```

### 3.2 Data Files (Used % of allocated)

| Approx. | Value |
| --- | --- |
| Minimum | ~2.36% |
| Maximum | ~98.40% |

High fill examples (**Fact** — not labeled as incidents by ops in the briefing):

| Application | Data Used % (approx.) |
| --- | ---: |
| Application B | 98.40% |
| Application C | 98.27% |
| Application D | 89.62% |
| Application E | 72.66% |
| Application F | 67.71% |
| Application G | 62.50% |

Application B detail (**Fact**):

```text
Allocated: ~1096 MB
Used:      ~1078 MB
Free:        ~17 MB
Used:      ~98.40%
```

### 3.3 Transaction Log (Used %)

| Approx. | Value |
| --- | --- |
| Range | ~0.5% → ~22.19% |

Higher examples (still with mostly `NOTHING` reuse wait):

| Application | Log Used % (approx.) |
| --- | ---: |
| Application G | 22.19% |
| Application B | 21.04% |
| Application F | 19.98% |
| Application A | 16.68% |
| Application H | 12.45% |

### 3.4 Active Transactions

**Fact:** For all 23 databases in the snapshot:

```text
ActiveTransactionCount = 0
OldestBeginTime = NULL
OldestDurationSeconds = NULL
```

### 3.5 What this snapshot does *not* tell us

- Autogrowth settings, `max_size`, or volume free space
- Whether high data fill is stable after growth or racing toward a hard limit
- Whether `ACTIVE_TRANSACTION` wait persists across checkpoints / minutes
- Any long-running transaction durations (none observed)
- Intentional vs accidental `ActiveServers = 0` rates over time

---

## 4. Metric Analysis

### 4.1 Servers

**Fact (product behavior today):**

- Source: Hangfire monitoring statistics (`Servers`), not SQL DMVs.
- `ApplicationStorageHealthRules` and Failed Jobs monitoring both treat `ServerCount` as **non-status**: propagated for display only.
- UI: `ServerCount == 0` → CSS class `status-row-no-servers` (visual cue), not a Health enum.

**Interpretation:**

| Situation | Meaning |
| --- | --- |
| App should have workers, `Servers = 0` | Possible outage / misconfig — **job processing** risk |
| App stopped intentionally | Expected — not storage failure |
| Transient between heartbeats | Possible false alarm on a single poll |

**Proposal:** Keep **Active Servers outside** the Storage Health column. Storage Health answers “can this database storage sustain Hangfire?”; Servers answers “are Hangfire workers registered?” Mixing them conflates two failure domains and forces a universal rule we cannot justify without per-app “expected servers” config.

**Confidence:** High for exclusion from Storage Health. Medium that a separate “workers” signal may be useful later (with expected-count config).

---

### 4.2 Data Files

**Fact — what `DataUsedPercent` is:**

```text
UsedPercent = UsedMB / AllocatedMB * 100
```

from `sys.dm_db_file_space_usage` (`allocated_extent_page_count` / `total_page_count`).

That is **fill within the current data file allocation**, not:

- free space on the disk volume,
- remaining autogrowth headroom,
- distance to `max_size`.

**Domain rules:** `DataFileSpaceHealthRules` maps that % to OK / WARNING / CRITICAL using **80% / 90%** constants. See the dedicated section below for origin, call chain, UI impact, snapshot comparison, and the explicit conclusion.

**Proposal (preview):** Treat `DataUsedPercent` as **informational** for future Health until headroom metrics exist.  
**Threshold for future Health:** `NO DEFINIR THRESHOLD` on used-of-allocated alone.

---

### 4.3 Transaction Log

**Fact:** Observed max ~22% used; majority with `NOTHING`. No DB in the snapshot approaches “log full” territory by percentage alone.

**Interpretation:**

- Log Used % high with `NOTHING` often means the log file is sized for peak and currently holding reusable VLFs / recent activity — **not** imminent 9002.
- Risk appears when **reuse is blocked** *and* used space grows toward file/volume limits (or growth is impossible).
- A lone threshold such as “LogUsed > 70% = WARNING” has **zero positive cases** in this snapshot and would still be arbitrary relative to estate norms (max 22%).

**Proposal:** `LogUsedPercent` alone → **informational**.  
**Threshold:** `NO DEFINIR THRESHOLD` until we see blocked reuse + rising/high used % (or cannot-grow) together.

---

### 4.4 Log Reuse — and the `ACTIVE_TRANSACTION` vs Active Tx = 0 gap

#### 4.4.1 What `log_reuse_wait_desc` means

**Fact (Microsoft Learn — `sys.databases`):** `log_reuse_wait` / `log_reuse_wait_desc` describe why transaction log space reuse is waiting **as of the last checkpoint** (truncation attempt context), not a guaranteed live census of open transactions.

Documented values include: `NOTHING`, `CHECKPOINT`, `LOG_BACKUP`, `ACTIVE_BACKUP_OR_RESTORE`, `ACTIVE_TRANSACTION`, `DATABASE_MIRRORING`, `REPLICATION`, `DATABASE_SNAPSHOT_CREATION`, `LOG_SCAN`, `AVAILABILITY_REPLICA`, `OLDEST_PAGE`, `XTP_CHECKPOINT`, `SLOG_SCAN`, …

**Fact (community / SQLSkills — Paul Randal):** The wait reason is what prevented log clearing the **last time** clearing was attempted. After the blocking condition ends, the description may remain until another truncation/checkpoint updates it.

#### 4.4.2 What our Active Transactions query means

**Fact (Monitor code — `ActiveTransactionsQuery`):**

```sql
SELECT COUNT(*), MIN(...), MAX(DATEDIFF(...))
FROM sys.dm_tran_active_transactions AS at
INNER JOIN sys.dm_tran_database_transactions AS dt
  ON dt.transaction_id = at.transaction_id
WHERE at.transaction_state = 2
  AND dt.database_id = DB_ID()
```

This is a **live** filter: state = active, scoped to the current database.

#### 4.4.3 Why both can be true at once

**Fact + Interpretation** (Application A case fits this model):

| Signal | Meaning |
| --- | --- |
| `log_reuse_wait_desc = ACTIVE_TRANSACTION` | Last truncation attempt attributed wait to an active transaction |
| `ActiveTransactionCount = 0` | **Right now**, no DB-scoped `transaction_state = 2` row |

Compatible explanations (not mutually exclusive):

1. **Stale wait reason** — transaction ended; wait not refreshed until next checkpoint/truncation attempt (especially plausible under **SIMPLE**, where checkpoints drive truncation).
2. **Race** — transaction ended between wait update and our DMV read (or reverse).
3. **Different visibility** — deferred / system / other transaction shapes may affect wait semantics differently than our strict filter (less likely primary explanation here, but possible edge).

**Interpretation of Application A:** Log used ~17%, Data used ~14%, Active Tx 0, SIMPLE. This is **not** evidence of an ongoing log-full incident. Treating `ACTIVE_TRANSACTION` alone as Unhealthy would be a **false positive** in this snapshot.

#### 4.4.4 Relevance of wait values for Hangfire Monitor estate

| Wait | Seen in 23 DBs? | Relevance (Interpretation) |
| --- | --- | --- |
| `NOTHING` | Yes (22) | Normal; no Health action |
| `ACTIVE_TRANSACTION` | Yes (1) | Investigate **with** live Active Tx + Log Used; alone insufficient |
| `LOG_BACKUP` | No | Relevant if FULL recovery + stuck backups; needs recovery_model context |
| `AVAILABILITY_REPLICA` / `DATABASE_MIRRORING` / `REPLICATION` | No | Environmental; warn only if sustained + log pressure (not Hangfire-specific) |
| Others | No | Collect as raw text; don’t invent per-value Health rules yet |

**Proposal for Health:** Do **not** map every non-`NOTHING` to Warning. Prefer composite rules later (wait + live evidence + log capacity). Alone: **Exclude** from automatic Health until sustained/composite evidence exists.  
**Threshold:** `NO DEFINIR THRESHOLD` for wait-alone.

---

### 4.5 Active Transactions

**Fact:** Snapshot provides **no** positive examples of `Count > 0` or non-null `OldestDurationSeconds`.

**Interpretation:**

- `OldestDurationSeconds` is the better **future** alert input than raw count (aligned with earlier technical investigation), **when** real long transactions appear.
- Inventing `> 60s` / `> 300s` would be pure speculation against empty evidence.

**Proposal:** Keep metrics on the details page. **Exclude** from Health until we have observed durations correlated with log pressure.  
**Threshold:** `NO DEFINIR THRESHOLD`.

---

## 5. Additional SQL Server Metrics

To turn capacity % into risk, collect (read-only) the following. **Proposal — not implemented.**

### 5.1 Data and log file configuration (`sys.database_files`)

| Column / derived | Why |
| --- | --- |
| `size` (current size) | Confirm allocation |
| `max_size` | Hard ceiling (`-1` / `268435456` pages = unlimited depending on version/docs) |
| `growth`, `is_percent_growth` | Autogrowth enabled? MB vs % |
| `type` / `type_desc` | Separate data (`ROWS`) vs log (`LOG`) |
| `name`, `physical_name` | Diagnostics / volume join |

**Interpretation:** Risk ≈ “little free space **and** (growth disabled **or** at max_size **or** volume full).”

### 5.2 Volume free space

| Source | Why |
| --- | --- |
| `sys.dm_os_volume_stats` joined to database files | Free bytes on the volume hosting data/log files |

**Fact:** Typically needs elevated state permissions (same family as other DMVs). May be `UNAVAILABLE` for some Monitor logins — treat as missing headroom signal, not CRITICAL.

### 5.3 Optional log context

| Metric | Why |
| --- | --- |
| `log_space_in_bytes_since_last_backup` (already on `sys.dm_db_log_space_usage`) | FULL recovery / backup lag context |
| Repeated samples of `log_reuse_wait_desc` over time | Distinguish stale vs sustained waits |

### 5.4 Suggested diagnostic query shape (documentation only)

```sql
-- File headroom (data + log)
SELECT
    df.name,
    df.type_desc,
    df.size / 128.0 AS CurrentSizeMB,
    CASE
        WHEN df.max_size = -1 THEN NULL
        WHEN df.max_size = 268435456 THEN NULL  -- unlimited sentinel (confirm per version)
        ELSE df.max_size / 128.0
    END AS MaxSizeMB,
    df.growth,
    df.is_percent_growth,
    vs.volume_mount_point,
    vs.available_bytes / 1048576.0 AS VolumeFreeMB,
    vs.total_bytes / 1048576.0 AS VolumeTotalMB
FROM sys.database_files AS df
CROSS APPLY sys.dm_os_volume_stats(DB_ID(), df.file_id) AS vs;
```

(Exact `max_size` unlimited sentinels should be verified against target SQL versions before coding.)

### 5.5 What we still should **not** require for MVP Health

- Historical growth charts
- Blocking / deadlock graphs
- ADO.NET pool metrics
- Universal absolute MB free thresholds without estate calibration

---

## 6. Candidate Health Rules

| Metric | Candidate rule | Evidence | Confidence | Recommendation |
| --- | --- | --- | --- | --- |
| Log Reuse (`ACTIVE_TRANSACTION` alone) | Non-`NOTHING` → Warning | 1/23 case; Active Tx 0; Log Used ~17%; wait can be stale | Low | **Exclude** until composite |
| Log Reuse + Active Tx duration + Log Used | Warning only if wait ≠ NOTHING **and** live oldest duration elevated **and** log used high / cannot grow | Compatible with SQL semantics; **0** confirming cases in snapshot | Low–Medium (theory) | **Defer**; collect more samples |
| Data Used % alone | e.g. ≥80% / ≥90% | Would flag healthy-looking high-fill DBs (Application B, Application C); provisional code already does this silently | Low (as Health) | **Exclude** / **revisar** existing domain thresholds |
| Data Used + cannot grow + low volume free | Unhealthy / Warning when free inside file low **and** growth blocked **or** volume nearly full | Strong capacity model; **metrics not collected yet** | High (model), N/A (data) | **Include later** after §5 metrics |
| Log Used % alone | e.g. ≥70% | Max observed ~22%; no incident correlation | Low | **Exclude** — `NO DEFINIR THRESHOLD` |
| Log Used + reuse blocked + cannot grow | Capacity/operational composite | Standard DBA incident pattern; no estate positive sample yet | Medium | **Defer** |
| Active Tx count | Count > N | All zeros | Low | **Exclude** — `NO DEFINIR THRESHOLD` |
| OldestDurationSeconds | > 60 / > 300 s | No observations | Low | **Exclude** — `NO DEFINIR THRESHOLD` |
| Active Servers = 0 | → Unhealthy | Ambiguous (stopped vs broken); already excluded from status | High for exclusion | **Exclude** from Storage Health |
| Schema Version | actual ≠ expected → Warning | Already implemented; different concern | High | Keep as **Schema** signal; optional input to overall Health later |
| Permission / connection failure | → Unavailable | Existing pattern | High | **Include** as Unavailable (not Unhealthy) |

---

## 7. Proposed Health Model

> **Proposed — pending approval**

### 7.1 Definition of Health

**Storage Health** = assessment of whether the Hangfire SQL Server database can **continue to store and process Hangfire work from a storage perspective** (space + log reuse), based on **justified** signals.

Out of scope for this Health value:

- Job failure counts (Failed Jobs page)
- Whether workers are intentionally stopped (`Servers`)
- Aesthetic “how full is the file” without headroom context

### 7.2 States (minimal)

| State | Meaning | Justification |
| --- | --- | --- |
| **Healthy** | No justified storage risk from evaluable metrics | Default when reads succeed and no included rule fires |
| **Warning** | Elevated risk or drift that needs attention, not proven imminent failure | Use only for rules with evidence (today: primarily Schema mismatch if folded in; capacity Warning only after headroom metrics) |
| **Unavailable** | Metrics could not be read (permissions, connectivity, invalid payload) | Existing product pattern; never map access failure to Unhealthy |

**Unhealthy / Critical:** **Not proposed for the first Health column** based on current evidence. The domain enum already has `CRITICAL`, but we lack estate-backed rules that deserve it without high false-positive risk.

**Offline:** Prefer **Unavailable** (cannot observe) rather than a fourth business state. Do not equate `Servers = 0` with Offline.

**Intermediate state:** Yes — **Healthy / Warning / Unavailable** is enough for v1 of the column. Revisit Unhealthy when composites fire on real incidents.

### 7.3 Scoring

**Proposal: no HealthScore (0–100).**

Prefer independent, explainable rules that surface the **worst applicable** status among **included** metrics (same spirit as current Schema + Data Files aggregation), with human-readable diagnosis pointing at the triggering metric.

Reasons:

- Scores hide which signal fired
- Arbitrary weights without incident history
- Conflicts with the preference for explicit rules

### 7.4 Combination / precedence (when rules eventually exist)

**Proposal (principle, not numeric priorities):**

1. **Unavailable** only when the application cannot be evaluated at all (or all included metrics unavailable).
2. Among evaluable metrics: **stronger evidence of write failure / cannot grow** outranks **soft** signals (stale reuse wait, high used-% with growth available).
3. Capacity headroom (cannot grow / volume full) prevails over used-% of allocated.
4. Live Active Tx duration + rising log used prevails over `ACTIVE_TRANSACTION` wait alone.
5. Schema drift remains a **Warning**-class signal, not proof of storage fullness.
6. Servers never override Storage Health.

Example (conceptual — **not** implemented):

```text
DataUsed = 99% + file can grow + volume has free space  → still Healthy (informational %)
DataUsed = 99% + growth disabled + volume nearly full   → Warning/Unhealthy (when defined)
LogReuse = ACTIVE_TRANSACTION + ActiveTx = 0 + LogUsed = 17% → Healthy (stale/benign)
LogReuse = ACTIVE_TRANSACTION + OldestDuration high + LogUsed high + cannot grow → Warning+
```

### 7.5 Metrics participation (v1 candidate)

| Metric | Health participation |
| --- | --- |
| Connection / metric acquisition failure | **Include** → Unavailable |
| Schema Version | **Optional include** (already justified) as Warning |
| Data / Log used % | **Informational only** until headroom collected |
| Log reuse wait alone | **Informational only** |
| Active transactions | **Informational only** |
| Active servers | **Informational only** (keep row styling) |

### 7.6 Explicit non-goals for this phase

- Do not invent Data/Log % thresholds
- Do not invent Active Tx duration thresholds
- Do not create HealthScore
- Do not implement the Health column yet
- Do not remove existing raw metrics from the UI

---

## DataFileSpaceHealthRules — detailed investigation

This section answers, with repository evidence only, where the **80% / 90%** thresholds come from, how they flow through the app, whether users can see them today, and how they would classify the 23-DB snapshot.

### Origin of the 80% / 90% thresholds

#### Where they are defined (**Fact**)

File: `Hangfire.Monitor.Domain/DataFileSpaceHealthRules.cs`

| Constant | Value | Meaning in code |
| --- | ---: | --- |
| `DefaultWarningThresholdPercent` | **80** | `usedPercent < 80` → `OK`; `80 ≤ usedPercent ≤ 90` → `WARNING` |
| `DefaultCriticalThresholdPercent` | **90** | `usedPercent > 90` → `CRITICAL` |

Exact mapping (**Fact** from `Evaluate`):

```text
usedPercent < 80.00   → OK
80.00 ≤ usedPercent ≤ 90.00  → WARNING
usedPercent > 90.00   → CRITICAL
```

So **exactly 90.00% is WARNING**, not CRITICAL. Invalid / zero allocated → `UNAVAILABLE` (not a threshold).

#### When introduced (**Fact** — Git)

| When | Commit | What |
| --- | --- | --- |
| 2026-09-26 09:46 | `499af9b` — `docs: document Hangfire storage health investigation` | First written as **“Evaluación provisional”** in `_docs/hangfire-storage-health-investigation.md`: &lt;80 OK, 80–90 WARNING, &gt;90 CRITICAL, plus a **Caveat** that high % of allocated is often normal |
| 2026-09-26 10:29 | `cbd8f63` — `feat: add data file space health rules` | Constants and rules landed in domain code; commit message has **no** rationale beyond the feature title |

No later commit changed the numeric values (file history is a single introducing commit for `DataFileSpaceHealthRules.cs`).

#### Justification in Git / docs (**Fact**)

| Claim | Evidence |
| --- | --- |
| Backed by the 23-DB estate? | **No** — rules landed **before** this evidence-based Health investigation; the acquisition doc did not cite estate measurements |
| Backed by Microsoft / Hangfire docs as required alert levels? | **No** — neither source mandates 80/90 for used-of-allocated |
| Labeled provisional in docs? | **Yes** — `_docs/hangfire-storage-health-investigation.md` §5: “Evaluación provisional”; principles item 8: “Thresholds provisional and later configurable”; Caveat warns high allocated fill is often normal |
| Commit message justifies numbers? | **No** — `feat: add data file space health rules` only |

**Interpretation:** The values are **provisional / arbitrary design defaults** copied from the earlier investigation doc into code. They are **not** estate-calibrated and **not** technically justified as capacity risk by the 23-DB snapshot.

Verdict on origin: **NOT JUSTIFIED** as Health thresholds (see conclusion below).

### Current usage (call chain)

```text
ConfiguredApplicationsStorageHealthMonitor.ReadDataFiles
        ↓  DataFileSpaceReader → DataFileSpaceMetrics
DataFileSpaceHealthRules.Evaluate(metrics)
        ↓  DataFileSpaceHealthResult
           (AllocatedMB, UsedMB, FreeMB, UsedPercent, Status, Diagnosis, Recommendation)
ApplicationStorageHealthRules.FromMetrics(..., dataFiles, ...)
        ↓  AggregateStatus(schema.Status, dataFiles.Status)
           → ApplicationStorageHealthResult.Status
Pages:
  Index  → FormatDataFileSpace(result.DataFiles)  // UsedPercent text only
  Details → FormatMegabytes / FormatUsedPercent   // raw numbers only
```

| Step | Who | What |
| --- | --- | --- |
| Caller | `ConfiguredApplicationsStorageHealthMonitor.ReadDataFiles` | Calls `_dataFileRules.Evaluate` on successful read; `Unavailable()` on `DbException` |
| Result | `DataFileSpaceHealthResult` | Carries size fields + `Status` + `Diagnosis` + `Recommendation` |
| Aggregator | `ApplicationStorageHealthRules.FromMetrics` | Schema + Data Files statuses only → app-level `Status` (CRITICAL wins over WARNING over OK; ignore UNAVAILABLE if the other metric is known) |
| Index UI | `StorageHealthDisplay.FormatDataFileSpace` | Uses **`UsedPercent` only** (or `-`); **does not** read `Status`, Diagnosis, or Recommendation |
| Details UI | `Details.cshtml` Data Files section | Shows Allocated / Used / Free / Used % only; **does not** show data-file Status / Diagnosis / Recommendation |
| Schema badge | `FormatSchemaBadgeCssClass(result.Schema)` | Uses **Schema** status only — not Data Files |
| App Status column | Removed in `426cbad` (2026-09-27) | Previously showed `result.Status` (Schema **or** Data Files aggregation). **Gone from UI** |
| `FormatStatus` / `FormatStatusSortValue` | Still exist on `StorageHealthDisplay` | Covered by tests; **not** referenced by current Index/Details views |
| Details `IsUnavailable` | `Status == UNAVAILABLE` only | A Data Files **CRITICAL** does **not** hide the details page |

DI registration (**Fact**): `HangfireMonitorServiceCollectionExtensions` registers `DataFileSpaceHealthRules` as singleton; still invoked on every Storage Health monitor pass.

### Current user-visible impact of 80/90

| Effect | Visible to user today? |
| --- | --- |
| Data Files column shows used % (e.g. `98.40%`) | **Yes** — but the **percentage is independent** of 80/90; thresholds only set `Status` |
| Badge / color / WARNING / CRITICAL for data files | **No** |
| Application Status column driven by Data Files CRITICAL | **No** (column removed) |
| Diagnosis / Recommendation strings | **No** (not bound in Razor) |
| Rules still compute Status in memory / tests | **Yes** — domain + unit tests; invisible in the running UI |

**Fact:** Users can see a high used % as a number. They **cannot** currently see that the domain would classify it as WARNING or CRITICAL. Restoring a Health/Status column that reused `ApplicationStorageHealthResult.Status` **without** changing `DataFileSpaceHealthRules` would suddenly surface these provisional thresholds.

### Comparison with the 23-DB snapshot

Rule applied to reported high-fill cases (**Fact** — approximate % from §3.2; exact boundary rules above):

| Application | Data Used % (approx.) | `DataFileSpaceHealthRules` status |
| --- | ---: | --- |
| Application B | 98.40 | **CRITICAL** (&gt; 90) |
| Application C | 98.27 | **CRITICAL** (&gt; 90) |
| Application D | 89.62 | **WARNING** (80–90 inclusive) |
| Application E | 72.66 | **OK** (&lt; 80) |
| Application F | 67.71 | **OK** |
| Application G | 62.50 | **OK** |

Among the “high percentage” cases listed for this estate, **only three** would be non-OK under current rules (2 CRITICAL, 1 WARNING). Ips and below stay OK despite being listed as relatively high fill.

A full ordered table of all 23 names was not supplied in the briefing; the list above are the documented high cases. Estate min/max (~2.36% / ~98.40%) imply most DBs are OK under these rules, while the worst fills would be CRITICAL.

#### Why those results do **not** prove a capacity problem

**Fact / Interpretation:**

1. Denominator is **current allocated file size**, not volume capacity or `max_size`.
2. Application B still has ~17 MB free *inside* the file; if autogrowth is enabled and the volume has space, SQL Server can grow — high fill often means the file was grown to fit.
3. No `growth` / `max_size` / volume-free metrics were collected for these DBs.
4. The briefing does not treat these high % values as known incidents.
5. The acquisition doc itself already caveated that high allocated fill is often normal.

Therefore classifying Application B / Application C as CRITICAL (or Application D as WARNING) via 80/90 is a **label from provisional constants**, not evidence of storage failure.

### What the code does today vs what is adequate for future Health

| | Current code behavior | Adequate for future Health? |
| --- | --- | --- |
| Computes used % of allocated | Yes | Yes — keep as **raw metric** |
| Maps 80/90 → WARNING/CRITICAL | Yes (in domain) | **No** — not estate-justified |
| Surfaces that mapping in UI | No (Status column removed) | Should not re-enable until rules are evidence-based |
| Diagnosis claims “approaching / exhausting capacity” | Yes (in result object) | Misleading without headroom context |

---

## DataFileSpaceHealthRules — conclusion

| Question | Answer |
| --- | --- |
| **¿Mantener 80/90% como thresholds de Health?** | **NOT JUSTIFIED** |
| **¿Utilizar `DataUsedPercent` como señal directa de Health?** | **NOT JUSTIFIED** (alone). Keep as informational capacity fill until headroom metrics exist. |
| **¿Qué hacer con `DataFileSpaceHealthRules` antes de implementar Health?** | **NOT ENOUGH EVIDENCE** to replace 80/90 with any other used-% thresholds. Before a Health column: do **not** wire UI Health to these provisional constants; either stop treating used-% as a Health contributor (informational only / always OK when metrics valid) or defer Data Files out of `ApplicationStorageHealthResult.Status` until cannot-grow / volume-free rules exist. Collect §5 headroom metrics first. |

Distinction (required):

- **Today:** code still evaluates 80/90 into `DataFileSpaceHealthResult.Status` and aggregates into `ApplicationStorageHealthResult.Status`, but the Storage Health UI does **not** display those statuses — only the used % number.
- **Future Health:** do **not** promote 80/90 (or used-% alone) into the Health column; that would be **NOT JUSTIFIED** against the 23-DB evidence.

---

## 8. Open Questions

1. **Headroom collection:** **Partially answered** — see [Data File Capacity / Headroom Investigation](#data-file-capacity--headroom-investigation). Query shape + permissions validated on **17** configured DBs; still missing the rest of the earlier ~23 estate (esp. former ~98% fill cases). Approve productizing readers later, separately from this investigation.
2. **Permission reality:** Which Monitor logins can see volume stats vs file metadata only?
3. **Recovery models in estate:** How many Hangfire DBs are `FULL` vs `SIMPLE`? (Affects whether `LOG_BACKUP` will appear.)
4. **Sustained sampling:** Can we re-poll Application A (and others) over time to see if `ACTIVE_TRANSACTION` clears after checkpoint / activity?
5. **Schema in Health column:** Should overall Health include Schema Warning, or keep Schema as its own column only?
6. **Existing `DataFileSpaceHealthRules` 80/90:** **Answered** — see [DataFileSpaceHealthRules — conclusion](#datafilespacehealthrules--conclusion). Remaining implementation choice (when coding Health): neutralize Data Files’ contribution to app Status vs leave domain as-is but unused by Health UI.
7. **Expected servers:** Is there appetite for per-app “expected server count” later (separate from Storage Health)?
8. **Incident backlog:** Are there known Hangfire DB incidents (log full, disk full) we can reverse-engineer for positive examples?
9. **Naming:** Keep domain enum `OK` / `WARNING` / `CRITICAL` / `UNAVAILABLE` vs UI labels Healthy / Warning / Unhealthy / Unavailable?

---

## Sources

- Estate snapshot: 23 Hangfire databases (metrics summarized in §3) — **Fact** from investigation briefing
- Monitor queries: `DataFileSpaceQuery`, `LogSpaceQuery`, `LogReuseWaitQuery`, `ActiveTransactionsQuery`
- Domain: `DataFileSpaceHealthRules`, `ApplicationStorageHealthRules`, `StorageHealthStatus`
- Git: `499af9b` (provisional 80/90 in docs), `cbd8f63` (rules implementation), `426cbad` (Status column removed from Storage Health index)
- UI: `StorageHealthDisplay.FormatDataFileSpace`, `Pages/StorageHealth/Index.cshtml`, `Pages/StorageHealth/Details.cshtml`
- Microsoft Learn: `sys.databases` (`log_reuse_wait` / `log_reuse_wait_desc`), `sys.dm_db_file_space_usage`, `sys.dm_db_log_space_usage`, troubleshooting full transaction log (error 9002)
- SQLSkills (Paul Randal): log reuse wait reflects last truncation attempt; `ACTIVE_TRANSACTION` severity when truly holding VLFs
- Prior repo doc: `_docs/hangfire-storage-health-investigation.md` (provisional thresholds — **not justified** for future Health given estate evidence)

---

## Data File Capacity / Headroom Investigation

**Status:** Investigation / data acquisition only — **no Health rules, thresholds, or production code changes**  
**Date:** 2026-09-27  
**Related:** §5 (proposed headroom metrics), [DataFileSpaceHealthRules — conclusion](#datafilespacehealthrules--conclusion)

**Goal:** Collect file-level and volume-level headroom so future Health can consider **growth capacity**, not only database-level `DataUsedPercent`.

**Anonymization (Fact):** Results below use generic labels only (`Application A`–`Q`, `Volume 1`, `file 1`). Real names, hosts, paths, and credentials from configuration were used only to run queries and are **not** recorded here. Labels in this section are **local to this snapshot** (alphabetical by configured app name) and are **not** the same mapping as §3.

### Coverage of this run

| Item | Value |
| --- | --- |
| Configured apps in local secrets for this run | **17** |
| Prior §3 briefing size | **23** |
| Successful reads | **17 / 17** |
| Failed / offline DBs | **0** |
| DATA (`ROWS`) files per DB | **1** each (17 file rows) |
| Distinct volumes observed | **1** (`Volume 1`) |
| `MaxSizeMB = UNLIMITED` | **17 / 17** |
| Growth disabled (`growth = 0` or `max_size = 0`) | **0** |
| Volume stats unavailable | **0** |

**Interpretation:** This run is a **partial estate** relative to the earlier 23-DB briefing. High-fill cases near ~98% from §3 were **not** present in the local configured set for this execution.

### Metric levels (do not mix)

| Level | Metrics | Source |
| --- | --- | --- |
| **Database** | `DbAllocatedMB`, `DbUsedMB`, `DbDataUsedPercent` | `sys.dm_db_file_space_usage` (same family as current Monitor `DataFileSpaceQuery`) |
| **File** | `CurrentSizeMB`, `UsedMB`, `FreeMB`, `FileUsedPercent`, `MaxSizeMB`, `GrowthMB` / `GrowthPercent`, `IsPercentGrowth` | `sys.database_files` + `FILEPROPERTY(name, 'SpaceUsed')` |
| **Volume** | `VolumeTotalGB`, `VolumeFreeGB`, `VolumeFreePercent` | `sys.dm_os_volume_stats(DB_ID(), file_id)` |

Relationship to preserve: **Database → File → Volume**.

### Metric definitions

| Metric | Meaning |
| --- | --- |
| `CurrentSizeMB` | Allocated size of the data file (`size` pages ÷ 128) |
| `UsedMB` / `FreeMB` | Space used / free **inside that file** (`FILEPROPERTY` SpaceUsed) |
| `FileUsedPercent` | `UsedMB / CurrentSizeMB` for that file |
| `DbDataUsedPercent` | Aggregate used-of-allocated across data pages (`dm_db_file_space_usage`) |
| `MaxSizeMB` | Hard ceiling from `max_size`. **`UNLIMITED`** when `max_size = -1` (grow until disk full). **`NO_GROWTH`** when `max_size = 0`. Otherwise numeric MB. Never invent a fake number for unlimited |
| `GrowthMB` | Autogrowth increment in MB when `is_percent_growth = 0` (`growth` pages ÷ 128). `NULL` when percent growth |
| `GrowthPercent` | Autogrowth percent when `is_percent_growth = 1`. `NULL` when fixed MB growth |
| `IsPercentGrowth` | Distinguishes `Growth = 10%` from `Growth = 512 MB` |
| `Volume*` | OS volume hosting that file’s path — shared across DBs on the same disk |

### Permissions

| Object | Permission (Fact — Microsoft Learn) |
| --- | --- |
| `sys.database_files` | Metadata visibility / `public` (subject to metadata visibility rules) |
| `FILEPROPERTY` | Available when file metadata is visible |
| `sys.dm_db_file_space_usage` | Typically requires `VIEW DATABASE STATE` (or broader server state permissions depending on version) |
| `sys.dm_os_volume_stats` | SQL Server **2019 and earlier:** `VIEW SERVER STATE`. SQL Server **2022+:** `VIEW SERVER PERFORMANCE STATE` |

**Fact from this run:** Current Monitor logins successfully returned volume stats for all 17 databases (permission present in this estate for those accounts).

### Limitations of `sys.dm_os_volume_stats`

1. **Server-level permission** — without `VIEW SERVER STATE` / `VIEW SERVER PERFORMANCE STATE`, volume columns are unavailable; treat as missing headroom signal (`UNAVAILABLE`), not CRITICAL.
2. **Per file, not per database** — join on `(database_id, file_id)`; multi-file / multi-volume DBs need one row per file.
3. **Volume is shared** — free % is for the whole volume (other non-Hangfire files consume space too).
4. **Not a forecast** — snapshot of free bytes now; does not predict growth rate.
5. **Linux / some hosts** — `volume_mount_point` may be null/empty; totals may still return depending on platform.
6. **Readable AG secondaries** — `physical_name` / volume context can reflect primary locations in some topologies (see Microsoft notes on `sys.database_files`).

### Per-database query (ROWS files only)

Read-only. Run in the context of each Hangfire database (or via dynamic `USE` in a consolidator).

```sql
SELECT
    DB_NAME() AS DatabaseName,
    df.file_id AS FileId,
    df.name AS FileName,
    df.physical_name AS PhysicalName,
    CAST(df.size AS bigint) / 128.0 AS CurrentSizeMB,
    CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint) / 128.0 AS UsedMB,
    (CAST(df.size AS bigint)
        - CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint)) / 128.0 AS FreeMB,
    CASE
        WHEN df.size > 0
        THEN 100.0 * CAST(FILEPROPERTY(df.name, N'SpaceUsed') AS bigint)
                   / CAST(df.size AS bigint)
        ELSE NULL
    END AS FileUsedPercent,
    CASE
        WHEN df.max_size = -1 THEN N'UNLIMITED'
        WHEN df.max_size = 0 THEN N'NO_GROWTH'
        ELSE CONVERT(nvarchar(50),
             CAST(CAST(df.max_size AS bigint) / 128.0 AS decimal(18, 2)))
    END AS MaxSizeMB,
    CASE
        WHEN df.is_percent_growth = 1 THEN NULL
        ELSE CAST(df.growth AS bigint) / 128.0
    END AS GrowthMB,
    CASE
        WHEN df.is_percent_growth = 1 THEN df.growth
        ELSE NULL
    END AS GrowthPercent,
    df.is_percent_growth AS IsPercentGrowth,
    vs.volume_mount_point AS VolumeMountPoint,
    CAST(vs.total_bytes AS float) / 1073741824.0 AS VolumeTotalGB,
    CAST(vs.available_bytes AS float) / 1073741824.0 AS VolumeFreeGB,
    CASE
        WHEN vs.total_bytes IS NULL OR vs.total_bytes = 0 THEN NULL
        ELSE 100.0 * CAST(vs.available_bytes AS float)
                   / CAST(vs.total_bytes AS float)
    END AS VolumeFreePercent
FROM sys.database_files AS df
OUTER APPLY sys.dm_os_volume_stats(DB_ID(), df.file_id) AS vs
WHERE df.type_desc = N'ROWS';
```

Database-level companion (same session / DB):

```sql
SELECT
    SUM(total_page_count) / 128.0 AS DbAllocatedMB,
    SUM(allocated_extent_page_count) / 128.0 AS DbUsedMB,
    CASE
        WHEN SUM(total_page_count) > 0
        THEN 100.0 * SUM(allocated_extent_page_count) / SUM(total_page_count)
        ELSE NULL
    END AS DbDataUsedPercent
FROM sys.dm_db_file_space_usage;
```

`OUTER APPLY` (not `CROSS APPLY`) keeps file rows when volume stats fail due to permissions.

### Consolidated pattern (`#DataFileHeadroom`)

When many Hangfire databases share one instance and the login can open each DB, collect into a temp table and continue on errors:

```sql
IF OBJECT_ID('tempdb..#DataFileHeadroom') IS NOT NULL
    DROP TABLE #DataFileHeadroom;

CREATE TABLE #DataFileHeadroom
(
    DatabaseName         sysname       NOT NULL,
    FileId               int           NULL,
    FileName             sysname       NULL,
    CurrentSizeMB        decimal(18,2) NULL,
    UsedMB               decimal(18,2) NULL,
    FreeMB               decimal(18,2) NULL,
    FileUsedPercent      decimal(18,2) NULL,
    DbDataUsedPercent    decimal(18,2) NULL,
    MaxSizeMB            nvarchar(50)  NULL,
    GrowthMB             decimal(18,2) NULL,
    GrowthPercent        int           NULL,
    IsPercentGrowth      bit           NULL,
    VolumeMountPoint     nvarchar(512) NULL,
    VolumeTotalGB        decimal(18,2) NULL,
    VolumeFreeGB         decimal(18,2) NULL,
    VolumeFreePercent    decimal(18,2) NULL,
    ErrorMessage         nvarchar(4000) NULL
);

DECLARE @HangfireDbs TABLE (DatabaseName sysname PRIMARY KEY);
-- Populate @HangfireDbs at runtime from configuration.
-- Do not commit real database names into versioned docs.

DECLARE @db sysname;
DECLARE @sql nvarchar(max);

DECLARE db_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT DatabaseName FROM @HangfireDbs;

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @db;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @sql = N'
USE ' + QUOTENAME(@db) + N';

DECLARE @DbDataUsedPercent decimal(18,2);
SELECT @DbDataUsedPercent =
    CASE
        WHEN SUM(total_page_count) > 0
        THEN 100.0 * SUM(allocated_extent_page_count) / SUM(total_page_count)
        ELSE NULL
    END
FROM sys.dm_db_file_space_usage;

INSERT INTO #DataFileHeadroom
(
    DatabaseName, FileId, FileName,
    CurrentSizeMB, UsedMB, FreeMB, FileUsedPercent, DbDataUsedPercent,
    MaxSizeMB, GrowthMB, GrowthPercent, IsPercentGrowth,
    VolumeMountPoint, VolumeTotalGB, VolumeFreeGB, VolumeFreePercent
)
SELECT
    DB_NAME(),
    df.file_id,
    df.name,
    CAST(df.size AS bigint) / 128.0,
    CAST(FILEPROPERTY(df.name, N''SpaceUsed'') AS bigint) / 128.0,
    (CAST(df.size AS bigint)
        - CAST(FILEPROPERTY(df.name, N''SpaceUsed'') AS bigint)) / 128.0,
    CASE
        WHEN df.size > 0
        THEN 100.0 * CAST(FILEPROPERTY(df.name, N''SpaceUsed'') AS bigint)
                   / CAST(df.size AS bigint)
        ELSE NULL
    END,
    @DbDataUsedPercent,
    CASE
        WHEN df.max_size = -1 THEN N''UNLIMITED''
        WHEN df.max_size = 0 THEN N''NO_GROWTH''
        ELSE CONVERT(nvarchar(50),
             CAST(CAST(df.max_size AS bigint) / 128.0 AS decimal(18, 2)))
    END,
    CASE WHEN df.is_percent_growth = 1 THEN NULL
         ELSE CAST(df.growth AS bigint) / 128.0 END,
    CASE WHEN df.is_percent_growth = 1 THEN df.growth ELSE NULL END,
    df.is_percent_growth,
    vs.volume_mount_point,
    CAST(vs.total_bytes AS float) / 1073741824.0,
    CAST(vs.available_bytes AS float) / 1073741824.0,
    CASE
        WHEN vs.total_bytes IS NULL OR vs.total_bytes = 0 THEN NULL
        ELSE 100.0 * CAST(vs.available_bytes AS float)
                   / CAST(vs.total_bytes AS float)
    END
FROM sys.database_files AS df
OUTER APPLY sys.dm_os_volume_stats(DB_ID(), df.file_id) AS vs
WHERE df.type_desc = N''ROWS'';
';
        EXEC sys.sp_executesql @sql;
    END TRY
    BEGIN CATCH
        INSERT INTO #DataFileHeadroom (DatabaseName, ErrorMessage)
        VALUES (@db, ERROR_MESSAGE());
    END CATCH;

    FETCH NEXT FROM db_cursor INTO @db;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

SELECT *
FROM #DataFileHeadroom
ORDER BY
    DbDataUsedPercent DESC,
    VolumeFreePercent ASC;
```

**Fact — how this run was executed:** Because each monitored app uses its **own** database login, the practical collector was **one connection per configured application** (same SELECT shape), aggregating anonymized rows client-side. The `#DataFileHeadroom` script above is the same-instance consolidator when a single login can reach every Hangfire database.

### Snapshot results (anonymized)

Sorted by `DbDataUsedPercent DESC`, then `VolumeFreePercent ASC`.

| Application | File | Current MB | Used MB | Free MB | File Used % | Db Used % | MaxSize | Growth | Volume | Vol Free % |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | ---: |
| Application F | file 1 | 72.00 | 52.31 | 19.69 | 72.66 | 72.66 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application O | file 1 | 6.00 | 4.06 | 1.94 | 67.71 | 67.71 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application M | file 1 | 8.00 | 5.00 | 3.00 | 62.50 | 62.50 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application D | file 1 | 8.00 | 4.69 | 3.31 | 58.59 | 58.59 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application A | file 1 | 2061.00 | 967.50 | 1093.50 | 46.94 | 46.94 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application H | file 1 | 1049.00 | 301.75 | 747.25 | 28.77 | 28.77 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application B | file 1 | 392.00 | 77.12 | 314.88 | 19.67 | 19.67 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application J | file 1 | 50.12 | 7.19 | 42.94 | 14.34 | 14.34 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application I | file 1 | 37.00 | 5.06 | 31.94 | 13.68 | 13.68 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application E | file 1 | 69.25 | 9.44 | 59.81 | 13.63 | 13.63 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application Q | file 1 | 54.00 | 6.75 | 47.25 | 12.50 | 12.50 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application P | file 1 | 1054.44 | 124.75 | 929.69 | 11.83 | 11.83 | UNLIMITED | 1 MB | Volume 1 | 47.50 |
| Application C | file 1 | 72.00 | 4.88 | 67.12 | 6.77 | 6.77 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application N | file 1 | 95.44 | 6.44 | 89.00 | 6.75 | 6.75 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application K | file 1 | 464.44 | 23.56 | 440.88 | 5.07 | 5.07 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application G | file 1 | 6664.00 | 247.38 | 6416.62 | 3.71 | 3.71 | UNLIMITED | 64 MB | Volume 1 | 47.50 |
| Application L | file 1 | 2584.00 | 60.75 | 2523.25 | 2.35 | 2.35 | UNLIMITED | 1 MB | Volume 1 | 47.50 |

**Volume 1 (Fact):** Total ≈ **81.00 GB**, Free ≈ **38.47 GB**, Free ≈ **47.50%**. Shared by all 17 data files in this run.

### Findings (no thresholds declared)

1. **Headroom ≠ file fill %.** Every DB can still grow (`UNLIMITED` + autogrowth on) and the shared volume has ~half free. Highest fill here is **~72.7%**, not the ~98% cases from §3.
2. **File % and DB % matched** for all single-file databases in this snapshot (`FILEPROPERTY` vs `dm_db_file_space_usage`).
3. **No percent growth** observed — only fixed MB increments (**1 MB** or **64 MB**).
4. **No multi-file / multi-volume** Hangfire DBs in this set — the query shape still supports them.
5. **Volume stats worked** for these Monitor logins — good signal that headroom collection is feasible in this environment.
6. **Estate gap:** re-run when the remaining databases from the 23-DB briefing are configured, especially former high-fill cases.

### Illustrative cases (for later Health design — **not** thresholds)

These are conceptual checks enabled by the collected columns. **No Health rule is proposed here.**

| Case | Pattern | What the three levels say |
| --- | --- | --- |
| **A** | Used ~98%, `MAXSIZE = UNLIMITED`, VolumeFree ~40% | File looks “full” but growth + volume free ⇒ **not automatically a capacity incident** |
| **B** | Used ~98%, `MAXSIZE = CurrentSize` / `NO_GROWTH`, VolumeFree ~40% | File cannot grow even with free disk ⇒ **file ceiling** is the risk |
| **C** | Used ~80%, `UNLIMITED`, VolumeFree ~2% | Risk sits at **volume**, not the used-% number alone |
| **D** | Used ~50%, Max 1000 MB, Current 950 MB, VolumeFree ~50% | Remaining headroom ≈ **min(max−current, volume free)** ⇒ small despite low used % |

**Closest real analogue in this run to Case A (milder):** Application F — Used **72.66%**, MaxSize **UNLIMITED**, VolumeFree **47.50%**. Still **not** labeled a problem; shows why used-% alone is insufficient.

### Explicit non-goals (this section)

- No change to `DataFileSpaceHealthRules`, monitors, UI, or tests
- No new thresholds or Health column
- No treating high `DataUsedPercent` as Failure by itself

### Sources (this section)

- Microsoft Learn: `sys.database_files`, `FILEPROPERTY`, `sys.dm_os_volume_stats`, `sys.dm_db_file_space_usage`
- Execution: read-only queries against locally configured Hangfire databases (2026-09-27); anonymized results only

---

## Storage Health Implementation

**Status:** Implemented (Storage Health v1)  
**Date:** 2026-09-27  
**Scope:** Capacity Health + Diagnosis + Resolution guidance (read-only)

### Health signals used

| Signal | Role |
| --- | --- |
| Connection / storage open failure | **Unavailable** (FailureReason preserved for Diagnosis) |
| Data-file headroom: `FreeMB <= 0` **and** growth blocked by `MaxSize` (`Limited` with `CurrentSizeMB >= MaxSizeMB`, or `NO_GROWTH`) | **Warning** |
| Successful reads with no headroom Warning | **Healthy** (`OK`) |

UI labels: Healthy / Warning / Unavailable (domain enum still uses `OK` / `WARNING` / `UNAVAILABLE`). Capacity rules do not produce `CRITICAL`.

### Informational only (do not set capacity Health)

- Database `DataUsedPercent` / allocated fill (including values ≥ 80% / ≥ 90% / ~98%)
- Log used percent
- `log_reuse_wait_desc` alone (including `ACTIVE_TRANSACTION`)
- Active transaction count / oldest duration alone
- Active Hangfire servers (`Servers = 0` may still style the row; not capacity Health)
- Schema Version (own column/badge; not folded into capacity Health)
- Missing volume stats (`dm_os_volume_stats` null) — diagnostic note, never Critical/Warning by itself
- Missing headroom read (`DbException` on headroom) — capacity stays Healthy with diagnosis that headroom was unavailable

### Why 80/90 were removed

Estate evidence showed high used-of-allocated without proven capacity failure. Those constants were provisional and unjustified. `DataFileSpaceHealthRules` now maps valid metrics to **OK** only (or UNAVAILABLE for invalid/unreadable metrics). No active used-% thresholds remain in that class.

### File / volume headroom

Productized as `DataFileHeadroomReader` / `DataFileHeadroomQuery` (ROWS files: size, used, free, MaxSize, growth, volume via `OUTER APPLY sys.dm_os_volume_stats`).

`UNLIMITED` (`max_size = -1` or sentinel `268435456` pages) never invents a numeric MaxSize MB and does not produce Warning from MaxSize alone.

Warning requires **both** no free space inside the file **and** a configured growth ceiling. “Close to MaxSize” by percentage is **not** implemented (would invent a proximity threshold).

### Diagnosis and Resolution

- Detail page: Health badge → Diagnosis text → metric tables → **How to resolve** only when Warning.
- Diagnostic SQL and corrective SQL are shown as copy/paste text for authorized DBA tools.
- Hangfire Monitor **does not** execute corrective SQL, expose Execute buttons, admin APIs, or extra credentials.
- Identifiers in generated corrective examples are bracket/literal-escaped; placeholders (`<new_size_mb>`) used when a DBA-chosen size is required.

### Deliberately not Warning (v1)

- High Data/Log used % alone
- Volume nearly full alone (no calibrated free-% threshold)
- File near but below MaxSize with free space remaining
- Stale or live `ACTIVE_TRANSACTION` wait alone
- Schema mismatch (shown separately)
