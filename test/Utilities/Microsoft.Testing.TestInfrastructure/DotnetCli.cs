﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Microsoft.Testing.TestInfrastructure;

public static class DotnetCli
{
    private static readonly string[] CodeCoverageEnvironmentVariables =
    [
        "MicrosoftInstrumentationEngine_ConfigPath32_VanguardInstrumentationProfiler",
        "MicrosoftInstrumentationEngine_ConfigPath64_VanguardInstrumentationProfiler",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "COR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "CODE_COVERAGE_SESSION_NAME",
        "CODE_COVERAGE_PIPE_PATH",
        "MicrosoftInstrumentationEngine_LogLevel",
        "MicrosoftInstrumentationEngine_DisableCodeSignatureValidation",
        "MicrosoftInstrumentationEngine_FileLogPath"
    ];

    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "It's causing some runtime bug")]
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

    public static bool DoNotRetry { get; set; }

    public static async Task<DotnetMuxerResult> RunAsync(
        string args,
        string nugetGlobalPackagesFolder,
        string? workingDirectory = null,
        Dictionary<string, string?>? environmentVariables = null,
        bool failIfReturnValueIsNotZero = true,
        bool disableTelemetry = true,
        int timeoutInSeconds = 50,
        int retryCount = 5,
        bool disableCodeCoverage = true,
        bool warnAsError = true,
        bool suppressPreviewDotNetMessage = true)
    {
        await s_maxOutstandingCommands_semaphore.WaitAsync();
        try
        {
            environmentVariables ??= new Dictionary<string, string?>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                // Skip all unwanted environment variables.
                string? key = entry.Key.ToString();
                if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (disableCodeCoverage)
                {
                    // Disable the code coverage during the build.
                    if (CodeCoverageEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // We use TryAdd to let tests "overwrite" existing environment variables.
                // Consider that the given dictionary has "TESTINGPLATFORM_UI_LANGUAGE" as a key.
                // And also Environment.GetEnvironmentVariables() is returning TESTINGPLATFORM_UI_LANGUAGE.
                // In that case, we do a "TryAdd" which effectively means the value from the original dictionary wins.
                environmentVariables.TryAdd(key!, entry.Value!.ToString()!);
            }

            if (disableTelemetry)
            {
                environmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            }

            environmentVariables["NUGET_PACKAGES"] = nugetGlobalPackagesFolder;

            string extraArgs = warnAsError ? " /warnaserror" : string.Empty;
            extraArgs += suppressPreviewDotNetMessage ? " -p:SuppressNETCoreSdkPreviewMessage=true" : string.Empty;
            if (args.IndexOf("-- ", StringComparison.Ordinal) is int platformArgsIndex && platformArgsIndex > 0)
            {
                args = args.Insert(platformArgsIndex, extraArgs + " ");
            }
            else
            {
                args += extraArgs;
            }

            if (DoNotRetry)
            {
                return await CallTheMuxerAsync(args, environmentVariables, workingDirectory, timeoutInSeconds, failIfReturnValueIsNotZero);
            }
            else
            {
                IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(TimeSpan.FromSeconds(3), retryCount, factor: 1.5);
                return await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(delay)
                    .ExecuteAsync(async () => await CallTheMuxerAsync(args, environmentVariables, workingDirectory, timeoutInSeconds, failIfReturnValueIsNotZero));
            }
        }
        finally
        {
            s_maxOutstandingCommands_semaphore.Release();
        }
    }

    private static async Task<DotnetMuxerResult> CallTheMuxerAsync(string args, Dictionary<string, string?> environmentVariables, string? workingDirectory, int timeoutInSeconds, bool failIfReturnValueIsNotZero)
    {
        if (args.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Command should not start with 'dotnet'");
        }

        using DotnetMuxer dotnet = new(environmentVariables);
        int exitCode = await dotnet.ExecuteAsync(args, workingDirectory, timeoutInSeconds);

        if (exitCode != 0 && failIfReturnValueIsNotZero)
        {
            throw new InvalidOperationException($"Command 'dotnet {args}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}");
        }

        if (dotnet.StandardOutput.Contains("error MSB4166: Child node")
            && dotnet.StandardOutput.Contains("exited prematurely. Shutting down."))
        {
            throw new InvalidOperationException($"Command 'dotnet {args}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}");
        }

        // Return a result object and let caller decide what to do with it.
        return new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines);
    }
}
