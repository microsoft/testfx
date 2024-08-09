// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Runtime.InteropServices;

using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class TestHost
{
    private readonly string _testHostModuleName;

    private static int s_maxOutstandingExecutions = Environment.ProcessorCount;
    private static SemaphoreSlim s_maxOutstandingExecutions_semaphore = new(s_maxOutstandingExecutions, s_maxOutstandingExecutions);

    private TestHost(string testHostFullName, string testHostModuleName)
    {
        FullName = testHostFullName;
        DirectoryName = Path.GetDirectoryName(testHostFullName)!;
        _testHostModuleName = testHostModuleName;
    }

    public static int MaxOutstandingExecutions
    {
        get => s_maxOutstandingExecutions;

        set
        {
            s_maxOutstandingExecutions = value;
            s_maxOutstandingExecutions_semaphore.Dispose();
            s_maxOutstandingExecutions_semaphore = new SemaphoreSlim(s_maxOutstandingExecutions, s_maxOutstandingExecutions);
        }
    }

    public string FullName { get; }

    public string DirectoryName { get; }

    public async Task<TestHostResult> ExecuteAsync(
        string? command = null,
        Dictionary<string, string>? environmentVariables = null,
        bool disableTelemetry = true,
        int timeoutSeconds = 60)
    {
        await s_maxOutstandingExecutions_semaphore.WaitAsync();
        try
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
                string? key = entry.Key.ToString();
                if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                environmentVariables.Add(key!, entry.Value!.ToString()!);
            }

            // Define DOTNET_ROOT to point to the dotnet we install for this repository, to avoid
            // computer configuration having impact on our tests.
            environmentVariables.Add("DOTNET_ROOT", $"{RootFinder.Find()}/.dotnet");

            string finalArguments = command ?? string.Empty;

            IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(TimeSpan.FromSeconds(3), retryCount: 5, factor: 1.5);
            return await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(delay)
                .ExecuteAsync(async () =>
                {
                    CommandLine commandLine = new();
                    int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
                        $"{FullName} {finalArguments}",
                        environmentVariables: environmentVariables,
                        workingDirectory: null,
                        cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
                        timeoutInSeconds: timeoutSeconds);
                    string fullCommand = command is not null ? $"{FullName} {command}" : FullName;
                    return new TestHostResult(fullCommand, exitCode, commandLine.StandardOutput, commandLine.StandardOutputLines, commandLine.ErrorOutput, commandLine.ErrorOutputLines);
                });
        }
        finally
        {
            s_maxOutstandingExecutions_semaphore.Release();
        }
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
        string expectedRootPath = Path.Combine(rootFolder, "bin", buildConfiguration.ToString(), tfm);
        string[] executables = Directory.GetFiles(expectedRootPath, moduleName, SearchOption.AllDirectories);
        string? expectedPath = executables.SingleOrDefault(p => p.Contains(rid) && p.Contains(verb == Verb.publish ? "publish" : string.Empty));

        return expectedPath is null
            ? throw new InvalidOperationException($"Host '{moduleName}' not found in '{expectedRootPath}'")
            : new TestHost(expectedPath, testHostModuleNameWithoutExtension);
    }
}
