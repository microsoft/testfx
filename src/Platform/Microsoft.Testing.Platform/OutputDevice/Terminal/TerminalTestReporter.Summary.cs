// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    private void AppendTestRunSummary(ITerminal terminal)
    {
        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
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

        int totalTests = assemblies.Sum(static a => a.TotalTests);
        int totalFailedTests = assemblies.Sum(static a => a.FailedTests);
        int totalSkippedTests = assemblies.Sum(static a => a.SkippedTests);
        int totalPassedTests = assemblies.Sum(static a => a.PassedTests);

        // DESIGN: `allTestsWereSkipped` is intentionally treated as a failed run. Skipped tests don't count as
        // "ran", so an all-skipped (or zero-test) run is reported in red as "Zero tests ran". This is the strict
        // default chosen in #3216 / #3243 ("Skipped tests count as not run") to flag the common "invalid filter
        // ran nothing" mistake. `--ignore-exit-code 8` only affects the process exit code; it does not change
        // this terminal summary/verdict/coloring, which still reports the run as failed by design.
        //
        // Two sibling sites mirror this decision and must stay in lockstep:
        //   - TestApplicationResult.ConsumeAsync (excludes skipped from `_totalRanTests` -> exit code 8)
        //   - Microsoft.Testing.Platform.MSBuild InvokeTestingPlatformTask (run-summary verdict)
        bool runFailed = TestRunSummaryHelper.IsRunFailed(totalTests, totalFailedTests, totalSkippedTests, WasCancelled, _options.MinimumExpectedTests) || HasHandshakeFailure;
        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);

        terminal.Append(TerminalResources.TestRunSummary);
        terminal.Append(' ');
        terminal.Append(TestRunSummaryHelper.GetVerdictText(totalTests, totalFailedTests, totalSkippedTests, WasCancelled, _options.MinimumExpectedTests));

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
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && failed == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && failed == 0;

        string totalText = $"{SingleIndentation}{TerminalResources.TotalLowercase}: {total}";
        string failedText = $"{SingleIndentation}{TerminalResources.FailedLowercase}: {failed}";
        string passedText = $"{SingleIndentation}{TerminalResources.SucceededLowercase}: {passed}";
        string skippedText = $"{SingleIndentation}{TerminalResources.SkippedLowercase}: {skipped}";
        string durationText = $"{SingleIndentation}{TerminalResources.DurationLowercase}: ";

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

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();

        // Re-print any handshake failures (orchestrator-only) at the very end so they aren't lost above the summary.
        // No-op for the in-process host, which never reports handshake failures.
        AppendHandshakeFailureRecap(terminal);
    }

    /// <summary>
    /// Orchestrator overload (<c>dotnet test</c>): the multi-process orchestrator also knows each discovered test's
    /// uid, file path and line number. The shared discovery summary currently lists display names only, so those are
    /// accepted for signature parity and the overload delegates to the core method.
    /// </summary>
    internal void TestDiscovered(string executionId, string? displayName, string? uid, string? filePath, int? lineNumber)
        => TestDiscovered(executionId, displayName ?? string.Empty);

    internal void TestDiscovered(string executionId, string displayName)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        // In discovery mode TotalTests is computed from DiscoveredTests; in execution mode it is computed from the
        // passed/skipped/failed tally as tests complete. So we only need to bump the discovered count here.
        asm.DiscoveredTests++;

        asm.DiscoveredTestDisplayNames.Add(MakeControlCharactersVisible(displayName, true));

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal)
    {
        List<TestProgressState> assemblies = [.. _assemblies.Values.OrderBy(static a => a.Id)];
        terminal.AppendLine();

        int totalTests = assemblies.Sum(static a => a.TotalTests);
        bool runFailed = WasCancelled || totalTests < 1;

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
}
