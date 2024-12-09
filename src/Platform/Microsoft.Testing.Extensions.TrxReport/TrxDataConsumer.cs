// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGenerator :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptionsService;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IMessageBus _messageBus;
    private readonly IClock _clock;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDisplay;
    private readonly ITestFramework _testFramework;
    private readonly ITestFrameworkCapabilities _testFrameworkCapabilities;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly TrxTestApplicationLifecycleCallbacks? _trxTestApplicationLifecycleCallbacks;
    private readonly ILogger<TrxReportGenerator> _logger;
    private readonly List<TestNodeUpdateMessage> _tests = [];
    private readonly Dictionary<TestNodeUid, List<SessionFileArtifact>> _artifactsByTestNode = new();
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension = new();
    private readonly bool _isEnabled;

    private DateTimeOffset? _testStartTime;
    private int _failedTestsCount;
    private int _passedTestsCount;
    private int _notExecutedTestsCount;
    private int _timeoutTestsCount;
    private bool _adapterSupportTrxCapability;

    public TrxReportGenerator(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptionsService,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IMessageBus messageBus,
        IClock clock,
        IEnvironment environment,
        IOutputDevice outputDisplay,
        ITestFramework testFramework,
        ITestFrameworkCapabilities testFrameworkCapabilities,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        // Can be null in case of server mode
        TrxTestApplicationLifecycleCallbacks? trxTestApplicationLifecycleCallbacks,
        ILogger<TrxReportGenerator> logger)
    {
        _configuration = configuration;
        _commandLineOptionsService = commandLineOptionsService;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _messageBus = messageBus;
        _clock = clock;
        _environment = environment;
        _outputDisplay = outputDisplay;
        _testFramework = testFramework;
        _testFrameworkCapabilities = testFrameworkCapabilities;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _trxTestApplicationLifecycleCallbacks = trxTestApplicationLifecycleCallbacks;
        _logger = logger;
        _isEnabled = commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(TestNodeFileArtifact),
        typeof(SessionFileArtifact)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid { get; } = nameof(TrxReportGenerator);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (!_isEnabled || cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        try
        {
            switch (value)
            {
                case TestNodeUpdateMessage nodeChangedMessage:
                    TestNodeStateProperty nodeState = nodeChangedMessage.TestNode.Properties.Single<TestNodeStateProperty>();
                    if (nodeState is PassedTestNodeStateProperty)
                    {
                        _tests.Add(nodeChangedMessage);
                        _passedTestsCount++;
                    }
                    else if (nodeState is TimeoutTestNodeStateProperty)
                    {
                        _tests.Add(nodeChangedMessage);
                        _timeoutTestsCount++;
                    }
                    else if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, nodeState.GetType()) != -1)
                    {
                        _tests.Add(nodeChangedMessage);
                        _failedTestsCount++;
                    }
                    else if (nodeState is SkippedTestNodeStateProperty)
                    {
                        _tests.Add(nodeChangedMessage);
                        _notExecutedTestsCount++;
                    }

                    break;
                case TestNodeFileArtifact testNodeFileArtifact:
                    if (!_artifactsByTestNode.TryGetValue(testNodeFileArtifact.TestNode.Uid, out List<SessionFileArtifact>? nodeFileArtifacts))
                    {
                        nodeFileArtifacts = [testNodeFileArtifact];
                        _artifactsByTestNode[testNodeFileArtifact.TestNode.Uid] = nodeFileArtifacts;
                    }
                    else
                    {
                        nodeFileArtifacts.Add(testNodeFileArtifact);
                    }

                    break;
                case SessionFileArtifact fileArtifact:
                    if (!_artifactsByExtension.TryGetValue(dataProducer, out List<SessionFileArtifact>? sessionFileArtifacts))
                    {
                        sessionFileArtifacts = [fileArtifact];
                        _artifactsByExtension[dataProducer] = sessionFileArtifacts;
                    }
                    else
                    {
                        sessionFileArtifacts.Add(fileArtifact);
                    }

                    break;
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Do nothing, we're stopping
        }

        return Task.CompletedTask;
    }

    public async Task OnTestSessionStartingAsync(SessionUid _, CancellationToken cancellationToken)
    {
        if (!_isEnabled || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            await _logger.LogDebugAsync($"""
PlatformCommandLineProvider.ServerOption: {_commandLineOptionsService.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey)}
CrashDumpCommandLineOptions.CrashDumpOptionName: {_commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)}
TrxReportGeneratorCommandLine.IsTrxReportEnabled: {_commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)}
""");
        }

        if (!_commandLineOptionsService.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) &&
            _commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName))
        {
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);

            try
            {
                await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<TestAdapterInformationRequest, VoidResponse>(new TestAdapterInformationRequest(_testFramework.Uid, _testFramework.Version), cancellationToken)
                    .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                // Do nothing, we're stopping
            }
        }

        ITrxReportCapability? trxCapability = _testFrameworkCapabilities.GetCapability<ITrxReportCapability>();
        if (_isEnabled && trxCapability is not null && trxCapability.IsSupported)
        {
            _adapterSupportTrxCapability = true;
            trxCapability.Enable();
        }

        _testStartTime = _clock.UtcNow;
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (!_isEnabled || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (!_adapterSupportTrxCapability)
            {
                await _outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFramework.DisplayName, _testFramework.Uid)));
            }

            ApplicationStateGuard.Ensure(_testStartTime is not null);

            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            TrxReportEngine trxReportGeneratorEngine = new(_testApplicationModuleInfo, _environment, _commandLineOptionsService, _configuration,
            _clock, _tests.ToArray(), _failedTestsCount, _passedTestsCount, _notExecutedTestsCount, _timeoutTestsCount, _artifactsByExtension, _artifactsByTestNode,
            _adapterSupportTrxCapability, _testFramework, _testStartTime.Value, exitCode, cancellationToken);
            string reportFileName = await trxReportGeneratorEngine.GenerateReportAsync();

            if (
                // TestController is not used when we run in server mode
                _commandLineOptionsService.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) ||
                // If crash dump is not enabled we run trx in-process only
                !_commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName))
            {
                // In server mode we report the trx in-process
                await _messageBus.PublishAsync(this, new SessionFileArtifact(sessionUid, new FileInfo(reportFileName), ExtensionResources.TrxReportArtifactDisplayName, ExtensionResources.TrxReportArtifactDescription));
            }
            else
            {
                ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
                ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);
                await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<ReportFileNameRequest, VoidResponse>(new ReportFileNameRequest(reportFileName), cancellationToken);
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Do nothing, we're stopping
        }
    }
}
