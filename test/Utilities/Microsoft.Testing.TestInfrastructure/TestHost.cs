// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class TestHost
{
    private readonly string _testHostModuleName;

    private TestHost(string testHostFullName, string testHostModuleName)
    {
        FullName = testHostFullName;
        DirectoryName = Path.GetDirectoryName(testHostFullName)!;
        _testHostModuleName = testHostModuleName;
    }

    public string FullName { get; }

    public string DirectoryName { get; }

    public async Task<TestHostResult> ExecuteAsync(string? command = null, Dictionary<string, string>? environmentVariables = null, bool disableTelemetry = true)
    {
        if (command?.StartsWith(_testHostModuleName, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            throw new InvalidOperationException($"Command should not start with module name '{_testHostModuleName}'.");
        }

        environmentVariables ??= new Dictionary<string, string>();

        if (disableTelemetry)
        {
            environmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        }

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables.Add(entry.Key!.ToString()!, entry.Value!.ToString()!);
        }

        // Define DOTNET_ROOT to point to the dotnet we install for this repository, to avoid
        // computer configuration having impact on our tests.
        environmentVariables.Add("DOTNET_ROOT", $"{RootFinder.Find()}/.dotnet");

        string finalArguments = command ?? string.Empty;

        CommandLine commandLine = new();
        int exitCode = await commandLine.RunAsyncAndReturnExitCode($"{FullName} {finalArguments}", environmentVariables: environmentVariables, workingDirectory: null, cleanDefaultEnvironmentVariableIfCustomAreProvided: true, 60 * 30);
        string fullCommand = command is not null ? $"{FullName} {command}" : FullName;
        return new TestHostResult(fullCommand, exitCode, commandLine.StandardOutput, commandLine.StandardOutputLines, commandLine.ErrorOutput, commandLine.ErrorOutputLines);
    }

    public static TestHost LocateFrom(string rootFolder, string testHostModuleNameWithoutExtension)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";

        // Try to find module in current dir, useful when we use to close
        return File.Exists(Path.Combine(rootFolder, moduleName))
            ? new TestHost(Path.Combine(rootFolder, moduleName), testHostModuleNameWithoutExtension)
            : throw new FileNotFoundException("Test host file not found", Path.Combine(rootFolder, moduleName));
    }

    public static TestHost LocateFrom(
        string rootFolder,
        string testHostModuleNameWithoutExtension,
        string tfm,
        string rid = "",
        Verb verb = Verb.build,
        BuildConfiguration buildConfiguration = BuildConfiguration.Release)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";
        string? expectedRootPath = Path.Combine(rootFolder, "bin", buildConfiguration.ToString(), tfm);
        string[] executables = Directory.GetFiles(expectedRootPath, moduleName, SearchOption.AllDirectories);
        string? expectedPath = executables.SingleOrDefault(p => p.Contains(rid) && p.Contains(verb == Verb.publish ? "publish" : string.Empty));

        return expectedPath is null
            ? throw new InvalidOperationException($"Host '{moduleName}' not found in '{expectedRootPath}'")
            : new TestHost(expectedPath, testHostModuleNameWithoutExtension);
    }
}
