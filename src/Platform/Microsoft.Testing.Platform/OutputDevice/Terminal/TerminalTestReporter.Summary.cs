// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        // Retry attempt (second or later): skip straight to the sections the orchestrator does not restate. The
        // verdict and counts below would describe the filtered subset this attempt re-ran rather than the run, so
        // the reconciled retry summary owns them instead.
        if (!_options.ShowRunSummary)
        {
            AppendSlowestTests(terminal, assemblies);
            AppendHandshakeFailureRecap(terminal);
            AppendErroredAssemblyRecap(terminal);
            return;
        }

        // Single-pass aggregation: compute all summary counters in one foreach instead of
        // 7 separate LINQ calls (Sum×5, Any×1, Count×1), saving 6 extra O(N) passes and
        // 7 LINQ enumerator allocations per test run.
        int totalTests = 0;
        int totalFailedTests = 0;
        int totalSkippedTests = 0;
        int totalPassedTests = 0;
        int totalRetriedTests = 0;
        int totalRetriedExecutions = 0;
        int totalFlakyTests = 0;
        bool anyAssemblyFailed = false;
        int failedAssembliesWithoutFailedTests = 0;

        foreach (TestProgressState assembly in assemblies)
        {
            totalTests += assembly.TotalTests;
            totalFailedTests += assembly.FailedTests;
            totalSkippedTests += assembly.SkippedTests;
            totalPassedTests += assembly.PassedTests;
            totalRetriedTests += assembly.RetriedTests;
            totalRetriedExecutions += assembly.RetriedExecutions;
            totalFlakyTests += assembly.FlakyTests;
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

        // Orchestrator-only: count assemblies that ended unsuccessfully without a failed test (crash / non-zero exit)
        // plus handshake failures. These are surfaced as an "error: N" line so they aren't hidden behind a zero
        // failed-test count. In-process leaves ShowAssembly off and never has handshake failures, so error is 0.
        int error = (_options.ShowAssembly ? failedAssembliesWithoutFailedTests : 0) + HandshakeFailureCount;
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && failed == 0;
        bool colorizeSkipped = skipped > 0;

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
        terminal.AppendLine(totalText);

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

        AppendRetrySummaryLines(terminal, totalFlakyTests, totalRetriedTests, totalRetriedExecutions);

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();

        // Optional "Flaky tests" section (on by default, suppressed by --show-flaky-tests off). No-op when nothing
        // was retried, so the summary stays byte-identical for a run without retries.
        AppendFlakyTests(terminal, assemblies);

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
    /// Appends the retry accounting lines that sit between the skipped count and the duration:
    /// <c>flaky: N</c> (tests that failed at least once but eventually passed) and
    /// <c>retried: N tests, M extra runs</c>. Both are omitted entirely when nothing was retried, so a run without
    /// retries keeps its historical summary byte-for-byte.
    /// </summary>
    private void AppendRetrySummaryLines(ITerminal terminal, int flakyTests, int retriedTests, int retriedExecutions)
    {
        // "flaky" is the headline value of retrying, so it is reported whenever it is non-zero unless the user
        // explicitly turned the feature off.
        if (flakyTests > 0 && _options.ShowFlakyTests)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            terminal.AppendLine($"{SingleIndentation}{string.Format(CultureInfo.CurrentCulture, TerminalResources.FlakyLowercase, flakyTests)}");
            terminal.ResetColor();
        }

        if (retriedTests > 0)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append($"{SingleIndentation}{TerminalResources.Retried}: ");
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, TerminalResources.RetriedTestsAndRuns, retriedTests, retriedExecutions));
            terminal.ResetColor();
        }
    }
}
