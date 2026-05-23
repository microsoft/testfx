// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

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
            terminal.AppendLine(artifactGroup.Key ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!RoslynString.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(PlatformResources.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        terminal.AppendLine();

        int totalTests = _testProgressState?.TotalTests ?? 0;
        int totalFailedTests = _testProgressState?.FailedTests ?? 0;
        int totalSkippedTests = _testProgressState?.SkippedTests ?? 0;

        // DESIGN: `allTestsWereSkipped` is intentionally treated as a failed run. Skipped tests don't count as
        // "ran", so an all-skipped (or zero-test) run is reported in red as "Zero tests ran". This is the strict
        // default chosen in #3216 / #3243 ("Skipped tests count as not run") to flag the common "invalid filter
        // ran nothing" mistake. Users who legitimately expect all-skipped runs should opt out via
        // `--ignore-exit-code 8`.
        //
        // Two sibling sites mirror this decision and must stay in lockstep:
        //   - TestApplicationResult.ConsumeAsync (excludes skipped from `_totalRanTests` -> exit code 8)
        //   - Microsoft.Testing.Platform.MSBuild InvokeTestingPlatformTask (run-summary verdict)
        bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool runFailed = anyTestFailed || notEnoughTests || allTestsWereSkipped || WasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);

        terminal.Append(PlatformResources.TestRunSummary);
        terminal.Append(' ');

        if (WasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
        }
        else if (allTestsWereSkipped)
        {
            terminal.Append(PlatformResources.ZeroTestsRan);
        }
        else if (anyTestFailed)
        {
            terminal.Append($"{PlatformResources.Failed}!");
        }
        else
        {
            terminal.Append($"{PlatformResources.Passed}!");
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(" - ");
        terminal.ResetColor();
        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal);

        terminal.AppendLine();

        int total = _testProgressState?.TotalTests ?? 0;
        int failed = _testProgressState?.FailedTests ?? 0;
        int passed = _testProgressState?.PassedTests ?? 0;
        int skipped = _testProgressState?.SkippedTests ?? 0;
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && failed == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && failed == 0;

        string totalText = $"{SingleIndentation}{PlatformResources.TotalLowercase}: {total}";
        string failedText = $"{SingleIndentation}{PlatformResources.FailedLowercase}: {failed}";
        string passedText = $"{SingleIndentation}{PlatformResources.SucceededLowercase}: {passed}";
        string skippedText = $"{SingleIndentation}{PlatformResources.SkippedLowercase}: {skipped}";
        string durationText = $"{SingleIndentation}{PlatformResources.DurationLowercase}: ";

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
    }

    internal void TestDiscovered(string displayName)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;
        asm.DiscoveredTests++;

        if (_isDiscovery)
        {
            // In discovery mode we count discovered tests,
            // but in execution mode the completion of test will increase the total tests count.
            asm.TotalTests++;
        }

        asm.DiscoveredTestDisplayNames.Add(MakeControlCharactersVisible(displayName, true));

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal)
    {
        TestProgressState? assembly = _testProgressState;
        terminal.AppendLine();

        int totalTests = assembly?.TotalTests ?? 0;
        bool runFailed = WasCancelled || totalTests < 1;

        if (assembly is not null)
        {
            foreach (string displayName in assembly.DiscoveredTestDisplayNames)
            {
                terminal.Append(SingleIndentation);
                terminal.AppendLine(displayName);
            }
        }

        terminal.AppendLine();

        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);
        terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.TestDiscoverySummarySingular, totalTests));

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(" - ");
        terminal.ResetColor();
        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal);

        terminal.ResetColor();
        terminal.AppendLine();

        if (WasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
            terminal.AppendLine();
        }

        string durationText = $"{SingleIndentation}{PlatformResources.DurationLowercase}: ";
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;
        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }
}
