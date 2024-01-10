// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class CommandLine : IDisposable
{
    private static int s_totalProcessesAttempt;

    public static int TotalProcessesAttempt => s_totalProcessesAttempt;

    private readonly List<string> _errorOutputLines = new();
    private readonly List<string> _standardOutputLines = new();
    private IProcessHandle? _process;

    public ReadOnlyCollection<string> StandardOutputLines => _standardOutputLines.AsReadOnly();

    public ReadOnlyCollection<string> ErrorOutputLines => _errorOutputLines.AsReadOnly();

    public string StandardOutput => string.Join(Environment.NewLine, _standardOutputLines);

    public string ErrorOutput => string.Join(Environment.NewLine, _errorOutputLines);

    public async Task RunAsync(
        string commandLine,
        IDictionary<string, string>? environmentVariables = null)
    {
        int exitCode = await RunAsyncAndReturnExitCode(commandLine, environmentVariables);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"""
                    Non-zero exit code {exitCode} from command line: '{commandLine}'
                    STD: {StandardOutput}
                    ERR: {ErrorOutput}
                    """);
        }
    }

    public async Task<int> RunAsyncAndReturnExitCode(
        string commandLine,
        IDictionary<string, string>? environmentVariables = null,
        string? workingDirectory = null,
        bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false,
        int timeoutInSeconds = 60)
    {
        Interlocked.Increment(ref s_totalProcessesAttempt);
        string[] tokens = commandLine.Split(' ');
        string command = tokens[0];
        string arguments = string.Join(" ", tokens.Skip(1));
        _errorOutputLines.Clear();
        _standardOutputLines.Clear();
        var startInfo = new ProcessConfiguration(command)
        {
            Arguments = arguments,
            EnvironmentVariables = environmentVariables,
            OnErrorOutput = (_, o) => _errorOutputLines.Add(o),
            OnStandardOutput = (_, o) => _standardOutputLines.Add(o),
            WorkingDirectory = workingDirectory,
        };
        _process = ProcessFactory.Start(startInfo, cleanDefaultEnvironmentVariableIfCustomAreProvided);

        Task<int> exited = _process.WaitForExitAsync();
        int seconds = timeoutInSeconds;
        var stopTheTimer = new CancellationTokenSource();
        var timedOut = Task.Delay(TimeSpan.FromSeconds(seconds), stopTheTimer.Token);
        if (await Task.WhenAny(exited, timedOut) == exited)
        {
#if NET8_0_OR_GREATER
            await stopTheTimer.CancelAsync();
#else
            stopTheTimer.Cancel();
#endif
            return await exited;
        }
        else
        {
            _process.Kill();
            throw new TimeoutException(
                $"""
                Timeout after {seconds}s on command line: '{commandLine}'
                STD: {StandardOutput}
                ERR: {ErrorOutput}
                """);
        }
    }

    public void Dispose() => _process?.Kill();
}
