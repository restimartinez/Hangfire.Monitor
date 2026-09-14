# Hangfire Monitor — Repository layout and local commands

**Status:** Bootstrap reference (HM-003)  
**Date:** 2026-09-14  
**Related:** `AGENTS.md`, `_docs/plan.md`

Concise map of the current solution layout and the `dotnet` commands used from the repository root.

---

## Layout

```text
Hangfire.Monitor/
├── Hangfire.Monitor.sln
├── Hangfire.Monitor.Domain/       # Configuration / domain POCOs
├── Hangfire.Monitor.Web/          # ASP.NET Core Razor Pages host
├── Hangfire.Monitor.Tests/        # xUnit test project
├── _docs/                         # Specs, plan, investigations
├── AGENTS.md
├── README.md
├── LICENSE
└── .gitignore
```

| Path | Role |
| --- | --- |
| `Hangfire.Monitor.sln` | Solution file; includes Domain, Web, and Tests |
| `Hangfire.Monitor.Domain` | Class library; Hangfire Monitor configuration/domain POCOs |
| `Hangfire.Monitor.Web` | Web application (`Microsoft.NET.Sdk.Web`); references Domain |
| `Hangfire.Monitor.Tests` | Unit tests; references Domain and Web |
| `_docs/` | Product/planning documentation |

All three projects target **`net9.0`**.

---

## Commands (from repository root)

Restore dependencies (optional; `build` / `test` restore as needed):

```text
dotnet restore
```

Build the solution:

```text
dotnet build
```

Run tests:

```text
dotnet test
```

Run the web application:

```text
dotnet run --project Hangfire.Monitor.Web
```

Stop the web process with `Ctrl+C` when finished.

Equivalent forms that also work from the root:

```text
dotnet build Hangfire.Monitor.sln
dotnet test Hangfire.Monitor.sln
```
