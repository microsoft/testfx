// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    /// <summary>
    /// Appends the "Flaky tests" section listing, by name, the tests that failed at least once but whose final
    /// attempt passed. Retried tests that never recovered are deliberately not listed: they are already reported as
    /// failures with their full error output, so a second listing would only duplicate. For a single assembly a flat
    /// list is rendered; the multi-assembly orchestrator groups per assembly. No-op when the feature is off or when
    /// no test was flaky.
    /// </summary>
    private void AppendFlakyTests(ITerminal terminal, List<TestProgressState> assemblies)
    {
        if (!_options.ShowFlakyTests)
        {
            return;
        }

        if (_options.ShowAssembly && assemblies.Count > 1)
        {
            bool headerWritten = false;
            foreach (TestProgressState assembly in assemblies)
            {
                IReadOnlyList<(string DisplayName, int Attempts)> flaky = assembly.GetFlakyTests();
                if (flaky.Count == 0)
                {
                    continue;
                }

                if (!headerWritten)
                {
                    terminal.AppendLine();
                    terminal.AppendLine(TerminalResources.FlakyTests);
                    headerWritten = true;
                }

                terminal.Append(SingleIndentation);
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly);
                terminal.AppendLine();
                foreach ((string displayName, int attempts) in flaky)
                {
                    terminal.Append(DoubleIndentation);
                    AppendFlakyTestLine(terminal, displayName, attempts);
                }
            }

            return;
        }

        IReadOnlyList<(string DisplayName, int Attempts)> tests = assemblies.Count == 1
            ? assemblies[0].GetFlakyTests()
            : [];
        if (tests.Count == 0)
        {
            return;
        }

        terminal.AppendLine();
        terminal.AppendLine(TerminalResources.FlakyTests);
        foreach ((string displayName, int attempts) in tests)
        {
            terminal.Append(SingleIndentation);
            AppendFlakyTestLine(terminal, displayName, attempts);
        }
    }

    private static void AppendFlakyTestLine(ITerminal terminal, string displayName, int attempts)
    {
        terminal.Append(MakeControlCharactersVisible(displayName, true));
        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(' ');
        terminal.Append(TerminalResources.FlakyTransition);
        terminal.Append(" (");
        terminal.Append(string.Format(CultureInfo.CurrentCulture, TerminalResources.FlakyAttempts, attempts));
        terminal.Append(')');
        terminal.ResetColor();
        terminal.AppendLine();
    }
}
