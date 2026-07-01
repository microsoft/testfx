// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Wraps each test assembly's output in a GitHub Actions log group (<c>::group::</c> / <c>::endgroup::</c>)
/// so the runner UI collapses the assembly's test output by default.
/// </summary>
/// <remarks>
/// Like the Azure DevOps sibling (<c>AzureDevOpsLogGroupReporter</c>), this handler also implements
/// <see cref="IDataConsumer"/> (with a no-op <see cref="ConsumeAsync(IDataProducer, IData, CancellationToken)"/>)
/// purely so that, at session end, its <see cref="OnTestSessionFinishingAsync(ITestSessionContext)"/> runs in the
/// consumer phase — i.e. after the producer-only handlers. Combined with registering it last, this ensures the
/// closing <c>::endgroup::</c> is emitted after the other reporters' final output, so the group truly wraps the
/// whole assembly's output. The <see cref="_groupOpened"/> flag guarantees <c>::endgroup::</c> is only emitted
/// when a matching <c>::group::</c> was actually opened.
/// </remarks>
internal sealed class GitHubActionsReporter :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer
{
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDisplay;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly string _targetFrameworkMoniker;

    private bool _groupOpened;

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
        _targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerWithRuntimeFallback();
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
    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

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

    // No-op: this consumer subscribes to data only to be ordered in the consumer phase at session end
    // (see the type-level remarks). It does not act on individual messages.
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            string name = _testApplicationModuleInfo.TryGetAssemblyName() ?? _testApplicationModuleInfo.GetDisplayName();
            string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.GroupTitle, name, _targetFrameworkMoniker);
            await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData($"::group::{GitHubActionsEscaper.EscapeData(title)}"), testSessionContext.CancellationToken).ConfigureAwait(false);
            _groupOpened = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
    }

    /// <inheritdoc />
    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_groupOpened)
            {
                return;
            }

            await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData("::endgroup::"), testSessionContext.CancellationToken).ConfigureAwait(false);
            _groupOpened = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }
}
