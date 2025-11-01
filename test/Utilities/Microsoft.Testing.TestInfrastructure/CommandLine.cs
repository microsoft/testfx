// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class CommandLine : IDisposable
{
    private readonly List<string> _errorOutputLines = [];
    private readonly List<string> _standardOutputLines = [];
    private IProcessHandle? _process;

    public ReadOnlyCollection<string> StandardOutputLines => _standardOutputLines.AsReadOnly();

    public ReadOnlyCollection<string> ErrorOutputLines => _errorOutputLines.AsReadOnly();

    public string StandardOutput => string.Join(Environment.NewLine, _standardOutputLines);

    public string ErrorOutput => string.Join(Environment.NewLine, _errorOutputLines);

    public async Task RunAsync(
        string commandLine,
        IDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        int exitCode = await RunAsyncAndReturnExitCodeAsync(commandLine, environmentVariables, cancellationToken: cancellationToken);
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

    private static (string Command, string Arguments) GetCommandAndArguments(string commandLine)
    {
        // Hacky way to split command and arguments that works with the limited cases we use in our tests.
        if (!commandLine.StartsWith('"'))
        {
            string[] tokens = commandLine.Split(' ');
            return (tokens[0], string.Join(' ', tokens.Skip(1)));
        }

        int endQuote = commandLine.IndexOf('"', 1);
        return (commandLine.Substring(1, endQuote - 1), commandLine.Substring(endQuote + 2));
    }

    public async Task<int> RunAsyncAndReturnExitCodeAsync(
        string commandLine,
        IDictionary<string, string?>? environmentVariables = null,
        string? workingDirectory = null,
        bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false,
        CancellationToken cancellationToken = default)
    {
        (string command, string arguments) = GetCommandAndArguments(commandLine);
        _errorOutputLines.Clear();
        _standardOutputLines.Clear();
        ProcessConfiguration startInfo = new(command)
        {
            Arguments = arguments,
            EnvironmentVariables = environmentVariables,
            OnErrorOutput = (_, o) => _errorOutputLines.Add(ClearBOM(o)),
            OnStandardOutput = (_, o) => _standardOutputLines.Add(ClearBOM(o)),
            WorkingDirectory = workingDirectory,
        };
        (_process, Task outputAndErrorTask) = ProcessFactory.Start(startInfo, cleanDefaultEnvironmentVariableIfCustomAreProvided);

        using CancellationTokenRegistration registration = cancellationToken.Register(() => _process.Kill());
        await outputAndErrorTask;
        return await _process.WaitForExitAsync(cancellationToken);
    }

    /// <summary>
    /// Depending on command line settings, e.g. when using Windows Terminal
    /// .NET Framework app might have BOM at the beginning of the captured output, which breaks output comparisons
    /// while no visible difference is seen between the outputs.
    /// </summary>
    private static string ClearBOM(string outputLine)
    {
        int firstChar = outputLine[0];
        int byteOrderMark = 65279;
        return firstChar == byteOrderMark ? outputLine[1..] : outputLine;
    }

    public void Dispose() => _process?.Kill();
}
