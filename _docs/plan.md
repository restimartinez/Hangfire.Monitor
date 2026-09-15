# Hangfire Monitor — Incremental Implementation Plan

**Status:** Planning artifact (not an implementation request)  
**Based on:** `AGENTS.md`, `_docs/SPEC.md`, `_docs/hangfire-monitoring-api-investigation.md`  
**Date:** 2026-09-14

This plan decomposes the MVP into small, independently verifiable tasks for an AI coding agent. Execute **one task at a time**. Do not invent features outside `_docs/SPEC.md`.

---

## Guiding constraints

- .NET 9, C#, ASP.NET Core Razor Pages, xUnit
- Multi-app external config: `HangfireMonitor:Applications` (`Name`, `ConnectionString`, optional `Schema` default `HangFire`)
- Failed count: `IMonitoringApi.GetStatistics().Failed`
- Last failure: exact `MAX(FailedAt)` via narrow read-only SQL (after verification task)
- Statuses: `OK` | `FAILED` | `UNAVAILABLE`
- Zero failures UI: count `0`, last failure `-`
- No Hangfire writes; `PrepareSchemaIfNecessary = false`
- No auth, SPA, REST API, Testcontainers, MediatR, repositories, or speculative layers
- Packages (when added): Hangfire.Core 1.8.25, Hangfire.SqlServer 1.8.25, Microsoft.Data.SqlClient

---

## Phase 0 — Repository and project bootstrap

### HM-001 — Create solution and web project

**Purpose:** Establish the .NET 9 Razor Pages application host.

**Depends on:** None

**Expected change:**

* Create solution (for example `Hangfire.Monitor.sln`)
* Create ASP.NET Core Razor Pages web project targeting `net9.0`
* Keep default Razor Pages scaffolding minimal; no feature pages yet beyond template defaults as needed to build

**Verification:**

* `dotnet build` succeeds for the web project

---

### HM-002 — Create xUnit test project and wire references

**Purpose:** Enable automated tests from the start.

**Depends on:** HM-001

**Expected change:**

* Create xUnit test project targeting `net9.0`
* Add project reference from tests → web project (or shared library if one is introduced later; prefer referencing the web project only if types are testable there—otherwise introduce the smallest necessary project layout without over-splitting)
* Ensure solution includes both projects

**Verification:**

* `dotnet test` runs (may have zero tests yet, or a trivial placeholder if required by the template)
* `dotnet build` succeeds for the solution

---

### HM-003 — Establish repository structure and document commands

**Purpose:** Make the repo navigable and update agent command guidance.

**Depends on:** HM-002

**Expected change:**

* Confirm folder layout (solution, web, tests, `_docs/`, `AGENTS.md`)
* Update `AGENTS.md` Commands section with the real `dotnet` commands/project paths once known
* Ensure `.gitignore` continues to exclude secrets/build outputs (already present; adjust only if scaffolding requires it)

**Verification:**

* Solution builds
* Documented commands match actual project names

---

### HM-004 — Bootstrap milestone commit

**Purpose:** Record a clean baseline before feature work.

**Depends on:** HM-003

**Expected change:**

* Human/agent creates a meaningful git commit for bootstrap (only when explicitly asked to commit)

**Verification:**

* Working tree clean after commit
* `dotnet build` and `dotnet test` still pass

**Recommended commit point:** project bootstrap

---

## Phase 1 — Configuration

### HM-010 — Define configuration model types

**Purpose:** Represent `HangfireMonitor:Applications` in strongly typed options within a small domain project.

**Depends on:** HM-002

**Expected change:**

