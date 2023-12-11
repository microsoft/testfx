﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

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
        bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false,
        int timeoutInSeconds = 300)
    {
        Interlocked.Increment(ref s_totalProcessesAttempt);
        string[] tokens = commandLine.Split(' ');
        string command = tokens[0];
        string arguments = string.Join(" ", tokens.Skip(1));
        _errorOutputLines.Clear();
        _standardOutputLines.Clear();
        var startInfo = new ProcessStartInfo(command)
        {
            Arguments = arguments,
            EnvironmentVariables = environmentVariables,
            OnErrorOutput = (_, o) => _errorOutputLines.Add(o),
            OnStandardOutput = (_, o) => _standardOutputLines.Add(o),
        };
        _process = new ProcessHelper().Start(startInfo, cleanDefaultEnvironmentVariableIfCustomAreProvided, timeoutInSeconds);

        Task<int> exited = _process.WaitForExitAsync();
        int seconds = timeoutInSeconds;
        var cancellationTokenSource = new CancellationTokenSource();
        var timedOut = Task.Delay(TimeSpan.FromSeconds(seconds), cancellationTokenSource.Token);
        if (await Task.WhenAny(exited, timedOut) == exited)
        {
            cancellationTokenSource.Cancel();
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

    public void Dispose()
    {
        _process?.Kill();
    }
}
