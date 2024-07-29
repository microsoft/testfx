// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.IPC;

internal class DotnetTestDataConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly NamedPipeClient _dotnetTestPipeClient;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;

    public DotnetTestDataConsumer(NamedPipeClient dotnetTestPipeClient, ITestApplicationModuleInfo testApplicationModuleInfo)
    {
        _dotnetTestPipeClient = dotnetTestPipeClient;
        _testApplicationModuleInfo = testApplicationModuleInfo;
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

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is TestNodeUpdateMessage testNodeUpdateMessage)
        {
            string state = string.Empty;
            string? reason = string.Empty;
            string? errorMessage = string.Empty;
            string? errorStackTrace = string.Empty;

            TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

            if (nodeState is PassedTestNodeStateProperty)
            {
                state = TestStates.Passed;
                reason = nodeState.Explanation;
            }
            else if (nodeState is SkippedTestNodeStateProperty)
            {
                state = TestStates.Skipped;
                reason = nodeState.Explanation;
            }
            else if (nodeState is FailedTestNodeStateProperty failedTestNodeStateProperty)
            {
                state = TestStates.Failed;
                reason = nodeState.Explanation;
                errorMessage = failedTestNodeStateProperty.Exception?.Message;
                errorStackTrace = failedTestNodeStateProperty.Exception?.StackTrace;
            }
            else if (nodeState is ErrorTestNodeStateProperty errorTestNodeStateProperty)
            {
                state = TestStates.Error;
                reason = nodeState.Explanation;
                errorMessage = errorTestNodeStateProperty.Exception?.Message;
                errorStackTrace = errorTestNodeStateProperty.Exception?.StackTrace;
            }
            else if (nodeState is TimeoutTestNodeStateProperty timeoutTestNodeStateProperty)
            {
                state = TestStates.Timeout;
                reason = nodeState.Explanation;
                errorMessage = timeoutTestNodeStateProperty.Exception?.Message;
                errorStackTrace = timeoutTestNodeStateProperty.Exception?.StackTrace;
            }
            else if (nodeState is CancelledTestNodeStateProperty cancelledTestNodeStateProperty)
            {
                state = TestStates.Cancelled;
                reason = nodeState.Explanation;
                errorMessage = cancelledTestNodeStateProperty.Exception?.Message;
                errorStackTrace = cancelledTestNodeStateProperty.Exception?.StackTrace;
            }

            if (state is TestStates.Passed or TestStates.Skipped)
            {
                SuccessfulTestResultMessage successfulTestResultMessage = new(
                    testNodeUpdateMessage.TestNode.Uid.Value,
                    testNodeUpdateMessage.TestNode.DisplayName,
                    state,
                    reason ?? string.Empty,
                    testNodeUpdateMessage.SessionUid.Value,
                    _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

                await _dotnetTestPipeClient.RequestReplyAsync<SuccessfulTestResultMessage, VoidResponse>(successfulTestResultMessage, cancellationToken);
            }
            else
            {
                FailedTestResultMessage testResultMessage = new(
                    testNodeUpdateMessage.TestNode.Uid.Value,
                    testNodeUpdateMessage.TestNode.DisplayName,
                    state,
                    reason ?? string.Empty,
                    errorMessage ?? string.Empty,
                    errorStackTrace ?? string.Empty,
                    testNodeUpdateMessage.SessionUid.Value,
                    _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

                await _dotnetTestPipeClient.RequestReplyAsync<FailedTestResultMessage, VoidResponse>(testResultMessage, cancellationToken);
            }
        }
        else if (value is TestNodeFileArtifact testNodeFileArtifact)
        {
            FileArtifactInfo fileArtifactInfo = new(
                testNodeFileArtifact.FileInfo.FullName,
                testNodeFileArtifact.DisplayName,
                testNodeFileArtifact.Description ?? string.Empty,
                testNodeFileArtifact.TestNode.Uid.Value,
                testNodeFileArtifact.TestNode.DisplayName,
                testNodeFileArtifact.SessionUid.Value,
                _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
        }
        else if (value is SessionFileArtifact sessionFileArtifact)
        {
            FileArtifactInfo fileArtifactInfo = new(
                sessionFileArtifact.FileInfo.FullName,
                sessionFileArtifact.DisplayName,
                sessionFileArtifact.Description ?? string.Empty,
                string.Empty,
                string.Empty,
                sessionFileArtifact.SessionUid.Value,
                _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
        }
        else if (value is FileArtifact fileArtifact)
        {
            FileArtifactInfo fileArtifactInfo = new(
                fileArtifact.FileInfo.FullName,
                fileArtifact.DisplayName,
                fileArtifact.Description ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, cancellationToken);
        }

        await Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            sessionUid.Value,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

        await _dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(sessionStartEvent, cancellationToken);
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            sessionUid.Value,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

        await _dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(sessionEndEvent, cancellationToken);
    }
}