* Create `Hangfire.Monitor.Domain` class library targeting `net9.0` and add it to the solution
* Keep the project minimal: configuration POCOs only for this task—no interfaces, repositories, services, MediatR, CQRS, or speculative abstractions
* Place configuration model types in `Hangfire.Monitor.Domain` (monitor settings root + per-application entry); treat them as simple configuration/domain objects, not DDD entities
* Properties: `Name`, `ConnectionString`, `Schema` (optional)
* Default schema value `HangFire` when schema is missing/blank (implementation may apply default at bind or validation time—document which)
* Add project reference: `Hangfire.Monitor.Web` → `Hangfire.Monitor.Domain`
* Add project reference: `Hangfire.Monitor.Tests` → `Hangfire.Monitor.Domain` (tests may exercise these models directly; existing Tests → Web reference may remain)
* Do **not** introduce an Infrastructure (or other) project yet; Hangfire/SQL Server integration placement is deferred to later phases

**Verification:**

* Solution builds with Domain, Web, and Tests projects
* Unit tests can construct the model with and without schema (via Domain reference)

---

### HM-011 — Bind `HangfireMonitor` configuration

**Purpose:** Load applications from configuration.

**Depends on:** HM-010

**Expected change:**

* Bind `HangfireMonitor` section from configuration
* Register options with the DI container using standard ASP.NET Core options binding
* Add non-secret placeholder structure in `appsettings.json` (empty applications list or commented example without real secrets)

**Verification:**

* Unit/integration-style config test using `ConfigurationBuilder` in-memory JSON binds sample apps
* No real connection strings committed

---

### HM-012 — Validate application configuration

**Purpose:** Fail fast on invalid config; accept valid defaults.

**Depends on:** HM-011

**Expected change:**

* Validation rules:
  * `Name` required / non-whitespace
  * `ConnectionString` required / non-whitespace
  * `Schema` optional; default `HangFire`
  * Empty applications list is allowed at bind time (UI may show empty table)—or document chosen behavior consistently with SPEC (SPEC requires monitoring configured apps; empty list is valid config)
* Clear validation errors when names/connection strings missing

**Verification:**

* Tests: valid app with schema
* Tests: valid app without schema → defaults to `HangFire`
* Tests: missing name fails
* Tests: missing connection string fails

---

### HM-013 — Document local User Secrets / environment variable usage

**Purpose:** Enable local runs without committing secrets.

**Depends on:** HM-011

**Expected change:**

* Brief local-dev notes (README section or `_docs/` note only if already appropriate—prefer updating README lightly or adding a short “Local configuration” subsection in an existing doc; do **not** invent production secret infrastructure)
* Ensure User Secrets id is enabled on the web project if using user secrets
* Example of overriding connection strings via user secrets / env vars (documentation only; no real secrets)

**Verification:**

* Project supports user secrets (when using that path)
* Review confirms no connection strings in tracked files

**Recommended commit point:** configuration

---

## Phase 2 — Hangfire monitoring integration

### HM-020 — Add Hangfire and SQL client packages

**Purpose:** Introduce approved dependencies only.

**Depends on:** HM-001

**Expected change:**

* Add packages pinned per SPEC:
  * `Hangfire.Core` 1.8.25
  * `Hangfire.SqlServer` 1.8.25
  * `Microsoft.Data.SqlClient` (current stable compatible)
* Do **not** add `Hangfire.AspNetCore` / server packages unless later explicitly required (not required for MVP)

**Verification:**

* `dotnet restore` / `dotnet build` succeed
* Package versions match SPEC decision

---

### HM-021 — Create read-only SqlServerStorage factory per application

**Purpose:** Construct one independent storage instance per configured application without writes.

**Depends on:** HM-012, HM-020

**Expected change:**

* Small factory/helper that, given connection string + schema, creates `SqlServerStorage` with:
  * `PrepareSchemaIfNecessary = false`
  * `SchemaName` from config (default `HangFire`)
  * Does not set `JobStorage.Current` as the multi-app mechanism
* No Hangfire server registration

**Verification:**

* Unit tests assert options passed to storage construction where practical (wrapper/test seam if needed—keep minimal)
* Code review: schema prep disabled; no server startup

---

