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

    public CiRunSummaryFailingClass[] TopFailingClasses { get; set; } = [];

    public CiCoverageSummaryData Coverage { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? GitHubActionsStepSummarySections { get; set; }
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
