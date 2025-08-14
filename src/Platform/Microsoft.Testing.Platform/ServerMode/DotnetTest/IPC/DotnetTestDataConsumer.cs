// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.IPC;

internal sealed class DotnetTestDataConsumer : IPushOnlyProtocolConsumer
{
    private readonly DotnetTestConnection? _dotnetTestConnection;
    private readonly IEnvironment _environment;

    public DotnetTestDataConsumer(DotnetTestConnection dotnetTestConnection, IEnvironment environment)
    {
        _dotnetTestConnection = dotnetTestConnection;
        _environment = environment;
    }

    public Type[] DataTypesConsumed =>
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
        typeof(TestRequestExecutionTimeInfo)
    ];

    public string Uid => nameof(DotnetTestDataConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(DotnetTestDataConsumer);

    public string Description => "Send information back to the dotnet test";

    private string? ExecutionId => _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

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
                        TestFileLocationProperty? testFileLocationProperty = null;
                        TestMethodIdentifierProperty? testMethodIdentifierProperty = null;
                        TestMetadataProperty[] traits = [];
                        if (_dotnetTestConnection.IsIDE)
                        {
                            testFileLocationProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestFileLocationProperty>();
                            testMethodIdentifierProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
                            traits = testNodeUpdateMessage.TestNode.Properties.OfType<TestMetadataProperty>();
                        }

                        DiscoveredTestMessages discoveredTestMessages = new(
                            ExecutionId,
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
                                    traits)
                            ]);

                        await _dotnetTestConnection.SendMessageAsync(discoveredTestMessages).ConfigureAwait(false);
                        break;

                    case TestStates.Passed:
                    case TestStates.Skipped:
                    case TestStates.InProgress when _dotnetTestConnection.IsIDE:
                        TestResultMessages testResultMessages = new(
                            ExecutionId,
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
                                   testNodeUpdateMessage.SessionUid.Value)
                            ],
                            []);

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages).ConfigureAwait(false);
                        break;

                    case TestStates.Failed:
                    case TestStates.Error:
                    case TestStates.Timeout:
                    case TestStates.Cancelled:
                        testResultMessages = new(
                            ExecutionId,
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
                                   testNodeUpdateMessage.SessionUid.Value)
                            ]);

                        await _dotnetTestConnection.SendMessageAsync(testResultMessages).ConfigureAwait(false);
                        break;
                }

                foreach (FileArtifactProperty artifact in testNodeUpdateMessage.TestNode.Properties.OfType<FileArtifactProperty>())
                {
                    FileArtifactMessages testFileArtifactMessages = new(
                        ExecutionId,
                        DotnetTestConnection.InstanceId,
                        [
                            new FileArtifactMessage(
                                artifact.FileInfo.FullName,
                                artifact.DisplayName,
                                artifact.Description ?? string.Empty,
                                testNodeUpdateMessage.TestNode.Uid.Value,
                                testNodeUpdateMessage.TestNode.DisplayName,
                                testNodeUpdateMessage.SessionUid.Value)
                        ]);

                    await _dotnetTestConnection.SendMessageAsync(testFileArtifactMessages).ConfigureAwait(false);
                }

                break;

            case SessionFileArtifact sessionFileArtifact:
                var fileArtifactMessages = new FileArtifactMessages(
                    ExecutionId,
                    DotnetTestConnection.InstanceId,
                    [
                        new FileArtifactMessage(
                            sessionFileArtifact.FileInfo.FullName,
                            sessionFileArtifact.DisplayName,
                            sessionFileArtifact.Description ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            sessionFileArtifact.SessionUid.Value)
                    ]);

                await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages).ConfigureAwait(false);
                break;

            case FileArtifact fileArtifact:
                fileArtifactMessages = new(
                    ExecutionId,
                    DotnetTestConnection.InstanceId,
                    [
                        new FileArtifactMessage(
                            fileArtifact.FileInfo.FullName,
                            fileArtifact.DisplayName,
                            fileArtifact.Description ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty)
                    ]);

                await _dotnetTestConnection.SendMessageAsync(fileArtifactMessages).ConfigureAwait(false);
                break;
        }
    }

    private static TestNodeDetails? GetTestNodeDetails(TestNodeUpdateMessage testNodeUpdateMessage)
    {
        byte? state = null;
        long? duration = null;
        string? reason = string.Empty;
        ExceptionMessage[]? exceptions = null;
        TestNodeStateProperty? nodeState = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (nodeState is null)
        {
            return null;
        }

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
                exceptions = FlattenToExceptionMessages(reason, failedTestNodeStateProperty.Exception);
                break;

            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                state = TestStates.Error;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, errorTestNodeStateProperty.Exception);
                break;

            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                state = TestStates.Timeout;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, timeoutTestNodeStateProperty.Exception);
                break;

            case CancelledTestNodeStateProperty cancelledTestNodeStateProperty:
                state = TestStates.Cancelled;
                duration = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, cancelledTestNodeStateProperty.Exception);
                break;

            case InProgressTestNodeStateProperty:
                state = TestStates.InProgress;
                break;
        }

        return new TestNodeDetails(state, duration, reason, exceptions, standardOutput, standardError);

        static ExceptionMessage[]? FlattenToExceptionMessages(string? errorMessage, Exception? exception)
        {
            if (errorMessage is null && exception is null)
            {
                return null;
            }

            FlatException[] exceptions = ExceptionFlattener.Flatten(errorMessage, exception);

            var exceptionMessages = new ExceptionMessage[exceptions.Length];
            for (int i = 0; i < exceptions.Length; i++)
            {
                exceptionMessages[i] = new ExceptionMessage(exceptions[i].ErrorMessage, exceptions[i].ErrorType, exceptions[i].StackTrace);
            }

            return exceptionMessages;
        }
    }

    public sealed record TestNodeDetails(byte? State, long? Duration, string? Reason, ExceptionMessage[]? Exceptions, string? StandardOutput, string? StandardError);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            testSessionContext.SessionUid.Value,
            ExecutionId);

        await _dotnetTestConnection.SendMessageAsync(sessionStartEvent).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            testSessionContext.SessionUid.Value,
            ExecutionId);

        await _dotnetTestConnection.SendMessageAsync(sessionEndEvent).ConfigureAwait(false);
    }
}
