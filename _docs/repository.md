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
├── Hangfire.Monitor.Domain/           # Configuration / domain POCOs
├── Hangfire.Monitor.Infrastructure/   # Hangfire SQL Server integration
│   └── Storage/
├── Hangfire.Monitor.Web/              # ASP.NET Core Razor Pages host
├── Hangfire.Monitor.Tests/            # xUnit test project
├── _docs/                             # Specs, plan, investigations
├── AGENTS.md
├── README.md
├── LICENSE
└── .gitignore
```

| Path | Role |
| --- | --- |
| `Hangfire.Monitor.sln` | Solution file; includes Domain, Infrastructure, Web, and Tests |
| `Hangfire.Monitor.Domain` | Class library; Hangfire Monitor configuration/domain POCOs |
| `Hangfire.Monitor.Infrastructure` | Class library; Hangfire SQL Server integration (e.g. `Storage/SqlServerStorageFactory`) |
| `Hangfire.Monitor.Web` | Web application (`Microsoft.NET.Sdk.Web`); references Domain and Infrastructure |
| `Hangfire.Monitor.Tests` | Unit tests; references Domain and Web |
| `_docs/` | Product/planning documentation |

All four projects target **`net9.0`**.

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

---

## Local configuration (connection strings)

Monitored Hangfire applications are configured under `HangfireMonitor:Applications`.

**Do not store real connection strings in `appsettings.json` or commit them to Git.** Keep tracked `appsettings.json` free of secrets (an empty `Applications` list is fine). Provide real connection strings with **User Secrets** (local development) or **environment variables**.

ASP.NET Core loads these automatically; no application code is required to read secrets manually.

### User Secrets (local development)

The Web project has User Secrets enabled. From the repository root:

```text
dotnet user-secrets set --project Hangfire.Monitor.Web "HangfireMonitor:Applications:0:Name" "Example"
dotnet user-secrets set --project Hangfire.Monitor.Web "HangfireMonitor:Applications:0:ConnectionString" "Server=localhost;Database=ExampleHangfire;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set --project Hangfire.Monitor.Web "HangfireMonitor:Applications:0:Schema" "HangFire"
```

That is equivalent to this structure (fictitious values only):

```json
{
  "HangfireMonitor": {
    "Applications": [
      {
        "Name": "Example",
        "ConnectionString": "Server=localhost;Database=ExampleHangfire;Trusted_Connection=True;TrustServerCertificate=True;",
        "Schema": "HangFire"
      }
    ]
  }
}
```

Add further applications with the next index (`:1:`, `:2:`, and so on).

Useful commands:

```text
dotnet user-secrets list --project Hangfire.Monitor.Web
dotnet user-secrets clear --project Hangfire.Monitor.Web
```

### Environment variables

Hierarchical configuration keys use `:` in appsettings / User Secrets. Environment variables use `__` (double underscore) in place of `:`.

| Configuration key | Environment variable |
| --- | --- |
| `HangfireMonitor:Applications:0:ConnectionString` | `HangfireMonitor__Applications__0__ConnectionString` |

Example (PowerShell), using a fictitious connection string:

```powershell
$env:HangfireMonitor__Applications__0__Name = "Example"
$env:HangfireMonitor__Applications__0__ConnectionString = "Server=localhost;Database=ExampleHangfire;Trusted_Connection=True;TrustServerCertificate=True;"
$env:HangfireMonitor__Applications__0__Schema = "HangFire"
```

Focus on providing `ConnectionString` (and the matching `Name` / optional `Schema`) for each application index you need; other Hangfire Monitor settings follow the same `__` convention when required.
