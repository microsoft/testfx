// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
#if NET9_0_OR_GREATER
    private readonly Lock _erroredAssembliesLock = new();
#else
    private readonly object _erroredAssembliesLock = new();
#endif

    /// <summary>
    /// Assemblies that handshaked but then exited with a non-zero code without reporting any failed test (crash,
    /// <c>Environment.FailFast</c>, a hang-dump kill, an option rejected after the handshake, ...). Their process
    /// output is printed inline when the assembly completes, but in a large multi-assembly run that inline output is
    /// scrolled far above the summary, so we also record it here and re-print it in an end-of-run recap.
    /// </summary>
    private readonly List<ErroredAssemblyRecord> _erroredAssemblies = [];

    /// <summary>
    /// Records an assembly that ended unsuccessfully with no failed test so its process output (exit code + stdout +
    /// stderr) can be re-printed at the end of the run. Orchestrator-only: only the multi-process <c>dotnet test</c>
    /// orchestrator reaches the exit-code overload of <see cref="AssemblyRunCompleted(string, int, string?, string?)"/>.
    /// </summary>
    private void RecordErroredAssembly(TestProgressState assemblyRun, int exitCode, string? outputData, string? errorData)
    {
        lock (_erroredAssembliesLock)
        {
            _erroredAssemblies.Add(new ErroredAssemblyRecord(assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture, exitCode, outputData, errorData));
        }
    }

    /// <summary>
    /// Re-print assemblies that errored during the run (non-zero exit with no failed test) so that — as with the
    /// handshake-failure recap — the actionable process output is shown at the end of the run rather than buried in
    /// the middle of a large multi-assembly run.
    /// </summary>
    private void AppendErroredAssemblyRecap(ITerminal terminal)
    {
        ErroredAssemblyRecord[] errored;
        lock (_erroredAssembliesLock)
        {
            if (_erroredAssemblies.Count == 0)
            {
                return;
            }

            errored = _erroredAssemblies.ToArray();
        }

        terminal.AppendLine();
        terminal.SetColor(TerminalColor.DarkRed);
        terminal.AppendLine(TerminalResources.ErroredAssembliesHeader);
        terminal.ResetColor();

        foreach (ErroredAssemblyRecord failure in errored)
        {
            terminal.Append(SingleIndentation);
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, failure.AssemblyPath, failure.TargetFramework, failure.Architecture);
            terminal.AppendLine();
            AppendExecutableSummary(terminal, failure.ExitCode, failure.OutputData, failure.ErrorData);
        }
    }

    private readonly record struct ErroredAssemblyRecord(
        string AssemblyPath,
        string? TargetFramework,
        string? Architecture,
        int ExitCode,
        string? OutputData,
        string? ErrorData);
}
