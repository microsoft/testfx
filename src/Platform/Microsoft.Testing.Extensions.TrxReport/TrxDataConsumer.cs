// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Extensions.TrxReport.Resources;
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

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGenerator :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptionsService;
    private readonly IFileSystem _fileSystem;
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
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension = [];
    private readonly bool _isEnabled;

    private DateTimeOffset? _testStartTime;
    private bool _adapterSupportTrxCapability;

    public TrxReportGenerator(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptionsService,
        IFileSystem fileSystem,
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
        _fileSystem = fileSystem;
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
        typeof(SessionFileArtifact)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid => nameof(TrxReportGenerator);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        // This is only run in TestHost, and not TestHostController.
        cancellationToken.ThrowIfCancellationRequested();

        switch (value)
        {
            case TestNodeUpdateMessage nodeChangedMessage:
                TestNodeStateProperty? nodeState = nodeChangedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
                if (nodeState is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
                {
                    return Task.CompletedTask;
                }

                _tests.Add(nodeChangedMessage);

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

        return Task.CompletedTask;
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        // This is only run in TestHost, and not TestHostController.
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        bool shouldUseOutOfProcessTrxGeneration = TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptionsService);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            await _logger.LogDebugAsync($"""
CrashDumpCommandLineOptions.CrashDumpOptionName: {_commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)}
TrxReportGeneratorCommandLine.IsTrxReportEnabled: {_commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)}
shouldUseOutOfProcessTrxGeneration: {shouldUseOutOfProcessTrxGeneration}
""").ConfigureAwait(false);
        }

        if (shouldUseOutOfProcessTrxGeneration)
        {
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);

            // This tells the TestHostController process the info of the registered ITestFramework.
            // The TestHostController needs that info to create the TrxReportEngine so that the info are written to the TRX file.
            // Note: TestHostController cannot retrieve ITestFramework from service provider which is why we need the extra complexity here.
            // TODO: Investigate if TestHostController can/should have access to ITestFramework and simplify this.
            await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<TestAdapterInformationRequest, VoidResponse>(new TestAdapterInformationRequest(_testFramework.Uid, _testFramework.Version), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
        }

        ITrxReportCapability? trxCapability = _testFrameworkCapabilities.GetCapability<ITrxReportCapability>();
        if (trxCapability is not null && trxCapability.IsSupported)
        {
            _adapterSupportTrxCapability = true;
            trxCapability.Enable();
        }

        _testStartTime = _clock.UtcNow;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        // This is only run in TestHost, and not TestHostController.
        // The current implementation tries to keep the TRX generation logic always in-process.
        // However, in case of a crash, the TestHostController takes over this responsibility (when running in out-of-proc mode).
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        if (!_adapterSupportTrxCapability)
        {
            await _outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFramework.DisplayName, _testFramework.Uid)), testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        ApplicationStateGuard.Ensure(_testStartTime is not null);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        var trxReportGeneratorEngine = new TrxReportEngine(_fileSystem, _testApplicationModuleInfo, _environment, _commandLineOptionsService, _configuration,
            _clock, _artifactsByExtension,
            _testFramework, _testStartTime.Value, exitCode, cancellationToken);
        (string reportFileName, string? warning) = await trxReportGeneratorEngine.GenerateReportAsync([.. _tests]).ConfigureAwait(false);
        if (warning is not null)
        {
            await _outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        // TRX can run in two modes. In-process or out-of-process.
        // If we are already running with the in-process mode, we publish the SessionFileArtifact to the message bus directly.
        // If we are running with out-of-process mode, we communicate via pipe to the TestHostController and send the ReportFileNameRequest.
        if (!TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptionsService))
        {
            await _messageBus.PublishAsync(this, new SessionFileArtifact(testSessionContext.SessionUid, new FileInfo(reportFileName), ExtensionResources.TrxReportArtifactDisplayName, ExtensionResources.TrxReportArtifactDescription)).ConfigureAwait(false);
        }
        else
        {
            // The TestHostController will receive the TRX file name.
            // Then, it will **modify** it and add any additional artifacts that were produced by TestHostController.
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);
            await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<ReportFileNameRequest, VoidResponse>(new ReportFileNameRequest(reportFileName), cancellationToken).ConfigureAwait(false);
        }
    }
}