### HM-022 — Obtain failed job count via `IMonitoringApi.GetStatistics().Failed`

**Purpose:** Read failed count using Hangfire’s public monitoring API.

**Depends on:** HM-021

**Expected change:**

* Monitoring accessor that:
  * creates/uses storage for an application
  * calls `GetMonitoringApi()`
  * returns `GetStatistics().Failed`
* Propagates/surfaces storage/monitoring exceptions to the caller (isolation happens in Phase 5)

**Verification:**

* Unit tests with a fake/stub seam for monitoring API or statistics provider (no production DB required)
* Tests: returns provided failed count
* Tests: exception bubbles or is mapped as decided by the small accessor contract (document chosen behavior)

---

### HM-023 — Confirm error surface for monitoring count failures

**Purpose:** Make failure modes explicit before orchestration.

**Depends on:** HM-022

**Expected change:**

* Define how count retrieval failures are represented at the Hangfire-integration boundary (exception vs result type)—prefer the simplest approach that Phase 5 can isolate per app
* Ensure no partial write paths exist

**Verification:**

* Tests cover exception/failure path for count retrieval
* `dotnet test` passes

**Recommended commit point:** Hangfire integration (count path)

---

## Phase 3 — Latest failure

### HM-030 — Verify FailedAt SQL timestamp semantics

**Purpose:** Close the SPEC implementation verification item before writing production SQL.

**Depends on:** HM-020

**Expected change:**

* Inspect Hangfire SQL Server source (`SqlServerMonitoringApi` / related schema) and record in `_docs/` (short note in investigation appendix or `_docs/architecture.md` if created) the verified mapping between `FailedJobDto.FailedAt` and the SQL expression to use for `MAX(...)`
* Explicitly state the chosen column/expression and join predicates for “current Failed jobs only”
* Do **not** implement the application SQL query in this task beyond documenting the verified statement shape

**Verification:**

* Written verification conclusion exists in repo docs
* Conclusion cites Hangfire source behavior
* No assumption of `State.CreatedAt` unless the verification confirms it matches `FailedJobDto.FailedAt` semantics

---

### HM-031 — Implement narrow read-only latest-failure SQL query

**Purpose:** Obtain exact `MAX(FailedAt)` using only the aggregate required.

**Depends on:** HM-030, HM-021

**Expected change:**

* Read-only query using the verified timestamp expression
* Parameterize schema safely (no string concatenation vulnerabilities; follow Hangfire’s schema quoting approach or equivalent safe pattern)
* Return `DateTime?` (null when no failed jobs)
* Preserve timestamp value as returned by storage (no business timezone conversion)

**Verification:**

* Unit tests around SQL text/parameters construction and null/value mapping (prefer testing the query builder/mapper without a live DB)
* Confirm query selects only the aggregate (no job payload columns)

---

### HM-032 — Integrate latest-failure query with per-application settings

**Purpose:** Apply connection string + schema when querying last failure.

**Depends on:** HM-031, HM-012

**Expected change:**

* Accessor that runs the verified query for a configured application
* Uses configured schema defaulting rules
* Surfaces query/connection failures to caller

**Verification:**

* Tests for schema default application
* Tests for failure path without production DB

**Recommended commit point:** latest-failure SQL (after verification)

---

## Phase 4 — Application/domain monitoring result

### HM-040 — Define monitoring status and result model

**Purpose:** Represent per-application monitoring outcome in domain terms.

**Depends on:** HM-010

**Expected change:**

* Status enum/constants: `OK`, `FAILED`, `UNAVAILABLE`
* Result model fields:
  * application name
  * failed count (nullable or defaulted appropriately when unavailable)
  * latest failure timestamp (`DateTime?`)
  * status
  * monitoring error information where appropriate (for `UNAVAILABLE`)

**Verification:**

* Unit tests construct all three statuses
* Build succeeds

---

### HM-041 — Implement status business rules

