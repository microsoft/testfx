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

    internal static string GetArgumentsResponseFilePath(string retryRootFolder, int attemptCount)
        => Path.Combine(retryRootFolder, $"retry-arguments-{attemptCount}.rsp");

    internal static string GetFilterUidsResponseFilePath(string retryRootFolder, int attemptCount)
        => Path.Combine(retryRootFolder, $"retry-filter-uids-{attemptCount}.rsp");

    /// <summary>
    /// Computes the indices of the original executable arguments that must be dropped when restarting the test
    /// host, namely the retry-specific options and the result-directory option (which is re-injected per attempt).
    /// </summary>
    public static List<int> ComputeIndicesToCleanup(string[] executableArguments)
    {
        List<int> indexToCleanup = [];

        if (!AddOptionIndicesToCleanup(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        AddOptionIndicesToCleanup(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName);
        AddOptionIndicesToCleanup(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName);
        AddOptionIndicesToCleanup(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);
        AddOptionIndicesToCleanup(PlatformCommandLineProvider.ResultDirectoryOptionKey);

        return indexToCleanup;

        bool AddOptionIndicesToCleanup(string optionName)
        {
            string shortForm = $"-{optionName}";
            string longForm = $"-{shortForm}";
            bool found = false;

            for (int i = 0; i < executableArguments.Length; i++)
            {
                string argument = executableArguments[i];
                bool isSeparate = argument == shortForm || argument == longForm;
                bool isInline = argument.StartsWith(shortForm + "=", StringComparison.Ordinal)
                    || argument.StartsWith(shortForm + ":", StringComparison.Ordinal)
                    || argument.StartsWith(longForm + "=", StringComparison.Ordinal)
                    || argument.StartsWith(longForm + ":", StringComparison.Ordinal);
                if (!isSeparate && !isInline)
                {
                    continue;
                }

                found = true;
                indexToCleanup.Add(i);
                if (isSeparate && i + 1 < executableArguments.Length)
                {
                    indexToCleanup.Add(i + 1);
                }
            }

            return found;
        }
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
        => await BuildAttemptArgumentsAsync(
            fileSystem,
            executableArguments,
            executableArguments,
            indexToCleanup,
            currentTryResultFolder,
            retryRootFolder,
            pipeName,
            lastListOfFailedId,
            attemptCount).ConfigureAwait(false);

    public static async Task<List<string>> BuildAttemptArgumentsAsync(
        IFileSystem fileSystem,
        string[] executableArguments,
        string[] originalExecutableArguments,
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

        int responseFileIndex = Array.FindIndex(originalExecutableArguments, argument => argument.StartsWith("@", StringComparison.Ordinal));
        List<string>? directPrefixArguments = null;
        if (responseFileIndex >= 0)
        {
            directPrefixArguments = [];
            for (int i = 0; i < responseFileIndex; i++)
            {
                if (!indexToCleanup.Contains(i))
                {
                    directPrefixArguments.Add(executableArguments[i]);
                }
            }
        }

        // When retrying, replace any existing test filter with --filter-uid for the failed tests.
        if (lastListOfFailedId is { Length: > 0 })
        {
            RemoveRetryOnlyOptions(finalArguments);
            if (directPrefixArguments is not null)
            {
                RemoveRetryOnlyOptions(directPrefixArguments);
            }
        }

        if (directPrefixArguments is not null
            && !finalArguments.Skip(directPrefixArguments.Count).Any(argument => argument.IndexOf('"') >= 0))
        {
            string responseFilePath = GetArgumentsResponseFilePath(retryRootFolder, attemptCount);
            using (IFileStream stream = fileSystem.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream.Stream))
            {
                foreach (string argument in finalArguments.Skip(directPrefixArguments.Count))
                {
                    await writer.WriteLineAsync($"\"{argument}\"").ConfigureAwait(false);
                }
            }

            finalArguments.RemoveRange(directPrefixArguments.Count, finalArguments.Count - directPrefixArguments.Count);
            finalArguments.Add($"@{responseFilePath}");
        }

        // Fix result folder
        finalArguments.Add($"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}");
        finalArguments.Add(currentTryResultFolder);

        // Point the child process at the retry pipe server.
        finalArguments.Add($"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}");
        finalArguments.Add(pipeName);

        if (lastListOfFailedId is { Length: > 0 })
        {
            // The RSP parser (ResponseFileHelper.SplitCommandLine) strips all '"' characters
            // from tokens, so UIDs containing literal '"' (e.g. parameterized tests with
            // string arguments that include double quotes) cannot safely round-trip through
            // a response file. In that case we must always use inline arguments.
            bool hasUidsWithQuotes = lastListOfFailedId.Any(uid => uid.IndexOf('"') >= 0);

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
                string responseFilePath = GetFilterUidsResponseFilePath(retryRootFolder, attemptCount);
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

        static void RemoveRetryOnlyOptions(List<string> arguments)
        {
            RetryOrchestratorHelper.RemoveOption(arguments, TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter);
            RetryOrchestratorHelper.RemoveOption(arguments, PlatformCommandLineProvider.FilterUidOptionKey);

            // A retry only re-runs the previously failed tests, so the original full-suite threshold must not apply.
            RetryOrchestratorHelper.RemoveOption(arguments, PlatformCommandLineProvider.MinimumExpectedTestsOptionKey);
        }
    }
}
