// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Testing.TestInfrastructure;

public static class DotnetCli
{
    private static int s_maxOutstandingCommand = Environment.ProcessorCount;
    private static SemaphoreSlim s_maxOutstandingCommands_semaphore = new(s_maxOutstandingCommand, s_maxOutstandingCommand);

    public static int MaxOutstandingCommands
    {
        get
        {
            return s_maxOutstandingCommand;
        }

        set
        {
            s_maxOutstandingCommand = value;
            s_maxOutstandingCommands_semaphore.Dispose();
            s_maxOutstandingCommands_semaphore = new SemaphoreSlim(s_maxOutstandingCommand, s_maxOutstandingCommand);
        }
    }

    public static async Task<DotnetMuxerResult> RunAsync(
        string args,
        string nugetGlobalPackagesFolder,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        bool failIfReturnValueIsNotZero = true,
        bool disableTelemetry = true)
    {
        await s_maxOutstandingCommands_semaphore.WaitAsync();
        try
        {
            environmentVariables ??= new Dictionary<string, string>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                // Skip all unwanted environment variables.
                if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                environmentVariables.Add(entry.Key!.ToString()!, entry.Value!.ToString()!);
            }

            if (disableTelemetry)
            {
                environmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            }

            environmentVariables["NUGET_PACKAGES"] = nugetGlobalPackagesFolder;

            // Retry in case of:
            // Plugin 'CredentialProvider.Microsoft' failed within 21.143 seconds with exit code
            return await RetryHelper.Retry(
                async () =>
                {
                    if (args.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Command should not start with 'dotnet'");
                    }

                    using var dotnet = new DotnetMuxer(environmentVariables);
                    int exitCode = await dotnet.Args(args, workingDirectory);

                    return exitCode != 0 && failIfReturnValueIsNotZero
                        ? throw new InvalidOperationException($"Command 'dotnet {args}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}")
                        : new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines);
                }, 3, TimeSpan.FromSeconds(3), exception => exception.ToString().Contains("Plugin 'CredentialProvider.Microsoft' failed"));
        }
        finally
        {
            s_maxOutstandingCommands_semaphore.Release();
        }
    }
}