**Purpose:** Map successful/failed monitoring data to SPEC statuses.

**Depends on:** HM-040

**Expected change:**

* Pure rules:
  * success + failed count `0` → `OK` (latest failure null/`-` at UI later)
  * success + failed count `> 0` → `FAILED` (latest failure may be present)
  * monitoring/storage failure → `UNAVAILABLE`
* Keep rules free of UI formatting and free of Hangfire types where practical

**Verification:**

* Tests: OK
* Tests: FAILED with count and timestamp
* Tests: UNAVAILABLE with error info
* Tests: zero failures keep latest failure empty/null at model level

**Recommended commit point:** monitoring domain model

---

## Phase 5 — Multi-application orchestration

### HM-050 — Orchestrate monitoring across all configured applications

**Purpose:** Produce a full result list even when individual storages fail.

**Depends on:** HM-022, HM-032, HM-041

**Expected change:**

* Service/orchestrator that:
  * iterates configured applications
  * monitors each independently (count + latest failure)
  * isolates per-app failures → `UNAVAILABLE` for that app only
  * returns results for all configured applications
* Deterministic ordering (recommend configuration order unless SPEC is later changed)

**Verification:**

* Tests with fakes/stubs for per-app monitor dependencies:
  * all healthy (`OK`)
  * failed jobs (`FAILED`)
  * one unavailable storage
  * multiple unavailable storages
  * mixed results
* No test requires production Hangfire databases

---

### HM-051 — Wire orchestrator into DI

**Purpose:** Make monitoring usable from Razor Pages.

**Depends on:** HM-050, HM-011

**Expected change:**

* Register orchestrator and monitoring collaborators in DI
* Keep registrations straightforward (no speculative lifetimes/patterns)

**Verification:**

* Application builds
* Smoke test that service resolves (optional small host test if cheap; otherwise manual `dotnet run` later in Phase 6)

**Recommended commit point:** monitoring logic / orchestration

---

## Phase 6 — Razor Pages UI

### HM-060 — Main monitoring page model

**Purpose:** Expose orchestrator results to the page.

**Depends on:** HM-051

**Expected change:**

* Razor Page at `/jobs/failed` (`Pages/Jobs/Failed`) that invokes the orchestrator
* Home (`/`, `Pages/Index`) is a landing page with navigation to Failed Jobs
* Page model carries the list of monitoring results
* No REST endpoints

**Verification:**

* Page compiles
* Basic page-model test if practical (orchestrator stubbed)

---

### HM-061 — Status table UI

**Purpose:** Display the MVP table clearly.

**Depends on:** HM-060

**Expected change:**

* Simple HTML table/list:
  * Application
  * Failed jobs
  * Last failure
  * Status distinguishable (`OK` / `FAILED` / `UNAVAILABLE`)
* Zero-failure display: count `0`, last failure `-`
* Unavailable display: clear status; count/last failure handling consistent (show `-` or omit numeric claims when unavailable—choose simplest consistent approach and cover with a page-level expectation)
* Timestamp format when present: clear local format such as `14/09/2026 11:42:37`
* No CSS/SPA frameworks unless already present from template; keep styling minimal

**Verification:**

* Manual `dotnet run` with sample config (user secrets) if available
* Unit tests for display formatting helpers if extracted (zero → `-`, timestamp format)

**Recommended commit point:** UI

---

## Phase 7 — Quality and hardening

### HM-070 — Full build and test suite

**Purpose:** Confirm MVP engineering bar.

**Depends on:** HM-061

**Expected change:**

* Fix any failing tests/build issues discovered in a full run
* No new features

**Verification:**

* `dotnet build`
* `dotnet test` (full suite green)

---

### HM-071 — Configuration and secrets review

**Purpose:** Enforce SPEC security constraints.

**Depends on:** HM-070

**Expected change:**

* Review tracked files for connection strings/secrets
* Confirm user secrets / env var path documented and usable
* Confirm validation still covers required fields

