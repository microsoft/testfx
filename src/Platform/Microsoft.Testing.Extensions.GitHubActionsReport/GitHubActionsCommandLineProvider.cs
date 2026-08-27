// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed class GitHubActionsCommandLineProvider : CommandLineOptionsProviderBase
{
    public GitHubActionsCommandLineProvider()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "GitHubActionsCommandLineProvider",
            ExtensionVersion.DefaultSemVer,
            GitHubActionsResources.DisplayName,
            GitHubActionsResources.Description,
            [
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsGroups, GitHubActionsResources.GroupsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsHistory, GitHubActionsResources.HistoryOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow, GitHubActionsResources.HistoryWindowOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsAnnotations, GitHubActionsResources.AnnotationsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsStepSummary, GitHubActionsResources.StepSummaryOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections, GitHubActionsResources.StepSummarySectionsOptionDescription, ArgumentArity.OneOrMore, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsFailureDetails, GitHubActionsResources.FailureDetailsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices, GitHubActionsResources.SlowTestNoticesOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold, GitHubActionsResources.SlowTestThresholdOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsOptionName, GitHubActionsResources.OptionDescription, ArgumentArity.Zero, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            GitHubActionsCommandLineOptions.GitHubActionsStepSummary
                when !CommandLineOptionArgumentValidator.IsValidBooleanArgument(arguments[0])
                    && !arguments[0].Equals(GitHubActionsCommandLineOptions.StepSummaryOnFailureValue, StringComparison.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidStepSummaryValue, arguments[0])),
            GitHubActionsCommandLineOptions.GitHubActionsGroups or GitHubActionsCommandLineOptions.GitHubActionsAnnotations or GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices or GitHubActionsCommandLineOptions.GitHubActionsFailureDetails
                when !CommandLineOptionArgumentValidator.IsValidBooleanArgument(arguments[0])
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidOnOffValue, arguments[0])),
            GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold
                when !(TimeSpanParser.TryParse(arguments[0], TimeSpanDefaultUnit.Seconds, out TimeSpan threshold) && threshold > TimeSpan.Zero)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidSlowTestThreshold, arguments[0])),
            GitHubActionsCommandLineOptions.GitHubActionsHistory
                when arguments is not [string historyPath] || !IsValidHistoryPath(historyPath)
                => ValidationResult.InvalidTask(GitHubActionsResources.InvalidHistoryPath),
            GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow
                when !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int historyWindow)
                || historyWindow is < 1 or > 90
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidHistoryWindow, arguments[0])),
            GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections
                when !GitHubActionsStepSummarySectionsParser.TryParse(arguments, out _, out string? invalidValue, out bool hasEmptyValue)
                => ValidationResult.InvalidTask(
                    hasEmptyValue
                        ? GitHubActionsResources.EmptyStepSummarySections
                        : string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidStepSummarySection, invalidValue)),
            _ => ValidationResult.ValidTask,
        };

    private static bool IsValidHistoryPath(string path)
    {
        if (RoslynString.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            return !Directory.Exists(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => RequiresMainOption(
            commandLineOptions,
            [
                GitHubActionsCommandLineOptions.GitHubActionsGroups,
                GitHubActionsCommandLineOptions.GitHubActionsHistory,
                GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow,
                GitHubActionsCommandLineOptions.GitHubActionsAnnotations,
                GitHubActionsCommandLineOptions.GitHubActionsStepSummary,
                GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections,
                GitHubActionsCommandLineOptions.GitHubActionsFailureDetails,
                GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices,
                GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold,
            ],
            GitHubActionsCommandLineOptions.GitHubActionsOptionName,
            () => string.Format(CultureInfo.CurrentCulture, GitHubActionsResources.SubOptionsRequireReportGh, GitHubActionsCommandLineOptions.GitHubActionsOptionName))
        ?? RequiresHistoryOptionAsync(commandLineOptions);

    private static Task<ValidationResult> RequiresHistoryOptionAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow)
           && !commandLineOptions.IsOptionSet(GitHubActionsCommandLineOptions.GitHubActionsHistory)
               ? ValidationResult.InvalidTask(GitHubActionsResources.HistoryWindowRequiresHistory)
               : ValidationResult.ValidTask;
}
