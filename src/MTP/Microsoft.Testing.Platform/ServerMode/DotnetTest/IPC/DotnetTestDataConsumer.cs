// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class DotnetTestDataConsumer : IPushOnlyProtocolConsumer
{
    internal const string DeferredTrxRootEnvironmentVariable = "TESTINGPLATFORM_RETRY_DEFERRED_TRX_ROOT";
    internal const string DeferredTrxDirectoryEnvironmentVariable = "TESTINGPLATFORM_RETRY_DEFERRED_TRX_DIRECTORY";
    internal const string ArtifactReplayPipeEnvironmentVariable = "TESTINGPLATFORM_DOTNETTEST_ARTIFACT_REPLAY_PIPE";

    private readonly DotnetTestConnection _dotnetTestConnection;
    private readonly string? _executionId;
    private readonly string? _deferredTrxRoot;
    private readonly string? _deferredTrxDirectory;

    public DotnetTestDataConsumer(DotnetTestConnection dotnetTestConnection, IEnvironment environment)
    {
        _dotnetTestConnection = dotnetTestConnection;
        _executionId = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
        _deferredTrxRoot = environment.GetEnvironmentVariable(DeferredTrxRootEnvironmentVariable);
        _deferredTrxDirectory = environment.GetEnvironmentVariable(DeferredTrxDirectoryEnvironmentVariable);
    }

    public Type[] DataTypesConsumed =>
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
        typeof(TestCoverageMessage),
    ];

    public string Uid => nameof(DotnetTestDataConsumer);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(DotnetTestDataConsumer);

    public string Description => "Send information back to the dotnet test";

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case TestNodeUpdateMessage testNodeUpdateMessage:

                TestNodeDetails? testNodeDetails = GetTestNodeDetails(testNodeUpdateMessage);
                if (testNodeDetails is null)
                {
                    return;
                }

                switch (testNodeDetails.State)
                {
                    case TestStates.Discovered:
                        // Only stream the full discovery details (file location, method identifier,
                        // traits) when the consumer asked for them. We reuse the existing IsIDE flag
                        // for that — despite the name, it is the handshake signal a consumer sets when
                        // it wants the complete discovery object (e.g. an IDE, or the SDK when running
                        // `dotnet test --list-tests json`). Plain runs keep the payload minimal.
                        TestFileLocationProperty? testFileLocationProperty = null;
                        TestMethodIdentifierProperty? testMethodIdentifierProperty = null;
                        TraitMessage[] traits = [];
                        if (_dotnetTestConnection.IsIDE)
                        {
                            testFileLocationProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestFileLocationProperty>();
                            testMethodIdentifierProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
                            // Convert the platform's TestMetadataProperty to the shared, platform-decoupled wire type.
                            traits = [.. testNodeDetails.Traits.Select(static t => new TraitMessage(t.Key, t.Value))];
                        }

                        DiscoveredTestMessages discoveredTestMessages = new(
                            _executionId,
                            DotnetTestConnection.InstanceId,
                            [
                                new DiscoveredTestMessage(
                                    testNodeUpdateMessage.TestNode.Uid.Value,
                                    testNodeUpdateMessage.TestNode.DisplayName,
                                    testFileLocationProperty?.FilePath,
                                    testFileLocationProperty?.LineSpan.Start.Line,
                                    testMethodIdentifierProperty?.Namespace,
                                    testMethodIdentifierProperty?.TypeName,
                                    testMethodIdentifierProperty?.MethodName,
                                    testMethodIdentifierProperty?.ParameterTypeFullNames,
                                    traits),
                            ]);

                        await _dotnetTestConnection.SendMessageAsync(discoveredTestMessages).ConfigureAwait(false);
                        break;

                    case TestStates.Passed:
                    case TestStates.Skipped:
                    case TestStates.InProgress when _dotnetTestConnection.IsIDE:
                        TestResultMessages testResultMessages = new(
                            _executionId,
                            DotnetTestConnection.InstanceId,
                            [
                                new SuccessfulTestResultMessage(
                                   testNodeUpdateMessage.TestNode.Uid.Value,
                                   testNodeUpdateMessage.TestNode.DisplayName,
                                   testNodeDetails.State,
                                   testNodeDetails.Duration,
                                   testNodeDetails.Reason ?? string.Empty,
                                   testNodeDetails.StandardOutput ?? string.Empty,
                                   testNodeDetails.StandardError ?? string.Empty,
                                   testNodeUpdateMessage.SessionUid.Value,
                                   testNodeDetails.RetryAttemptNumber,
                                   testNodeDetails.IsSuperseded),
                            ],
                            []);

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages).ConfigureAwait(false);
                        break;

                    case TestStates.InProgress:
                        // Non-IDE consumers (e.g. `dotnet test` with MTP) render in-progress events as a
                        // separate "currently running tests" panel; they don't expect them as TestResultMessages.
                        TestInProgressMessages inProgressMessages = new(
                            _executionId,
                            DotnetTestConnection.InstanceId,
                            [
                                new TestInProgressMessage(
                                    testNodeUpdateMessage.TestNode.Uid.Value,
                                    testNodeUpdateMessage.TestNode.DisplayName),
                            ]);

                        await _dotnetTestConnection.SendMessageAsync(inProgressMessages).ConfigureAwait(false);
                        break;

                    case TestStates.Failed:
                    case TestStates.Error:
                    case TestStates.Timeout:
                    case TestStates.Cancelled:
                        testResultMessages = new(
                            _executionId,
                            DotnetTestConnection.InstanceId,
                            [],
                            [
                                new FailedTestResultMessage(
                                   testNodeUpdateMessage.TestNode.Uid.Value,
                                   testNodeUpdateMessage.TestNode.DisplayName,
                                   testNodeDetails.State,
                                   testNodeDetails.Duration,
                                   testNodeDetails.Reason ?? string.Empty,
                                   testNodeDetails.Exceptions,
                                   testNodeDetails.StandardOutput ?? string.Empty,
                                   testNodeDetails.StandardError ?? string.Empty,
                                   testNodeUpdateMessage.SessionUid.Value,
                                   testNodeDetails.Expected,
                                   testNodeDetails.Actual,
                                   testNodeDetails.RetryAttemptNumber,
                                   testNodeDetails.IsSuperseded),
                            ]);

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages).ConfigureAwait(false);
                        break;
                }

                foreach (FileArtifactProperty artifact in testNodeDetails.Artifacts)
                {
                    FileArtifactMessages testFileArtifactMessages = new(
                        _executionId,
                        DotnetTestConnection.InstanceId,
                        [
                            new FileArtifactMessage(
                                artifact.FileInfo.FullName,
                                artifact.DisplayName,
                                artifact.Description ?? string.Empty,
                                testNodeUpdateMessage.TestNode.Uid.Value,
                                testNodeUpdateMessage.TestNode.DisplayName,
                                testNodeUpdateMessage.SessionUid.Value,
                                null),
                        ]);

                    if (!TryDeferTrxArtifacts(testFileArtifactMessages, _deferredTrxRoot, _deferredTrxDirectory))
                    {
                        await _dotnetTestConnection.SendMessageAsync(testFileArtifactMessages).ConfigureAwait(false);
                    }
                }

                break;

            case SessionFileArtifact sessionFileArtifact:
                FileArtifactMessages fileArtifactMessages = CreateFileArtifactMessages(_executionId, sessionFileArtifact);

                if (!TryDeferTrxArtifacts(fileArtifactMessages, _deferredTrxRoot, _deferredTrxDirectory))
                {
                    await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages).ConfigureAwait(false);
                }

                break;

            case FileArtifact fileArtifact:
                fileArtifactMessages = CreateFileArtifactMessages(_executionId, fileArtifact);

                if (!TryDeferTrxArtifacts(fileArtifactMessages, _deferredTrxRoot, _deferredTrxDirectory))
                {
                    await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages).ConfigureAwait(false);
                }

                break;

            case TestCoverageMessage coverage when _dotnetTestConnection.IsCoverageSummarySupported
                && coverage.Scope.Level is CoverageScopeLevel.Overall or CoverageScopeLevel.Module:
                await _dotnetTestConnection.SendMessageAsync(new CoverageSummaryMessages(
                    _executionId,
                    DotnetTestConnection.InstanceId,
                    [
                        new CoverageSummaryMessage(
                            coverage.SessionUid.Value,
                            coverage.ProducerId,
                            (int)coverage.Scope.Level,
                            coverage.Scope.Name,
                            (int)coverage.Metric,
                            coverage.CustomMetricName,
                            coverage.CoveredCount,
                            coverage.CoverableCount),
                    ])).ConfigureAwait(false);
                break;
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            testSessionContext.SessionUid.Value,
            _executionId);

        await _dotnetTestConnection.SendMessageAsync(sessionStartEvent).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            testSessionContext.SessionUid.Value,
            _executionId);

        await _dotnetTestConnection.SendMessageAsync(sessionEndEvent).ConfigureAwait(false);
    }
}
