// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal sealed class CiRunSummaryModule
{
    public string AssemblyName { get; set; } = string.Empty;

    public string ModulePath { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public string ExecutionId { get; set; } = string.Empty;

    public string SessionUid { get; set; } = string.Empty;

    public string RequestedOutputPath { get; set; } = string.Empty;

    public bool WriteOnFailureOnly { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? GitHubActionsStepSummaryEnabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubActionsHistoryPath { get; set; }

    public int GitHubActionsHistoryWindowInDays { get; set; }

    public int AttemptNumber { get; set; }

    public int ExitCode { get; set; }

    public long TotalTests { get; set; }

    public long PassedTests { get; set; }

    public long FailedTests { get; set; }

    public long SkippedTests { get; set; }

    public long TestDurationTicks { get; set; }

    public CiRunSummaryTest[] Failures { get; set; } = [];

    public CiRunSummaryTest[] FlakyTests { get; set; } = [];

    public CiRunSummaryTest[] SlowestTests { get; set; } = [];

    public CiRunSummaryHistoryTest[] HistoryTests { get; set; } = [];

    public CiRunSummaryDependency[] Dependencies { get; set; } = [];

    public CiRunSummaryFailingClass[] TopFailingClasses { get; set; } = [];

    public CiCoverageSummaryData Coverage { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? GitHubActionsStepSummarySections { get; set; }
}

internal static class CiRunSummaryModuleIdentity
{
    // Markdown builders call HasDuplicateModuleIdentity once per rendered module row and once per historical/dependency
    // test row (both proportional to the module count), so a naive per-call linear scan over 'modules' makes the whole
    // render O(modules^2) for a run with many test projects. A ConditionalWeakTable memoizes the duplicate-identity set
    // for a given 'modules' list, turning the repeated-scan cost into a single O(modules) pass the first time any
    // module in that list is queried.
#pragma warning disable IDE0028 // ConditionalWeakTable is not constructible via a collection expression on netstandard2.0; keep explicit construction for all TFMs.
    private static readonly ConditionalWeakTable<IReadOnlyList<CiRunSummaryModule>, HashSet<(string AssemblyName, string TargetFramework, string Architecture)>> DuplicateIdentitiesByModuleList = new ConditionalWeakTable<IReadOnlyList<CiRunSummaryModule>, HashSet<(string AssemblyName, string TargetFramework, string Architecture)>>();
#pragma warning restore IDE0028

    internal static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => GetDuplicateIdentities(modules).Contains((module.AssemblyName, module.TargetFramework, module.Architecture.ToUpperInvariant()));

    private static HashSet<(string AssemblyName, string TargetFramework, string Architecture)> GetDuplicateIdentities(IReadOnlyList<CiRunSummaryModule> modules)
        => DuplicateIdentitiesByModuleList.GetValue(modules, static list =>
        {
            Dictionary<(string AssemblyName, string TargetFramework, string Architecture), int> counts = [];
            foreach (CiRunSummaryModule candidate in list)
            {
                (string AssemblyName, string TargetFramework, string Architecture) key = (candidate.AssemblyName, candidate.TargetFramework, candidate.Architecture.ToUpperInvariant());
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }

            HashSet<(string AssemblyName, string TargetFramework, string Architecture)> duplicates = [];
            foreach (KeyValuePair<(string AssemblyName, string TargetFramework, string Architecture), int> entry in counts)
            {
                if (entry.Value > 1)
                {
                    duplicates.Add(entry.Key);
                }
            }

            return duplicates;
        });
}

internal sealed class CiRunSummaryTest
{
    public string DisplayName { get; set; } = string.Empty;

    public string FullyQualifiedName { get; set; } = string.Empty;

    public long DurationTicks { get; set; }

    /// <summary>
    /// Gets or sets the failure explanation (or exception message) of a failing test. Only populated for failures,
    /// and omitted from the fragment when absent so passing/slow-test entries stay small.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineNumber { get; set; }
}

internal sealed class CiRunSummaryHistoryTest
{
    public string TestId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FullyQualifiedName { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public long DurationTicks { get; set; }

    public bool IsFlaky { get; set; }

    public int HistoricalPassCount { get; set; }

    public int HistoricalFailCount { get; set; }

    public int HistoryWindowInDays { get; set; }

    public int DurationSampleCount { get; set; }

    public double P95DurationMilliseconds { get; set; }

    public double P99DurationMilliseconds { get; set; }
}

internal sealed class CiRunSummaryDependency
{
    public string DependentFullyQualifiedName { get; set; } = string.Empty;

    public string Prerequisite { get; set; } = string.Empty;

    public bool ProceedOnFailure { get; set; }
}

internal sealed class CiRunSummaryFailingClass
{
    public string ClassName { get; set; } = string.Empty;

    public long FailureCount { get; set; }
}

internal sealed class CiCoverageSummaryData
{
    public CiCoverageMetric[] Metrics { get; set; } = [];

    public CiCoverageThreshold[] Thresholds { get; set; } = [];

    public int ReportingModuleCount { get; set; }

    public int TotalModuleCount { get; set; }
}

internal sealed class CiCoverageMetric
{
    public CoverageScopeLevel ScopeLevel { get; set; }

    public string ScopeName { get; set; } = string.Empty;

    public CoverageMetric Metric { get; set; }

    public string CustomMetricName { get; set; } = string.Empty;

    public string ProducerId { get; set; } = string.Empty;

    public long CoveredCount { get; set; }

    public long CoverableCount { get; set; }
}

internal sealed class CiCoverageThreshold
{
    public string Source { get; set; } = string.Empty;

    public CoverageScopeLevel ScopeLevel { get; set; }

    public string ScopeName { get; set; } = string.Empty;

    public CoverageMetric Metric { get; set; }

    public string CustomMetricName { get; set; } = string.Empty;

    public string ProducerId { get; set; } = string.Empty;

    public CoverageAggregation Aggregation { get; set; }

    public CoverageScopeLevel? AggregatedOver { get; set; }

    public double ActualPercentage { get; set; }

    public double RequiredPercentage { get; set; }

    public bool HasCoverableData { get; set; }

    public bool Passed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExactActualPercentage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExactRequiredPercentage { get; set; }
}

internal sealed class CiRunSummaryAggregate
{
    public CiRunSummaryAggregate(
        IReadOnlyList<CiRunSummaryModule> modules,
        ArtifactPostProcessingContext context,
        long totalTests,
        long passedTests,
        long failedTests,
        long skippedTests,
        TimeSpan? duration,
        int? exitCode,
        bool hasAuthoritativeRunSummary,
        bool isPartial)
    {
        Modules = modules;
        Context = context;
        TotalTests = totalTests;
        PassedTests = passedTests;
        FailedTests = failedTests;
        SkippedTests = skippedTests;
        Duration = duration;
        ExitCode = exitCode;
        HasAuthoritativeRunSummary = hasAuthoritativeRunSummary;
        IsPartial = isPartial;
        FlakyTests =
        [
            .. modules
                .SelectMany(static module => module.FlakyTests)
                .OrderBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static test => test.DisplayName, StringComparer.Ordinal),
        ];
        IReadOnlyList<CiRunSummaryModule> coverageModules = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? modules.OrderBy(static module => module.AttemptNumber).Take(1).ToArray()
            : modules;
        Coverage = CiCoverageSummary.Aggregate(
            coverageModules,
            Math.Max(coverageModules.Count, context.RunSummary?.TestModuleCount ?? coverageModules.Count));
    }

    public IReadOnlyList<CiRunSummaryModule> Modules { get; }

    public ArtifactPostProcessingContext Context { get; }

    public long TotalTests { get; }

    public long PassedTests { get; }

    public long FailedTests { get; }

    public long SkippedTests { get; }

    public TimeSpan? Duration { get; }

    public int? ExitCode { get; }

    public bool HasAuthoritativeRunSummary { get; }

    public bool IsPartial { get; }

    public IReadOnlyList<CiRunSummaryTest> FlakyTests { get; }

    public CiCoverageSummaryData Coverage { get; }
}

#pragma warning restore RS0051
