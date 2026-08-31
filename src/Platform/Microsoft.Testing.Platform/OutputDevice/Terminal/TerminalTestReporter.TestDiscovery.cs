// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
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
}
