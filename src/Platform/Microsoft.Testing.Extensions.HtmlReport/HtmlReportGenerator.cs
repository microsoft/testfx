// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportGenerator :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IMessageBus _messageBus;
    private readonly IClock _clock;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestFramework _testFramework;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ILogger<HtmlReportGenerator> _logger;
    private readonly List<CapturedTestResult> _tests = [];
    private readonly bool _isEnabled;

    private DateTimeOffset? _testStartTime;

    public HtmlReportGenerator(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IMessageBus messageBus,
        IClock clock,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITestFramework testFramework,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ILogger<HtmlReportGenerator> logger)
    {
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
        _fileSystem = fileSystem;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _messageBus = messageBus;
        _clock = clock;
        _environment = environment;
        _outputDevice = outputDevice;
        _testFramework = testFramework;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _logger = logger;
        _isEnabled = commandLineOptions.IsOptionSet(HtmlReportGeneratorCommandLine.HtmlReportOptionName);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid => nameof(HtmlReportGenerator);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.HtmlReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.HtmlReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is TestNodeUpdateMessage update)
        {
            // Project to a capped DTO immediately so we don't retain the original
            // TestNode (and its potentially huge stdout/stderr/stack trace strings)
            // for the whole session. We never deduplicate on TestNode.Uid: some
            // frameworks emit several distinct results sharing the same UID
            // (parameterized rows, theory data, in-process retries, framework
            // misbehaviour). The engine surfaces all of them so no data is dropped.
            CapturedTestResult? captured = TestResultCapture.TryCapture(update.TestNode);
            if (captured is not null)
            {
                _tests.Add(captured);
            }
        }

        return Task.CompletedTask;
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        testSessionContext.CancellationToken.ThrowIfCancellationRequested();
        _testStartTime = _clock.UtcNow;
        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        ApplicationStateGuard.Ensure(_testStartTime is not null);

        await _logger.LogTraceAsync($"Generating HTML report for {_tests.Count} test result(s).").ConfigureAwait(false);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        var engine = new HtmlReportEngine(
            _fileSystem,
            _testApplicationModuleInfo,
            _environment,
            _commandLineOptions,
            _configuration,
            _clock,
            _testFramework,
            _testStartTime.Value,
            exitCode,
            cancellationToken);

        (string reportFileName, string? warning) = await engine.GenerateReportAsync([.. _tests]).ConfigureAwait(false);

        if (warning is not null)
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
        }

        await _messageBus.PublishAsync(
            this,
            new SessionFileArtifact(
                testSessionContext.SessionUid,
                new FileInfo(reportFileName),
                ExtensionResources.HtmlReportArtifactDisplayName,
                ExtensionResources.HtmlReportArtifactDescription)).ConfigureAwait(false);
    }
}
