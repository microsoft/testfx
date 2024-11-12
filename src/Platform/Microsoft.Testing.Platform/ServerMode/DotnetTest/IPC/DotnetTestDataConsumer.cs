// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.IPC;

internal class DotnetTestDataConsumer : IPushOnlyProtocolConsumer
{
    private readonly DotnetTestConnection? _dotnetTestConnection;
    private readonly IEnvironment _environment;

    public DotnetTestDataConsumer(DotnetTestConnection dotnetTestConnection, IEnvironment environment)
    {
        _dotnetTestConnection = dotnetTestConnection;
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
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        switch (value)
        {
            case TestNodeUpdateMessage testNodeUpdateMessage:

                TestNodeDetails testNodeDetails = GetTestNodeDetails(testNodeUpdateMessage);

                switch (testNodeDetails.State)
                {
                    case TestStates.Discovered:
                        DiscoveredTestMessages discoveredTestMessages = new(
                            ExecutionId,
                            new[]
                            {
                                new DiscoveredTestMessage(
                                    testNodeUpdateMessage.TestNode.Uid.Value,
                                    testNodeUpdateMessage.TestNode.DisplayName),
                            });

                        await _dotnetTestConnection.SendMessageAsync(discoveredTestMessages);
                        break;

                    case TestStates.Passed:
                    case TestStates.Skipped:
                        TestResultMessages testResultMessages = new(
                            ExecutionId,
                            new[]
                            {
                                new SuccessfulTestResultMessage(
                                   testNodeUpdateMessage.TestNode.Uid.Value,
                                   testNodeUpdateMessage.TestNode.DisplayName,
                                   testNodeDetails.State,
                                   testNodeDetails.Duration,
                                   testNodeDetails.Reason ?? string.Empty,
                                   testNodeDetails.StandardOutput ?? string.Empty,
                                   testNodeDetails.StandardError ?? string.Empty,
                                   testNodeUpdateMessage.SessionUid.Value),
                            },
                            Array.Empty<FailedTestResultMessage>());

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages);
                        break;

                    case TestStates.Failed:
                    case TestStates.Error:
                    case TestStates.Timeout:
                    case TestStates.Cancelled:
                        testResultMessages = new(
                            ExecutionId,
                            Array.Empty<SuccessfulTestResultMessage>(),
                            new[]
                            {
                                new FailedTestResultMessage(
                                   testNodeUpdateMessage.TestNode.Uid.Value,
                                   testNodeUpdateMessage.TestNode.DisplayName,
                                   testNodeDetails.State,
                                   testNodeDetails.Duration,
                                   testNodeDetails.Reason ?? string.Empty,
                                   testNodeDetails.ErrorMessage ?? string.Empty,
                                   testNodeDetails.ErrorStackTrace ?? string.Empty,
                                   testNodeDetails.StandardOutput ?? string.Empty,
                                   testNodeDetails.StandardError ?? string.Empty,
                                   testNodeUpdateMessage.SessionUid.Value),
                            });

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages);
                        break;
                }

                break;

            case TestNodeFileArtifact testNodeFileArtifact:
                FileArtifactMessages fileArtifactMessages = new(
                    ExecutionId,
                    new[]
                    {
                        new FileArtifactMessage(
                            testNodeFileArtifact.FileInfo.FullName,
                            testNodeFileArtifact.DisplayName,
                            testNodeFileArtifact.Description ?? string.Empty,
                            testNodeFileArtifact.TestNode.Uid.Value,
                            testNodeFileArtifact.TestNode.DisplayName,
                            testNodeFileArtifact.SessionUid.Value),
                    });

                await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages);
                break;

            case SessionFileArtifact sessionFileArtifact:
                fileArtifactMessages = new(
                    ExecutionId,
                    new[]
                    {
                        new FileArtifactMessage(
                            sessionFileArtifact.FileInfo.FullName,
                            sessionFileArtifact.DisplayName,
                            sessionFileArtifact.Description ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            sessionFileArtifact.SessionUid.Value),
                    });

                await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages);
                break;

            case FileArtifact fileArtifact:
                fileArtifactMessages = new(
                    ExecutionId,
                    new[]
                    {
                        new FileArtifactMessage(
                            fileArtifact.FileInfo.FullName,
                            fileArtifact.DisplayName,
                            fileArtifact.Description ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty),
                    });

                await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages);
                break;
        }
    }

    private static TestNodeDetails GetTestNodeDetails(TestNodeUpdateMessage testNodeUpdateMessage)
    {
        byte? state = null;
        long? duration = null;
        string? reason = string.Empty;
        string? errorMessage = string.Empty;
        string? errorStackTrace = string.Empty;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();
        string? standardOutput = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
        string? standardError = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;

        switch (nodeState)
        {
            case DiscoveredTestNodeStateProperty:
                state = TestStates.Discovered;
                break;

            case PassedTestNodeStateProperty:
                state = TestStates.Passed;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                break;

            case SkippedTestNodeStateProperty:
                state = TestStates.Skipped;
                reason = nodeState.Explanation;
                break;

            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                state = TestStates.Failed;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                errorMessage = failedTestNodeStateProperty.Exception?.Message;
                errorStackTrace = failedTestNodeStateProperty.Exception?.StackTrace;
                break;

            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                state = TestStates.Error;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                errorMessage = errorTestNodeStateProperty.Exception?.Message;
                errorStackTrace = errorTestNodeStateProperty.Exception?.StackTrace;
                break;

            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                state = TestStates.Timeout;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                errorMessage = timeoutTestNodeStateProperty.Exception?.Message;
                errorStackTrace = timeoutTestNodeStateProperty.Exception?.StackTrace;
                break;

            case CancelledTestNodeStateProperty cancelledTestNodeStateProperty:
                state = TestStates.Cancelled;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                errorMessage = cancelledTestNodeStateProperty.Exception?.Message;
                errorStackTrace = cancelledTestNodeStateProperty.Exception?.StackTrace;
                break;
        }

        return new TestNodeDetails(state, duration, reason, errorMessage, errorStackTrace, standardOutput, standardError);
    }

    public record TestNodeDetails(byte? State, long? Duration, string? Reason, string? ErrorMessage, string? ErrorStackTrace, string? StandardOutput, string? StandardError);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            sessionUid.Value,
            ExecutionId);

        await _dotnetTestConnection.SendMessageAsync(sessionStartEvent);
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            sessionUid.Value,
            ExecutionId);

        await _dotnetTestConnection.SendMessageAsync(sessionEndEvent);
    }
}