**Verification:**

* Checklist pass: no secrets in source control
* Sample local configuration instructions work

---

### HM-072 — Read-only and error-handling review

**Purpose:** Confirm Hangfire storages are never modified and failures are isolated.

**Depends on:** HM-070

**Expected change:**

* Code review notes (brief) confirming:
  * `PrepareSchemaIfNecessary = false`
  * no enqueue/retry/delete APIs used
  * per-app isolation remains intact
* Minimal logging review: errors for `UNAVAILABLE` are visible enough for local diagnosis without leaking secrets

**Verification:**

* Review checklist complete
* Tests for mixed/unavailable scenarios still pass

---

### HM-073 — Documentation and MVP acceptance pass

**Purpose:** Align docs with the implemented MVP and close the plan.

**Depends on:** HM-071, HM-072, HM-030

**Expected change:**

* Update `AGENTS.md` commands if needed
* Ensure FailedAt verification conclusion remains recorded
* Optionally note MVP completion against SPEC acceptance criteria (short checklist in `_docs/` or PR description—no speculative new docs)

**Verification:**

* SPEC acceptance criteria 1–12 can be checked off
* `dotnet build` / `dotnet test` green

**Recommended commit point:** MVP completion

---

## Dependency overview

```text
HM-001 → HM-002 → HM-003 → HM-004
                ↘
HM-001 → HM-020 → HM-021 → HM-022 → HM-023
                 ↘         ↘
                  HM-030 → HM-031 → HM-032
HM-010 → HM-011 → HM-012 → HM-013
   ↘                ↘
HM-040 → HM-041 ----→ HM-050 → HM-051 → HM-060 → HM-061
                                 ↑        ↑
                          HM-022/023   HM-032

HM-061 → HM-070 → HM-071
                ↘ HM-072 → HM-073
```

Parallelism note: Phase 1 (config) and package add (HM-020) can proceed after bootstrap; Phase 3 verification (HM-030) can run once packages/docs access exist and **must** complete before HM-031.

---

## Recommended commit milestones

| Milestone | After tasks | Suggested message theme |
| --- | --- | --- |
| Project bootstrap | HM-004 | Solution + web + test projects build |
| Configuration | HM-013 | HangfireMonitor options, validation, local secrets path |
| Hangfire integration | HM-023 | Read-only storage + failed count via Monitoring API |
| Latest failure | HM-032 | Verified SQL aggregate for MAX(FailedAt) |
| Monitoring logic | HM-051 | Domain status + multi-app orchestration |
| UI | HM-061 | Razor Pages status table |
| MVP completion | HM-073 | Hardening + docs/acceptance |

Do not auto-commit every task. Commit when asked, at these milestones, or when a coherent vertical slice is stable.

---

## Explicitly excluded from this plan

Anything listed as out of scope in `_docs/SPEC.md`, including retry/delete/enqueue, job details/exceptions/arguments, auth, notifications, charts, auto-refresh, config UI, REST/SPA, Testcontainers, and extra Hangfire server/dashboard hosting packages.

---

## AI Coding Workflow

How future coding work should be executed against this plan:

1. **Select one task** from this plan (single Task ID).
2. **Read** the relevant parts of `_docs/SPEC.md`, `_docs/hangfire-monitoring-api-investigation.md`, and `AGENTS.md`.
3. **Inspect** the existing implementation and tests.
4. **State a short implementation plan** for that task only (a few steps, assumptions, and verification).
5. **Implement only that task** — no speculative extras, no later phases.
6. **Run** the relevant `dotnet build` / `dotnet test` commands.
7. **Review the diff** for scope creep, secrets, and Hangfire write risks.
8. **Update documentation** only if a durable decision changed (for example FailedAt verification outcome).
9. **Stop and wait** for the next task assignment.

The agent must **not** automatically continue into subsequent tasks.
