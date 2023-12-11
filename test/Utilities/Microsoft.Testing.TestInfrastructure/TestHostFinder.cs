// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Linq;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class TestHostFinder
{
    private readonly string _testHostModuleName;

    public TestHostFinder(string testHost, string testHostModuleName)
    {
        TestHost = testHost;
        _testHostModuleName = testHostModuleName;
    }

    public string TestHost { get; }

    public async Task<TestHostResult> RunAsync(string command, Dictionary<string, string>? environmentVariables = null)
    {
        if (!command.StartsWith(_testHostModuleName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"'{_testHostModuleName}' command expected");
        }

        environmentVariables ??= new Dictionary<string, string>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            if (KnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables.Add(entry.Key!.ToString()!, entry.Value!.ToString()!);
        }

        // Define DOTNET_ROOT to point to the dotnet we install for this repository, to avoid
        // computer configuration having impact on our tests.
        environmentVariables.Add("DOTNET_ROOT", $"{RootFinder.Find()}/.dotnet");
        CommandLine commandLine = new();

        int argumentsStartIndex = command.IndexOf(" ", StringComparison.OrdinalIgnoreCase);
        string finalArguments = argumentsStartIndex < 0
            ? string.Empty
            : command[(argumentsStartIndex + 1)..];

        int exitCode = await commandLine.RunAsyncAndReturnExitCode($"{TestHost} {finalArguments}", environmentVariables, cleanDefaultEnvironmentVariableIfCustomAreProvided: true, 60 * 30);
        return new TestHostResult(command, exitCode, commandLine.StandardOutput, commandLine.StandardOutputLines, commandLine.ErrorOutput, commandLine.ErrorOutputLines);
    }
}
