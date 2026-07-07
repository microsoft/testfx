// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Pure argument-construction for the retry orchestrator: strips retry/result-dir flags from the original
/// command line and builds the per-attempt argument list (result directory, pipe name, and the failed-UID
/// filter, using a response file when the inline command line would exceed OS length limits). No output or
/// process concerns live here.
/// </summary>
internal static class RetryArgumentsBuilder
{
    // Estimate command line length to avoid hitting OS limits (~32K on Windows).
    // Add per-argument overhead to account for PasteArguments quoting on pre-.NET 8
    // targets where each argument may gain wrapping quotes and a separator space.
    private const int CommandLineLengthLimit = 30_000;
    private const int PerArgumentOverhead = 3;

    /// <summary>
    /// Computes the indices of the original executable arguments that must be dropped when restarting the test
    /// host, namely the retry-specific options and the result-directory option (which is re-injected per attempt).
    /// </summary>
    public static List<int> ComputeIndicesToCleanup(string[] executableArguments)
    {
        List<int> indexToCleanup = [];

        int argIndex = RetryOrchestratorHelper.GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, executableArguments);
        if (argIndex < 0)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        indexToCleanup.Add(argIndex);
        indexToCleanup.Add(argIndex + 1);

        argIndex = RetryOrchestratorHelper.GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        argIndex = RetryOrchestratorHelper.GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        argIndex = RetryOrchestratorHelper.GetOptionArgumentIndex(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            if (argIndex + 1 < executableArguments.Length)
            {
                indexToCleanup.Add(argIndex + 1);
            }
        }

        argIndex = RetryOrchestratorHelper.GetOptionArgumentIndex(PlatformCommandLineProvider.ResultDirectoryOptionKey, executableArguments);
        if (argIndex > -1)
        {
            indexToCleanup.Add(argIndex);
            indexToCleanup.Add(argIndex + 1);
        }

        return indexToCleanup;
    }

    /// <summary>
    /// Builds the argument list for a single retry attempt: the cleaned-up original arguments, the per-attempt
    /// result directory, the retry pipe name, and (on retry attempts) the failed-UID filter.
    /// </summary>
    public static async Task<List<string>> BuildAttemptArgumentsAsync(
        IFileSystem fileSystem,
        string[] executableArguments,
        List<int> indexToCleanup,
        string currentTryResultFolder,
        string retryRootFolder,
        string pipeName,
        string[]? lastListOfFailedId,
        int attemptCount)
    {
        List<string> finalArguments = [];

        // Cleanup the arguments
        for (int i = 0; i < executableArguments.Length; i++)
        {
            if (indexToCleanup.Contains(i))
            {
                continue;
            }

            finalArguments.Add(executableArguments[i]);
        }

        // Fix result folder
        finalArguments.Add($"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}");
        finalArguments.Add(currentTryResultFolder);

        // Point the child process at the retry pipe server.
        finalArguments.Add($"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}");
        finalArguments.Add(pipeName);

        // When retrying, replace any existing test filter with --filter-uid for the failed tests
        if (lastListOfFailedId is { Length: > 0 })
        {
            RetryOrchestratorHelper.RemoveOption(finalArguments, TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter);
            RetryOrchestratorHelper.RemoveOption(finalArguments, PlatformCommandLineProvider.FilterUidOptionKey);

            // Strip --minimum-expected-tests on retry attempts: a retry only re-runs the previously
            // failed tests, so propagating the original threshold (computed against the full test
            // set) would always trip the policy and fail the run. See issue #5639.
            RetryOrchestratorHelper.RemoveOption(finalArguments, PlatformCommandLineProvider.MinimumExpectedTestsOptionKey);

            // The RSP parser (ResponseFileHelper.SplitCommandLine) strips all '"' characters
            // from tokens, so UIDs containing literal '"' (e.g. parameterized tests with
            // string arguments that include double quotes) cannot safely round-trip through
            // a response file. In that case we must always use inline arguments.
            bool hasUidsWithQuotes = false;
            foreach (string uid in lastListOfFailedId)
            {
                if (uid.IndexOf('"') >= 0)
                {
                    hasUidsWithQuotes = true;
                    break;
                }
            }

            bool useResponseFile = false;
            if (!hasUidsWithQuotes)
            {
                int predictedLength = 0;
                foreach (string arg in finalArguments)
                {
                    predictedLength += arg.Length + PerArgumentOverhead;
                }

                predictedLength += 2 + PlatformCommandLineProvider.FilterUidOptionKey.Length + 1;
                foreach (string uid in lastListOfFailedId)
                {
                    predictedLength += uid.Length + PerArgumentOverhead;
                }

                useResponseFile = predictedLength > CommandLineLengthLimit;
            }

            if (!useResponseFile)
            {
                finalArguments.Add($"--{PlatformCommandLineProvider.FilterUidOptionKey}");
                finalArguments.AddRange(lastListOfFailedId);
            }
            else
            {
                // Use a response file to avoid exceeding command-line length limits.
                // Write to retryRootFolder (not the per-attempt folder) so it won't be included
                // in the final results move.
                string responseFilePath = Path.Combine(retryRootFolder, $"retry-filter-uids-{attemptCount}.rsp");
                using (IFileStream stream = fileSystem.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(stream.Stream))
                {
                    // Write all UIDs on a single line, each quoted. The RSP parser splits
                    // by whitespace and uses '"' for grouping, so quoting handles UIDs
                    // containing whitespace or starting with '#' (comment marker).
                    await writer.WriteAsync($"--{PlatformCommandLineProvider.FilterUidOptionKey}").ConfigureAwait(false);
                    foreach (string uid in lastListOfFailedId)
                    {
                        await writer.WriteAsync($" \"{uid}\"").ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync().ConfigureAwait(false);
                }

                finalArguments.Add($"@{responseFilePath}");
            }
        }

        return finalArguments;
    }
}
