// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

namespace Microsoft.Testing.Extensions;

internal abstract class ReportGeneratorBase<TGenerator, TCapturedTestResult> :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
    where TGenerator : ReportGeneratorBase<TGenerator, TCapturedTestResult>
    where TCapturedTestResult : class
{
#pragma warning disable SA1401 // Fields should be private - derived report generators create report-specific engines from these services.
    protected readonly IConfiguration _configuration;
    protected readonly ICommandLineOptions _commandLineOptions;
    protected readonly IFileSystem _fileSystem;
    protected readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    protected readonly IClock _clock;
    protected readonly IEnvironment _environment;
    protected readonly ITestFramework _testFramework;
#pragma warning restore SA1401

    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ILogger<TGenerator> _logger;
    private readonly List<TCapturedTestResult> _tests = [];
    private readonly bool _isEnabled;

    private DateTimeOffset? _testStartTime;

    protected ReportGeneratorBase(
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
        ILogger<TGenerator> logger,
        string optionName)
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
        _isEnabled = commandLineOptions.IsOptionSet(optionName);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public abstract string Uid { get; }

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is TestNodeUpdateMessage update)
        {
            OnTestNodeUpdate(update);
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

        await _logger.LogTraceAsync(GetGenerationLogMessage(_tests.Count)).ConfigureAwait(false);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        (string reportFileName, string? warning) = await GenerateReportAsync([.. _tests], _testStartTime.Value, exitCode, cancellationToken).ConfigureAwait(false);

        if (warning is not null)
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
        }

        await _messageBus.PublishAsync(
            this,
            new SessionFileArtifact(
                testSessionContext.SessionUid,
                new FileInfo(reportFileName),
                ArtifactDisplayName,
                ArtifactDescription)).ConfigureAwait(false);
    }

    protected virtual void OnTestNodeUpdate(TestNodeUpdateMessage update)
    {
        TCapturedTestResult? captured = TryCapture(update);
        if (captured is not null)
        {
            _tests.Add(captured);
        }
    }

    protected abstract string ArtifactDisplayName { get; }

    protected abstract string ArtifactDescription { get; }

    protected abstract string GetGenerationLogMessage(int testResultCount);

    protected abstract TCapturedTestResult? TryCapture(TestNodeUpdateMessage update);

    protected abstract Task<(string FileName, string? Warning)> GenerateReportAsync(
        TCapturedTestResult[] tests,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken);
}
