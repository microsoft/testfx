// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
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
}
