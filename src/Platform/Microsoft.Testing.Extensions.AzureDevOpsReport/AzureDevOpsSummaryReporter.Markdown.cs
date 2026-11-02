// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    private const int MaxHistoricalTests = 20;
    private const int MaxHistoricalDurations = 10;
    private const int MaxDependencies = 50;
    private const int MaxAggregateFailureDetails = 20;
    private const int MaxSlowestTests = 10;
    private const int MaxSkipReasonRows = 10;
    private const string TestTimeExplanation = "> Test time is the sum of reported test durations, not elapsed time; tests may run concurrently.\n\n";

    internal static /* for testing */ string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker)
        => BuildMarkdown(records, assemblyName, targetFrameworkMoniker, new CiCoverageSummaryData());

    private static string BuildMarkdown(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string targetFrameworkMoniker,
        CiCoverageSummaryData coverage)
    {
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            assemblyName,
            targetFrameworkMoniker,
            "unknown",
            executionId: null,
            sessionUid: "direct",
            attemptNumber: 1,
            exitCode: 0,
            coverage: coverage);
        return BuildMarkdown(module);
    }

    private static string BuildMarkdown(CiRunSummaryModule module)
    {
        bool runFailed = module.FailedTests > 0 || module.ExitCode != 0;
        var builder = new StringBuilder();
        builder.Append("## ").Append(runFailed ? "❌" : "✅").Append(' ')
            .Append(EscapeCell(module.AssemblyName)).Append(" (")
            .Append(EscapeCell(module.TargetFramework)).Append(")\n\n");
        AppendStatusStrip(
            builder,
            module.TotalTests,
            module.PassedTests,
            module.FailedTests,
            module.SkippedTests,
            module.FlakyTests.Length,
            TimeSpan.FromTicks(module.TestDurationTicks),
            module.ExitCode,
            isRunElapsed: false);
        builder.Append(TestTimeExplanation);
        CiCoverageSummary.AppendMarkdown(builder, module.Coverage, headingLevel: 3);
        AppendSkipReasonsMarkdown(builder, [module], headingLevel: 3);
        _ = AppendModuleDiagnostics(
            builder,
            module,
            headingLevel: 3,
            includeInsights: true,
            includeExitCode: false,
            failureDetailLimit: MaxFirstFailingFqns);
        return builder.ToString();
    }
}
