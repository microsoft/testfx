// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Surfaces tests that are still running past a per-test threshold as GitHub Actions <c>::notice</c>
/// workflow commands, mirroring <c>AzureDevOpsSlowTestReporter</c>.
/// </summary>
internal sealed class GitHubActionsSlowTestReporter : SlowTestReporterBase
{
    private readonly bool _isEnabled;
    private readonly TimeSpan _threshold;

    public GitHubActionsSlowTestReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory)
        : base(outputDevice, task, clock, loggerFactory.CreateLogger<GitHubActionsSlowTestReporter>())
    {
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices);
        _threshold = TimeSpan.FromSeconds(GetThresholdSeconds(commandLineOptions));
    }

    public override string Uid => nameof(GitHubActionsSlowTestReporter);

    public override string DisplayName => GitHubActionsResources.DisplayName;

    public override string Description => GitHubActionsResources.Description;

    protected override bool IsEnabled => _isEnabled;

    protected override string GetTestName(TestNode testNode) => TestNodeIdentity.GetTestName(testNode);

    protected override TimeSpan ResolveThreshold(string testName) => _threshold;

    protected override Task EmitSlowTestAsync(string testName, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        string line = BuildNoticeLine(testName, elapsed);
        return OutputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken);
    }

    internal static /* for testing */ string BuildNoticeLine(string testName, TimeSpan elapsed)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.SlowTestStillRunning,
            testName,
            ((long)elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.SlowTestNoticeTitle, testName);

        return string.Format(
            CultureInfo.InvariantCulture,
            "::notice title={0}::{1}",
            GitHubActionsEscaper.EscapeProperty(title),
            GitHubActionsEscaper.EscapeData(message));
    }

    // Re-reads the already-provider-validated threshold option (the CLI provider guarantees a parseable
    // positive integer). This mirrors the sibling AzureDevOpsSlowTestReporter, which likewise reads its
    // history options straight from ICommandLineOptions rather than threading a parsed value through.
    private static int GetThresholdSeconds(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold, out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds >= 1
                ? seconds
                : GitHubActionsCommandLineOptions.SlowTestThresholdDefaultSeconds;
}
