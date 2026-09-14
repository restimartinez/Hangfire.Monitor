# Hangfire Monitor — Agent Instructions

This repository is developed with an AI-native workflow. Treat the coding agent as a collaborator: propose small changes, make assumptions explicit, implement incrementally, and verify with tests. Do not generate the whole application in one pass.

## Project

Hangfire Monitor is a small ASP.NET Core web application that monitors Hangfire applications.

| Item | Value |
| --- | --- |
| Runtime | .NET 9 |
| Language | C# |
| Data store | SQL Server (Hangfire storage databases) |
| Initial focus | Monitoring only |
| First MVP | Read Hangfire SQL Server storage and display failed job information |
| Quality bar | Simple, maintainable, and testable |

Out of scope unless explicitly requested: Hangfire job enqueueing/processing, multi-product monitoring, complex dashboards, authentication/authorization systems, and unrelated platform features.

## Working style

1. Prefer the smallest change that advances the current request.
2. Implement only what was asked. Do not add “nice to have” features, speculative abstractions, or future-proofing layers.
3. Prefer clean, direct code over frameworks-within-frameworks. Avoid unnecessary interfaces, repositories, mediators, and generic helpers unless a concrete need is clear.
4. Keep changes reviewable: one concern per change set when practical.
5. State assumptions before coding when requirements are ambiguous. Prefer asking over inventing product behavior.
6. When an architectural decision is made, document it briefly (see Documents) instead of leaving it only in chat history.
7. Prefer evolving existing code over introducing parallel patterns.

## Implementation loop

For each task:

1. Restate the goal and acceptance criteria in one or two sentences.
2. List assumptions and open questions.
3. Propose a short plan (a few steps), then implement.
4. Add or update tests for the behavior changed.
5. Run the relevant tests and fix failures before considering the task done.
6. Summarize what changed and what was deliberately left out.

Do not start application scaffolding or feature code until asked. Repository instruction and planning documents come first.

## Testing

- Every non-trivial behavior change should include automated tests.
- Prefer focused unit tests for pure logic; use integration tests only where they give clear confidence (for example, SQL access against Hangfire schema).
- Tests must be deterministic and independent of a developer’s local Hangfire instance unless an explicit test fixture/container is part of the task.
- Do not weaken production code to make tests pass; fix the test or the design.

## .NET conventions

- Target `net9.0`.
- Follow existing project structure once it exists; do not invent a second layout.
- Prefer ASP.NET Core idioms already in use in the solution.
- Keep configuration explicit (`appsettings`, environment variables, user secrets). Never commit secrets.
- Treat Hangfire SQL Server storage as an external system. Prefer read-only access for monitoring unless a later task explicitly requires writes.
- Prefer clarity over cleverness. Names should reflect Hangfire/monitoring concepts the team already uses.

## Documents

Keep durable context in the repository, not only in chat.

| Document | Purpose |
| --- | --- |
| `AGENTS.md` | Always-on agent rules (this file) |
| `_docs/plan.md` | Product/MVP specification (create when scoping) |
| `_docs/repository.md` | Solution layout and local `dotnet` commands |
| `_docs/process.md` | How work is organized (tasks, commits, review) |
| `_docs/architecture.md` | Architectural decisions and rejected alternatives |
| `_docs/testing.md` | Testing conventions once the test project exists |

When correcting the agent during a session, update the relevant document so the next session inherits the rule.

Before UI work, check for a design note under `_docs/`. Before data-access work, check `_docs/architecture.md` for Hangfire schema and connection assumptions.

## Commands

Run from the repository root. See `_docs/repository.md` for layout details.

```text
dotnet restore
dotnet build
dotnet test
dotnet run --project Hangfire.Monitor.Web
```

## Hard stops

Stop and ask the human before:

- Adding NuGet packages that meaningfully change architecture (ORMs, UI frameworks, Hangfire server packages, auth stacks)
- Changing the target framework or introducing a second application host
- Writing to Hangfire storage tables
- Expanding scope beyond failed-job monitoring for the MVP
- Committing secrets, connection strings with credentials, or production data dumps
