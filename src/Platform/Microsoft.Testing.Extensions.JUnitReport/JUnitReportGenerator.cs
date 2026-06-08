// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;
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

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportGenerator :
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
    private readonly ILogger<JUnitReportGenerator> _logger;
    private readonly List<CapturedTestResult> _tests = [];

    // Parent chain for ALL TestNodeUpdateMessages (including Discovered / InProgress).
    // Keyed by the raw, untruncated TestNodeUid value so that long UIDs do not break
    // chain lookups after capture-time truncation. The engine uses this to reconstruct
    // the testpath of every test case in the report.
    private readonly Dictionary<string, TestResultCapture.ParentChainEntry> _parentChain = [];
    private readonly bool _isEnabled;

    private DateTimeOffset? _testStartTime;

    public JUnitReportGenerator(
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
        ILogger<JUnitReportGenerator> logger)
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
        _isEnabled = commandLineOptions.IsOptionSet(JUnitReportGeneratorCommandLine.JUnitReportOptionName);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid => nameof(JUnitReportGenerator);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.JUnitReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.JUnitReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is TestNodeUpdateMessage update)
        {
            // Record the parent chain entry for EVERY update so non-terminal parent
            // nodes (Discovered / InProgress) are still available when reconstructing
            // the path of a terminal child test. Later updates for the same UID just
            // refresh the entry (frameworks may emit several updates per node).
            // The raw UID is test-controlled and unbounded by the platform, so we
            // truncate it to a fixed identity budget before using it as a dictionary
            // key. Capture-side `RawUid`/`ParentRawUid` values are truncated to the
            // same budget so cross-lookups remain consistent.
            string rawUid = TestResultCapture.Truncate(update.TestNode.Uid.Value, TestResultCapture.MaxIdentityFieldLength)!;
            _parentChain[rawUid] = TestResultCapture.GetParentChainEntry(update);

            // Project to a capped DTO immediately so we don't retain the original
            // TestNode (and its potentially huge stdout/stderr/stack trace strings)
            // for the whole session. We never deduplicate on TestNode.Uid: some
            // frameworks emit several distinct results sharing the same UID
            // (parameterized rows, theory data, in-process retries, framework
            // misbehaviour). The engine surfaces all of them so no data is dropped.
            CapturedTestResult? captured = TestResultCapture.TryCapture(update);
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

        await _logger.LogTraceAsync($"Generating JUnit XML report for {_tests.Count} test result(s).").ConfigureAwait(false);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        var engine = new JUnitReportEngine(
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

        (string reportFileName, string? warning) = await engine.GenerateReportAsync([.. _tests], _parentChain).ConfigureAwait(false);

        if (warning is not null)
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
        }

        await _messageBus.PublishAsync(
            this,
            new SessionFileArtifact(
                testSessionContext.SessionUid,
                new FileInfo(reportFileName),
                ExtensionResources.JUnitReportArtifactDisplayName,
                ExtensionResources.JUnitReportArtifactDescription)).ConfigureAwait(false);
    }
}
