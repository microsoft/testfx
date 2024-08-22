// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.IPC;

internal class DotnetTestDataConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly NamedPipeClient _dotnetTestPipeClient;
    private readonly IEnvironment _environment;

    public DotnetTestDataConsumer(NamedPipeClient dotnetTestPipeClient, IEnvironment environment)
    {
        _dotnetTestPipeClient = dotnetTestPipeClient;
        _environment = environment;
    }

    public Type[] DataTypesConsumed => new[]
    {
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
        typeof(TestRequestExecutionTimeInfo),
    };

    public string Uid => nameof(DotnetTestDataConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(DotnetTestDataConsumer);

    public string Description => "Send back information to the dotnet test";

    private string? ExecutionId => _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case TestNodeUpdateMessage testNodeUpdateMessage:

                GetTestNodeDetails(testNodeUpdateMessage, out byte? state, out string? reason, out string? errorMessage, out string? errorStackTrace);

                switch (state)
                {
                    case TestStates.Passed:
                    case TestStates.Skipped:
                        SuccessfulTestResultMessage successfulTestResultMessage = new(
                           testNodeUpdateMessage.TestNode.Uid.Value,
                           testNodeUpdateMessage.TestNode.DisplayName,
                           state,
                           reason ?? string.Empty,
                           testNodeUpdateMessage.SessionUid.Value,
                           ExecutionId);

                        await _dotnetTestPipeClient.RequestReplyAsync<SuccessfulTestResultMessage, VoidResponse>(successfulTestResultMessage, cancellationToken);
                        break;

                    case TestStates.Failed:
                        FailedTestResultMessage testResultMessage = new(
                           testNodeUpdateMessage.TestNode.Uid.Value,
                           testNodeUpdateMessage.TestNode.DisplayName,
                           state,
                           reason ?? string.Empty,
                           errorMessage ?? string.Empty,
                           errorStackTrace ?? string.Empty,
                           testNodeUpdateMessage.SessionUid.Value,
                           ExecutionId);

                        await _dotnetTestPipeClient.RequestReplyAsync<FailedTestResultMessage, VoidResponse>(testResultMessage, cancellationToken);
                        break;
                }

                break;

            case TestNodeFileArtifact testNodeFileArtifact:
                FileArtifactInfo fileArtifactInfo = new(
                    testNodeFileArtifact.FileInfo.FullName,
                    testNodeFileArtifact.DisplayName,
                    testNodeFileArtifact.Description ?? string.Empty,
                    testNodeFileArtifact.TestNode.Uid.Value,
                    testNodeFileArtifact.TestNode.DisplayName,
                    testNodeFileArtifact.SessionUid.Value,
                    ExecutionId);

                await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
                break;

            case SessionFileArtifact sessionFileArtifact:
                fileArtifactInfo = new(
                    sessionFileArtifact.FileInfo.FullName,
                    sessionFileArtifact.DisplayName,
                    sessionFileArtifact.Description ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    sessionFileArtifact.SessionUid.Value,
                    ExecutionId);

                await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
                break;

            case FileArtifact fileArtifact:
                fileArtifactInfo = new(
                   fileArtifact.FileInfo.FullName,
                   fileArtifact.DisplayName,
                   fileArtifact.Description ?? string.Empty,
                   string.Empty,
                   string.Empty,
                   string.Empty,
                   ExecutionId);

                await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
                break;
        }

        await Task.CompletedTask;
    }

    private static void GetTestNodeDetails(TestNodeUpdateMessage testNodeUpdateMessage, out byte? state, out string? reason, out string? errorMessage, out string? errorStackTrace)
    {
        state = null;
        reason = string.Empty;
        errorMessage = string.Empty;
        errorStackTrace = string.Empty;
        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case PassedTestNodeStateProperty:
                state = TestStates.Passed;
                reason = nodeState.Explanation;
                break;

            case SkippedTestNodeStateProperty:
                state = TestStates.Skipped;
                reason = nodeState.Explanation;
                break;

            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                state = TestStates.Failed;
                reason = nodeState.Explanation;
                errorMessage = failedTestNodeStateProperty.Exception?.Message;
                errorStackTrace = failedTestNodeStateProperty.Exception?.StackTrace;
                break;

            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                state = TestStates.Error;
                reason = nodeState.Explanation;
                errorMessage = errorTestNodeStateProperty.Exception?.Message;
                errorStackTrace = errorTestNodeStateProperty.Exception?.StackTrace;
                break;

            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                state = TestStates.Timeout;
                reason = nodeState.Explanation;
                errorMessage = timeoutTestNodeStateProperty.Exception?.Message;
                errorStackTrace = timeoutTestNodeStateProperty.Exception?.StackTrace;
                break;

            case CancelledTestNodeStateProperty cancelledTestNodeStateProperty:
                state = TestStates.Cancelled;
                reason = nodeState.Explanation;
                errorMessage = cancelledTestNodeStateProperty.Exception?.Message;
                errorStackTrace = cancelledTestNodeStateProperty.Exception?.StackTrace;
                break;
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            sessionUid.Value,
            ExecutionId);

        await _dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(sessionStartEvent, cancellationToken);
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            sessionUid.Value,
            ExecutionId);

        await _dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(sessionEndEvent, cancellationToken);
    }
}
