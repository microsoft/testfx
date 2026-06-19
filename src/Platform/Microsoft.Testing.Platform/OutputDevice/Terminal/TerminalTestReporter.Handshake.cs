// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
#if NET9_0_OR_GREATER
    private readonly Lock _handshakeFailuresLock = new();
#else
    private readonly object _handshakeFailuresLock = new();
#endif
    private readonly List<HandshakeFailureRecord> _handshakeFailures = [];
    private int _handshakeFailuresCount;

    /// <summary>
    /// Gets a value indicating whether any assembly failed to hand-shake (e.g. a child test host process
    /// exited before it could start a session). Used by the <c>dotnet test</c> orchestrator to surface a
    /// non-success exit even when no test result was ever reported.
    /// </summary>
    public bool HasHandshakeFailure => _handshakeFailuresCount > 0;

    /// <summary>
    /// Report that an assembly failed to produce a usable handshake. This is an orchestrator-only path (the
    /// in-process host always hand-shakes); it records the failure so <see cref="HasHandshakeFailure"/> flips and
    /// the failure is re-printed in the end-of-run recap, and prints the immediate failure context.
    /// </summary>
    internal void HandshakeFailure(string assemblyPath, string? targetFramework, int exitCode, string outputData, string errorData, bool reportEvenWhenHelp = false)
    {
        if (_isHelp && !reportEvenWhenHelp)
        {
            // Backward-compat workaround: older Microsoft.Testing.Platform versions don't perform a handshake on the
            // --help path (the host just prints help and exits). In that case the orchestrator routes here with the
            // "process exited without a usable handshake" payload, which is expected and should not be reported as a
            // failure. Explicit protocol-level rejections pass reportEvenWhenHelp=true and are still surfaced.
            return;
        }

        Interlocked.Increment(ref _handshakeFailuresCount);
        lock (_handshakeFailuresLock)
        {
            _handshakeFailures.Add(new HandshakeFailureRecord(assemblyPath, targetFramework, exitCode, outputData, errorData));
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyPath, targetFramework, architecture: null);
            terminal.Append(' ');
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.Append(TerminalResources.ZeroTestsRan);
            terminal.ResetColor();
            terminal.AppendLine();
            AppendExecutableSummary(terminal, exitCode, outputData, errorData);
        });
    }

    /// <summary>
    /// Re-print handshake failures captured during the run so that — even when there is a lot of diagnostic output
    /// before the summary — the user sees the actionable failure context (assembly, exit code, stdout, stderr) at
    /// the end of the run rather than having to scroll back.
    /// </summary>
    private void AppendHandshakeFailureRecap(ITerminal terminal)
    {
        HandshakeFailureRecord[] failures;
        lock (_handshakeFailuresLock)
        {
            if (_handshakeFailures.Count == 0)
            {
                return;
            }

            failures = _handshakeFailures.ToArray();
        }

        terminal.AppendLine();
        terminal.SetColor(TerminalColor.DarkRed);
        terminal.AppendLine(TerminalResources.HandshakeFailuresHeader);
        terminal.ResetColor();

        foreach (HandshakeFailureRecord failure in failures)
        {
            terminal.Append(SingleIndentation);
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, failure.AssemblyPath, failure.TargetFramework, architecture: null);
            terminal.AppendLine();
            AppendExecutableSummary(terminal, failure.ExitCode, failure.OutputData, failure.ErrorData);
        }
    }

    private static void AppendExecutableSummary(ITerminal terminal, int? exitCode, string? outputData, string? errorData)
    {
        terminal.Append(TerminalResources.ExitCode);
        terminal.Append(": ");
        terminal.AppendLine(exitCode?.ToString(CultureInfo.CurrentCulture) ?? "<null>");
        AppendOutputWhenPresent(TerminalResources.StandardOutput, outputData);
        AppendOutputWhenPresent(TerminalResources.StandardError, errorData);

        void AppendOutputWhenPresent(string description, string? output)
        {
            if (!RoslynString.IsNullOrWhiteSpace(output))
            {
                AppendIndentedLine(terminal, $"{description}: {output}", SingleIndentation);
            }
        }
    }

    private readonly record struct HandshakeFailureRecord(
        string AssemblyPath,
        string? TargetFramework,
        int ExitCode,
        string OutputData,
        string ErrorData);
}
