// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    private void AppendTestRunSummary(ITerminal terminal)
    {
        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (_artifacts.Count > 0)
        {
            // Add extra empty line when we will be writing any artifacts, to split it from previous output.
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, TestRunArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? TerminalResources.OutOfProcessArtifactsProduced : TerminalResources.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!RoslynString.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(TerminalResources.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        terminal.AppendLine();

        List<TestProgressState> assemblies = [.. _assemblies.Values.OrderBy(static a => a.Id)];

        // Single-pass aggregation: compute all summary counters in one foreach instead of
        // 7 separate LINQ calls (Sum×5, Any×1, Count×1), saving 6 extra O(N) passes and
        // 7 LINQ enumerator allocations per test run.
        int totalTests = 0;
        int totalFailedTests = 0;
        int totalSkippedTests = 0;
        int totalPassedTests = 0;
        int totalRetried = 0;
        bool anyAssemblyFailed = false;
        int failedAssembliesWithoutFailedTests = 0;

        foreach (TestProgressState assembly in assemblies)
        {
            totalTests += assembly.TotalTests;
            totalFailedTests += assembly.FailedTests;
            totalSkippedTests += assembly.SkippedTests;
            totalPassedTests += assembly.PassedTests;
            totalRetried += assembly.RetriedFailedTests;
            if (!assembly.Success)
            {
                anyAssemblyFailed = true;
                if (assembly.FailedTests == 0)
                {
                    failedAssembliesWithoutFailedTests++;
                }
            }
        }

        // The `--zero-tests-policy` decision is mirrored here: under the default `allow-skipped` an all-skipped run is
        // reported as a passing run instead of red "Zero tests ran"; under `strict` it is reported as "Zero tests ran".
        //
        // Two sibling sites mirror this decision and must stay in lockstep:
        //   - TestApplicationResult.ConsumeAsync (excludes skipped from `_totalRanTests` -> exit code 8)
        //   - Microsoft.Testing.Platform.MSBuild InvokeTestingPlatformTask (run-summary verdict)
        // Orchestrator-only: an assembly whose process ended unsuccessfully (crash / non-zero exit) with no failed
        // tests is still a run failure. Gated on ShowAssembly (the orchestrator marker): the in-process host leaves
        // ShowAssembly off and never sets Success, so this stays false and its verdict/color are unchanged.
        bool hasFailedAssemblies = _options.ShowAssembly && anyAssemblyFailed;

        bool runFailed = TestRunSummaryHelper.IsRunFailed(totalTests, totalFailedTests, totalSkippedTests, WasCancelled, _options.MinimumExpectedTests, _options.ZeroTestsPolicy) || HasHandshakeFailure || hasFailedAssemblies;
        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);

        terminal.Append(TerminalResources.TestRunSummary);
        terminal.Append(' ');
        terminal.Append(TestRunSummaryHelper.GetVerdictText(totalTests, totalFailedTests, totalSkippedTests, WasCancelled, _options.MinimumExpectedTests, HasHandshakeFailure, hasFailedAssemblies, _options.ZeroTestsPolicy));

        // For a single assembly (the in-process host) the verdict is followed by the assembly link, exactly as
        // before. For multiple assemblies (the dotnet test orchestrator) the per-assembly identity is rendered in
        // the progress area, so we keep the run-level verdict line link-free.
        if (assemblies.Count == 1)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblies[0]);
        }

        terminal.AppendLine();

        // For the dotnet test orchestrator (ShowAssembly) running more than one assembly, list each assembly with
        // its own result + compact counts under the run-level verdict. Additive: the in-process host leaves
        // ShowAssembly off, so this block never runs and its summary stays byte-identical.
        if (_options.ShowAssembly && assemblies.Count > 1)
        {
            foreach (TestProgressState assemblyRun in assemblies)
            {
                terminal.Append(SingleIndentation);
                AppendAssemblySummary(assemblyRun, terminal);
            }

            terminal.AppendLine();
        }

        int total = totalTests;
        int failed = totalFailedTests;
        int passed = totalPassedTests;
        int skipped = totalSkippedTests;
        int retried = totalRetried;

        // Orchestrator-only: count assemblies that ended unsuccessfully without a failed test (crash / non-zero exit)
        // plus handshake failures. These are surfaced as an "error: N" line so they aren't hidden behind a zero
        // failed-test count. In-process leaves ShowAssembly off and never has handshake failures, so error is 0.
        int error = (_options.ShowAssembly ? failedAssembliesWithoutFailedTests : 0) + HandshakeFailureCount;
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && failed == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && failed == 0;

        string errorText = $"{SingleIndentation}{TerminalResources.Error}: {error}";
        string totalText = $"{SingleIndentation}{TerminalResources.TotalLowercase}: {total}";
        string failedText = $"{SingleIndentation}{TerminalResources.FailedLowercase}: {failed}";
        string passedText = $"{SingleIndentation}{TerminalResources.SucceededLowercase}: {passed}";
        string skippedText = $"{SingleIndentation}{TerminalResources.SkippedLowercase}: {skipped}";
        string durationText = $"{SingleIndentation}{TerminalResources.DurationLowercase}: ";

        if (error > 0)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.AppendLine(errorText);
            terminal.ResetColor();
            terminal.AppendLine();
        }

        terminal.ResetColor();
        terminal.Append(totalText);

        // Orchestrator-only: when failed tests were retried, append "(+N retried)" after the total so the headline
        // count (which reflects the final attempt) is reconciled with the extra retried executions. retried is 0 for
        // the in-process host, so the total line stays byte-identical there.
        if (retried > 0)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append($" (+{retried} {TerminalResources.Retried})");
            terminal.ResetColor();
        }

        terminal.AppendLine();
        if (colorizeFailed)
        {
            terminal.SetColor(TerminalColor.DarkRed);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.DarkGreen);
        }

        terminal.AppendLine(passedText);

        if (colorizePassed)
        {
            terminal.ResetColor();
        }

        if (colorizeSkipped)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
        }

        terminal.AppendLine(skippedText);

        if (colorizeSkipped)
        {
            terminal.ResetColor();
        }

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();

        // Optional "Slowest tests" section (opt-in via --show-slowest-tests). Additive: no-op when the feature is
        // off, so the summary stays byte-identical for the default run.
        AppendSlowestTests(terminal, assemblies);

        // Re-print any handshake failures (orchestrator-only) at the very end so they aren't lost above the summary.
        // No-op for the in-process host, which never reports handshake failures.
        AppendHandshakeFailureRecap(terminal);

        // Re-print any assemblies that errored (non-zero exit with no failed test) for the same reason: the inline
        // process output is otherwise buried in the middle of a large run. No-op for the in-process host.
        AppendErroredAssemblyRecap(terminal);
    }

    /// <summary>
    /// Orchestrator overload (<c>dotnet test</c>): the multi-process orchestrator also knows each discovered test's
    /// uid, file path and line number. The shared discovery summary currently lists display names only, so those are
    /// accepted for signature parity. When <paramref name="displayName"/> is missing the <paramref name="uid"/> is used
    /// as the listed name; when neither is available the test is still counted (so the discovery total stays correct)
    /// but no blank entry is added to the summary.
    /// </summary>
    internal void TestDiscovered(string executionId, string? displayName, string? uid, string? filePath, int? lineNumber)
    {
        // Prefer the display name, fall back to the uid so the discovered test is still listed by something.
        string? name = displayName ?? uid;
        if (name is not null)
        {
            TestDiscovered(executionId, name);
            return;
        }

        // No name available at all: still increment the discovered count so the discovery summary total stays
        // correct (in discovery mode TotalTests is computed from DiscoveredTests), but avoid adding a blank entry.
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        asm.ReportDiscoveredTest(displayName: null);
        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    internal void TestDiscovered(string executionId, string displayName)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        // In discovery mode TotalTests is computed from DiscoveredTests; in execution mode it is computed from the
        // passed/skipped/failed tally as tests complete. So we only need to bump the discovered count here.
        asm.ReportDiscoveredTest(MakeControlCharactersVisible(displayName, true));

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal)
    {
        List<TestProgressState> assemblies = [.. _assemblies.Values.OrderBy(static a => a.Id)];
        terminal.AppendLine();

        int totalTests = assemblies.Sum(static a => a.TotalTests);
        bool runFailed = WasCancelled || totalTests < 1;

        if (_options.ShowAssembly)
        {
            // Orchestrator (dotnet test): a per-assembly "Discovered N tests in assembly - <link>" header followed by
            // the discovered test names, then a run-level total ("Discovered N tests." / "... in N assemblies.").
            foreach (TestProgressState assembly in assemblies)
            {
                terminal.Append(string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsInAssembly, assembly.DiscoveredTests));
                terminal.Append(" - ");
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly);
                terminal.AppendLine();
                foreach (string displayName in assembly.DiscoveredTestDisplayNames)
                {
                    terminal.Append(SingleIndentation);
                    terminal.AppendLine(displayName);
                }

                terminal.AppendLine();
            }

            terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);
            terminal.AppendLine(assemblies.Count <= 1
                ? string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsSummarySingular, totalTests)
                : string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsSummary, totalTests, assemblies.Count));
            terminal.ResetColor();
            terminal.AppendLine();

            if (WasCancelled)
            {
                terminal.Append(TerminalResources.Aborted);
                terminal.AppendLine();
            }

            return;
        }

        // In-process host: the single "Test discovery summary: found N test(s)" format (unchanged shipping output).
        foreach (TestProgressState assembly in assemblies)
        {
            foreach (string displayName in assembly.DiscoveredTestDisplayNames)
            {
                terminal.Append(SingleIndentation);
                terminal.AppendLine(displayName);
            }
        }

        terminal.AppendLine();

        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);
        terminal.Append(string.Format(CultureInfo.CurrentCulture, TerminalResources.TestDiscoverySummarySingular, totalTests));

        if (assemblies.Count == 1)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblies[0]);
        }

        terminal.ResetColor();
        terminal.AppendLine();

        if (WasCancelled)
        {
            terminal.Append(TerminalResources.Aborted);
            terminal.AppendLine();
        }

        string durationText = $"{SingleIndentation}{TerminalResources.DurationLowercase}: ";
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;
        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }

    /// <summary>
    /// Appends the opt-in "Slowest tests" section, ranking the longest-running tests by their reported execution
    /// duration. For a single assembly a flat list is rendered; for the multi-assembly orchestrator each assembly
    /// gets its own sub-list so the ranking stays scoped per assembly. No-op when the feature is off or when no
    /// timed tests were recorded.
    /// </summary>
    private void AppendSlowestTests(ITerminal terminal, List<TestProgressState> assemblies)
    {
        int count = _options.SlowestTestsCount;
        if (count <= 0)
        {
            return;
        }

        if (_options.ShowAssembly && assemblies.Count > 1)
        {
            bool headerWritten = false;
            foreach (TestProgressState assembly in assemblies)
            {
                IReadOnlyList<(string DisplayName, TimeSpan Duration)> slowest = assembly.GetSlowestTests(count);
                if (slowest.Count == 0)
                {
                    continue;
                }

                if (!headerWritten)
                {
                    terminal.AppendLine();
                    terminal.AppendLine(TerminalResources.SlowestTests);
                    headerWritten = true;
                }

                terminal.Append(SingleIndentation);
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly);
                terminal.AppendLine();
                foreach ((string displayName, TimeSpan duration) in slowest)
                {
                    terminal.Append(DoubleIndentation);
                    AppendSlowestTestLine(terminal, displayName, duration);
                }
            }

            return;
        }

        // Single assembly (in-process host, or the orchestrator with a single assembly): a flat list.
        IReadOnlyList<(string DisplayName, TimeSpan Duration)> tests = assemblies.Count == 1
            ? assemblies[0].GetSlowestTests(count)
            : [];
        if (tests.Count == 0)
        {
            return;
        }

        terminal.AppendLine();
        terminal.AppendLine(TerminalResources.SlowestTests);
        foreach ((string displayName, TimeSpan duration) in tests)
        {
            terminal.Append(SingleIndentation);
            AppendSlowestTestLine(terminal, displayName, duration);
        }
    }

    private static void AppendSlowestTestLine(ITerminal terminal, string displayName, TimeSpan duration)
    {
        AppendLongDuration(terminal, duration, wrapInParentheses: false);
        terminal.Append(' ');
        terminal.Append(MakeControlCharactersVisible(displayName, true));
        terminal.AppendLine();
    }

    internal void AppendCoverageSummary(IReadOnlyList<CoverageScopeSummary> scopes, IReadOnlyList<TestCoverageThresholdMessage> thresholds)
        => _terminalWithProgress.WriteToTerminal(terminal => AppendCoverageSummary(terminal, scopes, thresholds));

    private static void AppendCoverageSummary(ITerminal terminal, IReadOnlyList<CoverageScopeSummary> scopes, IReadOnlyList<TestCoverageThresholdMessage> thresholds)
    {
        if (scopes.Count == 0 && thresholds.Count == 0)
        {
            return;
        }

        terminal.AppendLine();

        if (scopes.Count > 0)
        {
            terminal.AppendLine($"{SingleIndentation}{TerminalResources.CodeCoverageSummary}");

            foreach (CoverageScopeSummary scope in scopes)
            {
                string scopeLabel = GetCoverageScopeLabel(scope.Scope);
                foreach (CoverageMetricResult metric in scope.Metrics)
                {
                    terminal.Append(DoubleIndentation);
                    string coverageValue = metric.HasCoverableData
                        ? $"{metric.Percentage.ToString("F1", CultureInfo.CurrentCulture)}%"
                        : TerminalResources.CoverageNoData;
                    terminal.AppendLine($"{scopeLabel} - {GetCoverageMetricLabel(metric.Metric, metric.CustomMetricName)}: {coverageValue}");
                }
            }
        }

        if (thresholds.Count > 0)
        {
            // Only separate from the coverage block above when it was actually rendered; otherwise the
            // unconditional blank line at the top of the method already provides the single leading blank.
            if (scopes.Count > 0)
            {
                terminal.AppendLine();
            }

            terminal.AppendLine($"{SingleIndentation}{TerminalResources.CoverageThresholdResults}");

            foreach (TestCoverageThresholdMessage threshold in thresholds)
            {
                bool passed = threshold.Passed;
                terminal.SetColor(passed ? TerminalColor.DarkGreen : TerminalColor.DarkRed);
                terminal.Append(DoubleIndentation);
                string comparison;
                if (threshold.HasCoverableData)
                {
                    (string actual, string required) = FormatThresholdPercentages(threshold);
                    comparison = string.Format(
                        CultureInfo.CurrentCulture,
                        passed ? TerminalResources.CoverageThresholdPassed : TerminalResources.CoverageThresholdFailed,
                        actual,
                        required);
                }
                else
                {
                    comparison = passed
                        ? TerminalResources.CoverageThresholdNoDataPassed
                        : TerminalResources.CoverageThresholdNoDataFailed;
                }

                terminal.AppendLine($"{GetCoverageThresholdLabel(threshold)}: {comparison}");
                terminal.ResetColor();
            }
        }
    }

    private static (string Actual, string Required) FormatThresholdPercentages(TestCoverageThresholdMessage threshold)
    {
        string actual = threshold.ActualPercentage.ToString("F1", CultureInfo.CurrentCulture);
        string required = threshold.RequiredPercentage.ToString("F1", CultureInfo.CurrentCulture);

        // Rounding a failed near-boundary result to one decimal can make both operands look equal.
        // Increase precision only for that ambiguous case so the rendered comparison remains truthful.
        if (!threshold.Passed && string.Equals(actual, required, StringComparison.Ordinal))
        {
            actual = threshold.ActualPercentage.ToString("G17", CultureInfo.CurrentCulture);
            required = threshold.RequiredPercentage.ToString("G17", CultureInfo.CurrentCulture);
        }

        return (actual, required);
    }

    // Renders the scope identity used in the coverage summary; the whole-run (Overall) scope is shown
    // with a localized "Total" label, every other scope uses its own name.
    private static string GetCoverageScopeLabel(CoverageScope scope)
        => scope.Level == CoverageScopeLevel.Overall
            ? TerminalResources.CoverageScopeOverall
            : MakeControlCharactersVisible(scope.Name ?? GetCoverageScopeLevelLabel(scope.Level), true);

    // Maps the CoverageMetric enum to a localized label so localized runs don't render the English enum
    // identifier; falls back to the identifier (or custom name) for any member without a resource.
    private static string GetCoverageMetricLabel(CoverageMetric metric, string? customMetricName)
        => metric switch
        {
            CoverageMetric.Line => TerminalResources.CoverageMetricLine,
            CoverageMetric.Statement => TerminalResources.CoverageMetricStatement,
            CoverageMetric.Branch => TerminalResources.CoverageMetricBranch,
            CoverageMetric.Method => TerminalResources.CoverageMetricMethod,
            CoverageMetric.Function => TerminalResources.CoverageMetricFunction,
            CoverageMetric.Block => TerminalResources.CoverageMetricBlock,
            CoverageMetric.Instruction => TerminalResources.CoverageMetricInstruction,
            CoverageMetric.Region => TerminalResources.CoverageMetricRegion,
            CoverageMetric.Class => TerminalResources.CoverageMetricClass,
            CoverageMetric.Condition => TerminalResources.CoverageMetricCondition,
            CoverageMetric.Complexity => TerminalResources.CoverageMetricComplexity,
            CoverageMetric.Custom => MakeControlCharactersVisible(customMetricName ?? nameof(CoverageMetric.Custom), true),
            _ => metric.ToString(),
        };

    private static string GetCoverageThresholdLabel(TestCoverageThresholdMessage threshold)
    {
        string scope = GetCoverageScopeLabel(threshold.Scope);
        string metric = GetCoverageMetricLabel(threshold.Metric, threshold.CustomMetricName);
        if (threshold.Aggregation == CoverageAggregation.None)
        {
            return $"{scope} - {metric}";
        }

        string aggregation = GetCoverageAggregationLabel(threshold.Aggregation);
        string aggregatedOver = GetCoverageScopeLevelLabel(threshold.AggregatedOver.GetValueOrDefault());
        string aggregationLabel = string.Format(
            CultureInfo.CurrentCulture,
            TerminalResources.CoverageAggregationOver,
            aggregation,
            aggregatedOver);

        return $"{scope} - {metric} ({aggregationLabel})";
    }

    private static string GetCoverageAggregationLabel(CoverageAggregation aggregation)
        => aggregation switch
        {
            CoverageAggregation.Total => TerminalResources.CoverageAggregationTotal,
            CoverageAggregation.Minimum => TerminalResources.CoverageAggregationMinimum,
            CoverageAggregation.Average => TerminalResources.CoverageAggregationAverage,
            CoverageAggregation.Maximum => TerminalResources.CoverageAggregationMaximum,
            _ => aggregation.ToString(),
        };

    private static string GetCoverageScopeLevelLabel(CoverageScopeLevel level)
        => level switch
        {
            CoverageScopeLevel.Overall => TerminalResources.CoverageScopeOverall,
            CoverageScopeLevel.Module => TerminalResources.CoverageScopeModule,
            CoverageScopeLevel.Assembly => TerminalResources.CoverageScopeAssembly,
            CoverageScopeLevel.Namespace => TerminalResources.CoverageScopeNamespace,
            CoverageScopeLevel.Type => TerminalResources.CoverageScopeType,
            CoverageScopeLevel.File => TerminalResources.CoverageScopeFile,
            _ => level.ToString(),
        };
}
