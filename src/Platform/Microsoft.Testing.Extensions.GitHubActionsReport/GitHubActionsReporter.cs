// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed class GitHubActionsReporter :
    ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer
{
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDisplay;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly string _targetFrameworkMoniker;

    public GitHubActionsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IOutputDevice outputDisplay,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory)
    {
        _commandLine = commandLine;
        _environment = environment;
        _outputDisplay = outputDisplay;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _logger = loggerFactory.CreateLogger<GitHubActionsReporter>();
        _targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker();
    }

    /// <inheritdoc />
    public string Uid => nameof(GitHubActionsReporter);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = GitHubActionsResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = GitHubActionsResources.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
    {
        bool isEnabled = GitHubActionsFeature.IsEnabled(_commandLine, _environment, GitHubActionsCommandLineOptions.GitHubActionsGroups);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{nameof(GitHubActionsReport)} groups is {(isEnabled ? "enabled" : "disabled")}.");
        }

        return Task.FromResult(isEnabled);
    }

    /// <inheritdoc />
    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        string name = _testApplicationModuleInfo.TryGetAssemblyName() ?? _testApplicationModuleInfo.GetDisplayName();
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.GroupTitle, name, _targetFrameworkMoniker);
        await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData($"::group::{GitHubActionsEscaper.EscapeData(title)}"), testSessionContext.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        => await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData("::endgroup::"), testSessionContext.CancellationToken).ConfigureAwait(false);
}
