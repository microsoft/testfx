// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    private static int s_binlogCounter;

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
        int timeoutInSeconds = 10000,
        int retryCount = 5,
        bool disableCodeCoverage = true,
        bool warnAsError = true,
        bool suppressPreviewDotNetMessage = true,
        [CallerMemberName] string callerMemberName = "")
    {
        await s_maxOutstandingCommands_semaphore.WaitAsync();
        try
        {
            environmentVariables ??= [];
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

            string extraArgs = warnAsError ? " -p:MSBuildTreatWarningsAsErrors=true" : string.Empty;
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
                return await CallTheMuxerAsync(args, environmentVariables, workingDirectory, timeoutInSeconds, failIfReturnValueIsNotZero, callerMemberName);
            }
            else
            {
                IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(TimeSpan.FromSeconds(3), retryCount, factor: 1.5);
                return await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(delay)
                    .ExecuteAsync(async () => await CallTheMuxerAsync(args, environmentVariables, workingDirectory, timeoutInSeconds, failIfReturnValueIsNotZero, callerMemberName));
            }
        }
        finally
        {
            s_maxOutstandingCommands_semaphore.Release();
        }
    }

    private static bool IsDotNetTestWithExeOrDll(string args)
        => args.StartsWith("test ", StringComparison.Ordinal) && (args.Contains(".dll") || args.Contains(".exe"));

    // Workaround NuGet issue https://github.com/NuGet/Home/issues/14064
    private static async Task<DotnetMuxerResult> CallTheMuxerAsync(string args, Dictionary<string, string?> environmentVariables, string? workingDirectory, int timeoutInSeconds, bool failIfReturnValueIsNotZero, string binlogBaseFileName)
        => await Policy
            .Handle<InvalidOperationException>(ex => ex.Message.Contains("MSB4236"))
            .WaitAndRetryAsync(retryCount: 3, sleepDurationProvider: static _ => TimeSpan.FromSeconds(2))
            .ExecuteAsync(async () => await CallTheMuxerCoreAsync(args, environmentVariables, workingDirectory, timeoutInSeconds, failIfReturnValueIsNotZero, binlogBaseFileName));

    private static async Task<DotnetMuxerResult> CallTheMuxerCoreAsync(string args, Dictionary<string, string?> environmentVariables, string? workingDirectory, int timeoutInSeconds, bool failIfReturnValueIsNotZero, string binlogBaseFileName)
    {
        if (args.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Command should not start with 'dotnet'");
        }

        string? binlogFullPath = null;
        if (!args.Contains("-bl:") && !IsDotNetTestWithExeOrDll(args))
        {
            // We do this here rather than in the caller so that different retries produce different binlog file names.
            binlogFullPath = Path.Combine(TempDirectory.TestSuiteDirectory, $"{binlogBaseFileName}-{Interlocked.Increment(ref s_binlogCounter)}.binlog");
            string binlogArg = $" -bl:\"{binlogFullPath}\"";
            if (args.IndexOf("-- ", StringComparison.Ordinal) is int platformArgsIndex && platformArgsIndex > 0)
            {
                args = args.Insert(platformArgsIndex, binlogArg + " ");
            }
            else
            {
                args += binlogArg;
            }
        }

        using DotnetMuxer dotnet = new(environmentVariables);
        int exitCode = await dotnet.ExecuteAsync(args, workingDirectory, timeoutInSeconds);

        if (dotnet.StandardError.Contains("Invalid runtimeconfig.json"))
        {
            // Invalid runtimeconfig.json [D:\a\_work\1\s\artifacts\tmp\Release\testsuite\gqRdj\MSTestSdk\bin\Debug\net9.0\MSTestSdk.runtimeconfig.json]
            Match match = Regex.Match(dotnet.StandardError, @"Invalid runtimeconfig\.json \[(?<path>.+?)\]");
            string fileContent;
            if (!match.Success)
            {
                fileContent = "CANNOT MATCH PATH IN REGEX";
            }
            else
            {
                string filePath = match.Groups["path"].Value;
                fileContent = !File.Exists(filePath)
                    ? $"FILE DOES NOT EXIST: {filePath}"
                    : File.ReadAllText(filePath);
            }

            throw new InvalidOperationException($"Invalid runtimeconfig.json:{fileContent}\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}");
        }

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
        return new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines, binlogFullPath);
    }
}
