// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal static partial class CiRunSummaryAggregation
{
    // Additive fields such as coverage and flaky tests keep the same schema version so newer and older
    // extension versions can still aggregate each other's fragments in mixed-version test runs.
    private const int SchemaVersion = 1;
    private const int MaxHistoryTests = 10_000;

    public static CiRunSummaryAggregate ReadAndAggregate(
        IReadOnlyList<InputArtifact> inputs,
        string provider,
        ArtifactPostProcessingContext context)
    {
        var modules = new List<CiRunSummaryModule>(inputs.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        int remainingHistoryTests = MaxHistoryTests;
        foreach (InputArtifact input in inputs)
        {
            CiRunSummaryFragment? fragment;
            using (FileStream stream = File.OpenRead(input.Path))
            {
                fragment = JsonSerializer.Deserialize(stream, CiRunSummaryJsonContext.Default.CiRunSummaryFragment);
            }

            if (fragment is null
                || fragment.SchemaVersion != SchemaVersion
                || !string.Equals(fragment.Provider, provider, StringComparison.Ordinal)
                || fragment.Module is null)
            {
                throw new FormatException($"Invalid {provider} summary fragment '{input.Path}'.");
            }

            ValidateModule(fragment.Module, input, context.Mode);
            if (fragment.Module.HistoryTests.Length > remainingHistoryTests)
            {
                fragment.Module.HistoryTests =
                [
                    .. fragment.Module.HistoryTests.Take(remainingHistoryTests),
                ];
            }

            remainingHistoryTests -= fragment.Module.HistoryTests.Length;
            string identity = GetModuleIdentity(fragment.Module);
            if (!identities.Add(identity))
            {
                throw new FormatException($"Duplicate {provider} summary fragment identity '{identity}'.");
            }

            modules.Add(fragment.Module);
        }

        modules.Sort(CompareModules);

        long observedPassed = 0;
        long observedFailed = 0;
        long observedSkipped = 0;
        foreach (CiRunSummaryModule module in modules)
        {
            observedPassed = checked(observedPassed + module.PassedTests);
            observedFailed = checked(observedFailed + module.FailedTests);
            observedSkipped = checked(observedSkipped + module.SkippedTests);
        }

        long observedTotal = checked(observedPassed + observedFailed + observedSkipped);
        ArtifactPostProcessingRunSummary? runSummary = context.RunSummary;
        return runSummary is null
            ? new CiRunSummaryAggregate(
                modules,
                context,
                observedTotal,
                observedPassed,
                observedFailed,
                observedSkipped,
                duration: null,
                exitCode: null,
                hasAuthoritativeRunSummary: false,
                isPartial: context.IsTruncated)
            : new CiRunSummaryAggregate(
                modules,
                context,
                runSummary.TotalTests,
                runSummary.PassedTests,
                runSummary.FailedTests,
                runSummary.SkippedTests,
                runSummary.Duration,
                runSummary.ExitCode,
                hasAuthoritativeRunSummary: true,
                isPartial: context.IsTruncated);
    }

    private static void ValidateModule(
        CiRunSummaryModule module,
        InputArtifact input,
        ArtifactPostProcessingMode mode)
    {
        if (RoslynString.IsNullOrWhiteSpace(module.AssemblyName)
            || RoslynString.IsNullOrWhiteSpace(module.ModulePath)
            || RoslynString.IsNullOrWhiteSpace(module.TargetFramework)
            || RoslynString.IsNullOrWhiteSpace(module.Architecture)
            || RoslynString.IsNullOrWhiteSpace(module.SessionUid)
            || module.AttemptNumber <= 0
            || module.TotalTests < 0
            || module.PassedTests < 0
            || module.FailedTests < 0
            || module.SkippedTests < 0
            || module.TestDurationTicks < 0
            || (module.GitHubActionsHistoryPath is not null
                && (RoslynString.IsNullOrWhiteSpace(module.GitHubActionsHistoryPath)
                    || module.GitHubActionsHistoryWindowInDays is < 1 or > 90))
            || checked(module.PassedTests + module.FailedTests + module.SkippedTests) != module.TotalTests
            || module.Failures is null
            || module.FlakyTests is null
            || module.SlowestTests is null
            || module.HistoryTests is null
            || module.HistoryTests.Length > MaxHistoryTests
            || module.TopFailingClasses is null
            || module.Coverage is null
            || module.Coverage.Metrics is null
            || module.Coverage.Thresholds is null
            || module.Failures.Any(test => !IsValidTest(test))
            || module.FlakyTests.Any(test => !IsValidTest(test))
            || module.SlowestTests.Any(test => !IsValidTest(test))
            || module.HistoryTests.Any(static test =>
                RoslynString.IsNullOrWhiteSpace(test.TestId)
                || RoslynString.IsNullOrWhiteSpace(test.DisplayName)
                || RoslynString.IsNullOrWhiteSpace(test.FullyQualifiedName)
                || test.Outcome is not ("passed" or "failed" or "skipped")
                || test.DurationTicks < 0)
            || module.TopFailingClasses.Any(item => RoslynString.IsNullOrWhiteSpace(item.ClassName) || item.FailureCount <= 0)
            || module.Coverage.Metrics.Any(metric =>
                RoslynString.IsNullOrWhiteSpace(metric.ProducerId)
                || metric.CoveredCount < 0
                || metric.CoverableCount < metric.CoveredCount)
            || module.Coverage.Thresholds.Any(threshold =>
                RoslynString.IsNullOrWhiteSpace(threshold.ProducerId)
                || double.IsNaN(threshold.ActualPercentage)
                || double.IsNaN(threshold.RequiredPercentage)
                || threshold.ActualPercentage < 0
                || threshold.ActualPercentage > 100
                || threshold.RequiredPercentage < 0
                || threshold.RequiredPercentage > 100))
        {
            throw new FormatException($"Invalid CI summary module in '{input.Path}'.");
        }

        if (input.ProducingTestModule is not null
            && !PathsEqual(input.ProducingTestModule, module.ModulePath))
        {
            throw new FormatException($"CI summary module provenance does not match '{input.Path}'.");
        }

        if (input.TargetFramework is not null
            && !string.Equals(input.TargetFramework, module.TargetFramework, StringComparison.Ordinal))
        {
            throw new FormatException($"CI summary target framework provenance does not match '{input.Path}'.");
        }

        if (input.Architecture is not null
            && !string.Equals(input.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"CI summary architecture provenance does not match '{input.Path}'.");
        }

        if (input.ExecutionId is not null
            && (mode == ArtifactPostProcessingMode.RetryAttempts
                ? !int.TryParse(input.ExecutionId, NumberStyles.None, CultureInfo.InvariantCulture, out int attempt)
                    || attempt != module.AttemptNumber
                : !string.Equals(input.ExecutionId, module.ExecutionId, StringComparison.Ordinal)))
        {
            throw new FormatException($"CI summary execution provenance does not match '{input.Path}'.");
        }
    }

    private static bool IsValidTest(CiRunSummaryTest test)
        => !RoslynString.IsNullOrWhiteSpace(test.DisplayName)
            && !RoslynString.IsNullOrWhiteSpace(test.FullyQualifiedName)
            && test.DurationTicks >= 0
            && test.LineNumber is null or >= 0;

    private static int CompareModules(CiRunSummaryModule left, CiRunSummaryModule right)
    {
        int result = StringComparer.Ordinal.Compare(left.AssemblyName, right.AssemblyName);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.TargetFramework, right.TargetFramework);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.Architecture, right.Architecture);
        result = result != 0 ? result : left.AttemptNumber.CompareTo(right.AttemptNumber);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.ModulePath, right.ModulePath);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.ExecutionId, right.ExecutionId);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left.SessionUid, right.SessionUid);
    }

    private static string GetModuleIdentity(CiRunSummaryModule module)
        => $"{module.ModulePath}\0{module.TargetFramework}\0{module.Architecture}\0{module.ExecutionId}\0{module.AttemptNumber}\0{module.SessionUid}";

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

#pragma warning restore RS0051
