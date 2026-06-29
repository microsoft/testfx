// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed class GitHubActionsCommandLineProvider : CommandLineOptionsProviderBase
{
    private static readonly string[] OnOffOptions = [GitHubActionsCommandLineOptions.OptionOn, GitHubActionsCommandLineOptions.OptionOff];

    public GitHubActionsCommandLineProvider()
        : base(
            nameof(GitHubActionsCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            GitHubActionsResources.DisplayName,
            GitHubActionsResources.Description,
            [
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsGroups, GitHubActionsResources.GroupsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsAnnotations, GitHubActionsResources.AnnotationsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(GitHubActionsCommandLineOptions.GitHubActionsOptionName, GitHubActionsResources.OptionDescription, ArgumentArity.Zero, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            GitHubActionsCommandLineOptions.GitHubActionsGroups or GitHubActionsCommandLineOptions.GitHubActionsAnnotations
                when !OnOffOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidOnOffValue, arguments[0])),
            _ => ValidationResult.ValidTask,
        };
}
