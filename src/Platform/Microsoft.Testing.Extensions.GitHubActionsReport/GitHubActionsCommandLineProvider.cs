// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
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
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsAnnotations, GitHubActionsResources.AnnotationsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsStepSummary, GitHubActionsResources.StepSummaryOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices, GitHubActionsResources.SlowTestNoticesOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold, GitHubActionsResources.SlowTestThresholdOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsOptionName, GitHubActionsResources.OptionDescription, ArgumentArity.Zero, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            GitHubActionsCommandLineOptions.GitHubActionsGroups or GitHubActionsCommandLineOptions.GitHubActionsAnnotations or GitHubActionsCommandLineOptions.GitHubActionsStepSummary or GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices
                when !CommandLineOptionArgumentValidator.IsValidBooleanArgument(arguments[0])
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidOnOffValue, arguments[0])),
            GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold
                when !(TimeSpanParser.TryParse(arguments[0], TimeSpanDefaultUnit.Seconds, out TimeSpan threshold) && threshold > TimeSpan.Zero)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidSlowTestThreshold, arguments[0])),
            _ => ValidationResult.ValidTask,
        };
}
