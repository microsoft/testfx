// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static class GitHubActionsCommandLineOptions
{
    public const string GitHubActionsOptionName = "report-gh";
    public const string GitHubActionsGroups = "report-gh-groups";
    public const string GitHubActionsAnnotations = "report-gh-annotations";
    public const string GitHubActionsStepSummary = "report-gh-step-summary";
    public const string GitHubActionsSlowTestNotices = "report-gh-slow-test-notices";
    public const string GitHubActionsSlowTestThreshold = "report-gh-slow-test-threshold";
    public const string OptionOn = "on";
    public const string OptionOff = "off";

    public const int SlowTestThresholdDefaultSeconds = 60;
}
