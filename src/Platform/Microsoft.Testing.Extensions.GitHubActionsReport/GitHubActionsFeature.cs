// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Shared activation logic for the GitHub Actions reporters. The extension activates only when running
/// on GitHub Actions (<c>GITHUB_ACTIONS=true</c>) and the <c>--report-gh</c> master switch is set; each
/// individual feature is then on by default but can be turned off with its <c>--report-gh-*</c> knob set
/// to <c>off</c>.
/// </summary>
internal static class GitHubActionsFeature
{
    public static bool IsRunningOnGitHubActions(IEnvironment environment)
        => string.Equals(environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsMasterEnabled(ICommandLineOptions commandLine, IEnvironment environment)
        => IsRunningOnGitHubActions(environment)
            && commandLine.IsOptionSet(GitHubActionsCommandLineOptions.GitHubActionsOptionName);

    public static bool IsKnobEnabled(ICommandLineOptions commandLine, string knobOptionName)
        => !(commandLine.TryGetOptionArgumentList(knobOptionName, out string[]? arguments)
            && arguments is [string value]
            && string.Equals(value, GitHubActionsCommandLineOptions.OptionOff, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled(ICommandLineOptions commandLine, IEnvironment environment, string knobOptionName)
        => IsMasterEnabled(commandLine, environment) && IsKnobEnabled(commandLine, knobOptionName);
}
