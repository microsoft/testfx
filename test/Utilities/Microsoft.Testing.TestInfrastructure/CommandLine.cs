// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class CommandLine : IDisposable
{
    private static int s_totalProcessesAttempt;

    private readonly List<string> _errorOutputLines = new();
    private readonly List<string> _standardOutputLines = new();
    private IProcessHandle? _process;

    public static int TotalProcessesAttempt => s_totalProcessesAttempt;

    public ReadOnlyCollection<string> StandardOutputLines => _standardOutputLines.AsReadOnly();

    public ReadOnlyCollection<string> ErrorOutputLines => _errorOutputLines.AsReadOnly();

    public string StandardOutput => string.Join(Environment.NewLine, _standardOutputLines);

    public string ErrorOutput => string.Join(Environment.NewLine, _errorOutputLines);

    private static int s_maxOutstandingCommand = Environment.ProcessorCount;
    private static SemaphoreSlim s_maxOutstandingCommands_semaphore = new(s_maxOutstandingCommand, s_maxOutstandingCommand);

    public static int MaxOutstandingCommands
    {
        get => s_maxOutstandingCommand;

        set
        {
            s_maxOutstandingCommand = value;
            s_maxOutstandingCommands_semaphore.Dispose();
            s_maxOutstandingCommands_semaphore = new SemaphoreSlim(s_maxOutstandingCommand, s_maxOutstandingCommand);
        }
    }

    public async Task RunAsync(
        string commandLine,
        IDictionary<string, string>? environmentVariables = null)
    {
        int exitCode = await RunAsyncAndReturnExitCodeAsync(commandLine, environmentVariables);
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

    public async Task<int> RunAsyncAndReturnExitCodeAsync(
        string commandLine,
        IDictionary<string, string>? environmentVariables = null,
        string? workingDirectory = null,
        bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false,
        int timeoutInSeconds = 60)
    {
        await s_maxOutstandingCommands_semaphore.WaitAsync();
        try
        {
            Interlocked.Increment(ref s_totalProcessesAttempt);
            string[] tokens = commandLine.Split(' ');
            string command = tokens[0];
            string arguments = string.Join(" ", tokens.Skip(1));
            _errorOutputLines.Clear();
            _standardOutputLines.Clear();
            ProcessConfiguration startInfo = new(command)
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
            CancellationTokenSource stopTheTimer = new();
            var timedOut = Task.Delay(TimeSpan.FromSeconds(seconds), stopTheTimer.Token);
            if (await Task.WhenAny(exited, timedOut) == exited)
            {
#if NET8_0_OR_GREATER
                await stopTheTimer.CancelAsync();
#else
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                stopTheTimer.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
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
        finally
        {
            s_maxOutstandingCommands_semaphore.Release();
        }
    }

    public void Dispose() => _process?.Kill();
}
