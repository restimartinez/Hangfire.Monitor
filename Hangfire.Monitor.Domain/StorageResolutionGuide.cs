namespace Hangfire.Monitor.Domain;

/// <summary>
/// Operator/DBA guidance for resolving a storage capacity Warning.
/// Contains SQL text for external tools only — Hangfire Monitor never executes it.
/// </summary>
public sealed record StorageResolutionGuide(
    string WhatIsHappening,
    string WhatToCheck,
    string RecommendedAction,
    string DiagnosticQueriesSql,
    string CorrectiveQueriesSql,
    string RequiredPermissions,
    string ProductionWarning);
