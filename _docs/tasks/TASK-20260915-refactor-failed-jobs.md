# Refactor Failed Jobs to `/jobs/failed`

## Objective

Move the existing Failed Jobs monitoring page from the Home page to a dedicated Razor Page available at:

`/jobs/failed`

The Home page should become a simple application landing page with navigation to the Failed Jobs monitoring page.

This task is a structural and visual refactor. Existing Failed Jobs functionality must remain unchanged.

---

## Current situation

The Failed Jobs functionality is currently implemented in:

* `Pages/Index.cshtml`
* `Pages/Index.cshtml.cs`

The page currently acts as both the application Home page and the Failed Jobs monitoring page.

The application should now separate these responsibilities.

---

## Target structure

The Razor Pages structure should become:

```text
Pages/
├── Index.cshtml
├── Index.cshtml.cs
│
└── Jobs/
    ├── Failed.cshtml
    └── Failed.cshtml.cs
```

The resulting routes should be:

```text
/              → Home
/jobs/failed   → Failed Jobs
```

---

## Requirements

### 1. Move Failed Jobs page

Move the existing Failed Jobs Razor Page from:

```text
Pages/Index.cshtml
Pages/Index.cshtml.cs
```

to:

```text
Pages/Jobs/Failed.cshtml
Pages/Jobs/Failed.cshtml.cs
```

The new page must explicitly use:

```text
/jobs/failed
```

as its route.

The existing Failed Jobs functionality must be preserved.

Do not redesign or rewrite the monitoring logic unnecessarily.

---

### 2. Preserve existing functionality

The `/jobs/failed` page must continue to provide all functionality currently available on the Home page, including:

* Configured Hangfire applications
* Failed jobs count
* Last failure
* Application status
* `OK`
* `FAILED`
* `UNAVAILABLE`
* Independent handling of application failures
* Existing date/time formatting
* Existing client-side sorting introduced by HM-081 (default **Failed jobs** descending on page load)

No functional regression should be introduced.

---

### 3. Home page

`Pages/Index.cshtml` should become a dedicated Home page.

It should provide:

* Application name: `Hangfire Monitoring`
* A short description of the application
* A clear entry point to Failed Jobs

Example content:

```text
Hangfire Monitoring

Monitor your Hangfire applications from a single place.

Failed Jobs

Monitor failed jobs across all configured Hangfire applications.

View failed jobs →
```

The exact wording and visual presentation may be improved as long as the purpose remains clear.

`Pages/Index.cshtml.cs` should contain only the logic required by the Home page.

If no server-side logic is required, keep the PageModel minimal.

---

### 4. Navigation

Add simple application navigation.

The user must be able to:

* Navigate from Home to `/jobs/failed`
* Navigate from Failed Jobs back to Home

Navigation should be implemented using standard Razor/HTML links.

Avoid introducing a frontend framework or external navigation dependency.

---

### 5. Visual improvements

Use this task to establish a simple and consistent visual structure.

The UI should include, where appropriate:

* Application header
* Navigation
* Clear page title
* Page subtitle/description
* Consistent spacing
* Card-style presentation on Home
* Clear Failed Jobs table
* Visually distinguishable status badges
* Hover/focus states for interactive elements
* Basic responsive behavior

Keep the implementation lightweight.

Do not introduce a CSS framework or frontend dependency unless one already exists in the project.

Prefer the existing project CSS approach.

---

### 6. Existing HM-081 functionality

HM-081 introduced client-side sorting for the Failed Jobs table.

This functionality must continue working after the move.

Preserve:

* `data-sort-type`
* `aria-sort`
* `data-sort-value`
* Numeric sorting for Failed Jobs
* ISO/date sorting for Last Failure
* Existing JavaScript behavior
* Existing CSS required for sorting

If paths or script references need to change because the Razor Page moved, update them appropriately.

Do not remove or replace the sorting implementation.

---

## Constraints

Do not:

* Add new NuGet packages
* Add a frontend framework
* Add an API
* Add authentication
* Add new Hangfire functionality
* Change the Hangfire monitoring logic
* Change SQL queries
* Change Domain classes
* Change Infrastructure behavior
* Introduce repositories or unnecessary abstractions
* Introduce JavaScript frameworks
* Introduce CSS frameworks

The goal is a clean structural and visual refactor, not a functional expansion.

---

## Documentation

Update project documentation if it currently describes the Failed Jobs page as the Home page.

The documentation should reflect:

```text
/              → Home
/jobs/failed   → Failed Jobs
```

Also document the new `Pages/Jobs/` structure if the architecture documentation describes the Razor Pages structure.

---

## Acceptance Criteria

### Routing

* [ ] `/` loads the Home page.
* [ ] `/jobs/failed` loads the Failed Jobs page.
* [ ] The old Home route no longer serves the Failed Jobs page.

### Structure

* [ ] `Pages/Jobs/Failed.cshtml` exists.
* [ ] `Pages/Jobs/Failed.cshtml.cs` exists.
* [ ] Failed Jobs functionality has been removed from `Pages/Index.cshtml`.
* [ ] `Pages/Index.cshtml.cs` contains only Home-related logic.

### Functionality

* [ ] All configured applications are still displayed.
* [ ] Failed job counts are unchanged.
* [ ] Last failure values are unchanged.
* [ ] `OK`, `FAILED`, and `UNAVAILABLE` statuses still work.
* [ ] Failure in one application does not prevent other applications from being displayed.
* [ ] Existing date/time formatting is preserved.
* [ ] HM-081 client-side sorting still works.

### Navigation

* [ ] Home contains a link to `/jobs/failed`.
* [ ] Failed Jobs contains a link back to `/`.

### Visual

* [ ] Home has a clear application title.
* [ ] Home has a clear Failed Jobs entry point.
* [ ] Failed Jobs has a clear page title and description.
* [ ] Existing table remains readable.
* [ ] Statuses remain visually distinguishable.
* [ ] Navigation is visually clear.
* [ ] Layout works reasonably on smaller screens.

### Quality

* [ ] No new NuGet packages are introduced.
* [ ] No unnecessary architectural abstractions are introduced.
* [ ] Existing tests pass.
* [ ] `dotnet build` succeeds.
* [ ] No compiler warnings are introduced by this task.
* [ ] No existing functionality is removed unintentionally.
