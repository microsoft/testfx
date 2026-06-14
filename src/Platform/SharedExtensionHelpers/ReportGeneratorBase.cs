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
    // MTP guarantees that ConsumeAsync is called sequentially (never concurrently)
    // for a given consumer instance, so List<T> is safe here without locking.
    private readonly List<TCapturedTestResult> _tests = [];
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ILogger<TGenerator> _logger;
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
        Configuration = configuration;
        CommandLineOptions = commandLineOptions;
        FileSystem = fileSystem;
        TestApplicationModuleInfo = testApplicationModuleInfo;
        _messageBus = messageBus;
        Clock = clock;
        Environment = environment;
        _outputDevice = outputDevice;
        TestFramework = testFramework;
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

    protected IConfiguration Configuration { get; }

    protected ICommandLineOptions CommandLineOptions { get; }

    protected IFileSystem FileSystem { get; }

    protected ITestApplicationModuleInfo TestApplicationModuleInfo { get; }

    protected IClock Clock { get; }

    protected IEnvironment Environment { get; }

    protected ITestFramework TestFramework { get; }

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
        _testStartTime = Clock.UtcNow;
        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset testStartTime = _testStartTime ?? throw ApplicationStateGuard.Unreachable();

        await _logger.LogTraceAsync(GetGenerationLogMessage(_tests.Count)).ConfigureAwait(false);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        (string reportFileName, string? warning) = await GenerateReportAsync([.. _tests], testStartTime, exitCode, cancellationToken).ConfigureAwait(false);

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

    // Capture every update unconditionally — no UID-based deduplication.
    // CTRF relies on this to detect flaky tests (earlier attempts become retryAttempts[];
    // see CtrfReportEngine.CollapseAttempts). HTML/JUnit rely on it to surface all results
    // for tests that emit multiple updates per UID (parameterized rows, in-process retries,
    // framework quirks). Engine-side logic handles any deduplication.
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
