// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.IPC;

[UnsupportedOSPlatform("browser")]
internal sealed class DotnetTestDataConsumer : IPushOnlyProtocolConsumer
{
    private readonly DotnetTestConnection? _dotnetTestConnection;
    private readonly string? _executionId;

    public DotnetTestDataConsumer(DotnetTestConnection dotnetTestConnection, IEnvironment environment)
    {
        _dotnetTestConnection = dotnetTestConnection;
        _executionId = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
    }

    public Type[] DataTypesConsumed =>
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
    ];

    public string Uid => nameof(DotnetTestDataConsumer);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(DotnetTestDataConsumer);

    public string Description => "Send information back to the dotnet test";

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
                        // Only stream the full discovery details (file location, method identifier,
                        // traits) when the consumer asked for them. We reuse the existing IsIDE flag
                        // for that — despite the name, it is the handshake signal a consumer sets when
                        // it wants the complete discovery object (e.g. an IDE, or the SDK when running
                        // `dotnet test --list-tests json`). Plain runs keep the payload minimal.
                        TestFileLocationProperty? testFileLocationProperty = null;
                        TestMethodIdentifierProperty? testMethodIdentifierProperty = null;
                        TestMetadataProperty[] traits = [];
                        if (_dotnetTestConnection.IsIDE)
                        {
                            testFileLocationProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestFileLocationProperty>();
                            testMethodIdentifierProperty = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
                            traits = testNodeDetails.Traits;
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
                                    traits)
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
                                   testNodeUpdateMessage.SessionUid.Value)
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
                                   testNodeUpdateMessage.SessionUid.Value)
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
                                testNodeUpdateMessage.SessionUid.Value)
                        ]);

                    await _dotnetTestConnection.SendMessageAsync(testFileArtifactMessages).ConfigureAwait(false);
                }

                break;

            case SessionFileArtifact sessionFileArtifact:
                var fileArtifactMessages = new FileArtifactMessages(
                    _executionId,
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
                    _executionId,
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

        // This method already performs a single GetStructEnumerator() walk to collect the
        // StandardOutput, StandardError, and TimingProperty values. Folding FileArtifactProperty
        // and TestMetadataProperty collection into that same walk removes the two additional
        // PropertyBag.OfType<T>() linked-list traversals that ConsumeAsync used to perform for
        // every test node update.
        TimingProperty? timingProperty = null;
        StandardOutputProperty? standardOutputProperty = null;
        StandardErrorProperty? standardErrorProperty = null;

        // Mirror PropertyBag.OfType<T>()'s "first + overflow list" pattern so the common case of
        // zero or one match doesn't allocate a List<T>.
        FileArtifactProperty? firstArtifact = null;
        List<FileArtifactProperty>? artifactsOverflow = null;
        TestMetadataProperty? firstTrait = null;
        List<TestMetadataProperty>? traitsOverflow = null;
        PropertyBag.PropertyBagEnumerator enumerator = testNodeUpdateMessage.TestNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty timing:
                    timingProperty = GetSingleOrDefaultValue(timingProperty, timing);
                    break;
                case StandardOutputProperty outputProperty:
                    standardOutputProperty = GetSingleOrDefaultValue(standardOutputProperty, outputProperty);
                    break;
                case StandardErrorProperty errorProperty:
                    standardErrorProperty = GetSingleOrDefaultValue(standardErrorProperty, errorProperty);
                    break;
                case FileArtifactProperty artifact:
                    if (firstArtifact is null)
                    {
                        firstArtifact = artifact;
                    }
                    else
                    {
                        (artifactsOverflow ??= [firstArtifact]).Add(artifact);
                    }

                    break;
                case TestMetadataProperty trait:
                    if (firstTrait is null)
                    {
                        firstTrait = trait;
                    }
                    else
                    {
                        (traitsOverflow ??= [firstTrait]).Add(trait);
                    }

                    break;
            }
        }

        FileArtifactProperty[] artifacts = firstArtifact is null
            ? []
            : artifactsOverflow is not null ? [.. artifactsOverflow] : [firstArtifact];
        TestMetadataProperty[] traits = firstTrait is null
            ? []
            : traitsOverflow is not null ? [.. traitsOverflow] : [firstTrait];

        string? standardOutput = standardOutputProperty?.StandardOutput;
        string? standardError = standardErrorProperty?.StandardError;

        switch (nodeState)
        {
            case DiscoveredTestNodeStateProperty:
                state = TestStates.Discovered;
                break;

            case PassedTestNodeStateProperty:
                state = TestStates.Passed;
                duration = timingProperty?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                break;

            case SkippedTestNodeStateProperty:
                state = TestStates.Skipped;
                reason = nodeState.Explanation;
                break;

            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                state = TestStates.Failed;
                duration = timingProperty?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, failedTestNodeStateProperty.Exception);
                break;

            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                state = TestStates.Error;
                duration = timingProperty?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, errorTestNodeStateProperty.Exception);
                break;

            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                state = TestStates.Timeout;
                duration = timingProperty?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, timeoutTestNodeStateProperty.Exception);
                break;

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            case CancelledTestNodeStateProperty cancelledTestNodeStateProperty:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                state = TestStates.Cancelled;
                duration = timingProperty?.GlobalTiming.Duration.Ticks;
                reason = nodeState.Explanation;
                exceptions = FlattenToExceptionMessages(reason, cancelledTestNodeStateProperty.Exception);
                break;

            case InProgressTestNodeStateProperty:
                state = TestStates.InProgress;
                break;
        }

        return new TestNodeDetails(state, duration, reason, exceptions, standardOutput, standardError, artifacts, traits);

        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
            where TProperty : IProperty
            => existingProperty is not null
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : property;

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

    public sealed record TestNodeDetails(byte? State, long? Duration, string? Reason, ExceptionMessage[]? Exceptions, string? StandardOutput, string? StandardError, FileArtifactProperty[] Artifacts, TestMetadataProperty[] Traits);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionStartEvent = new(
            SessionEventTypes.TestSessionStart,
            testSessionContext.SessionUid.Value,
            _executionId);

        await _dotnetTestConnection.SendMessageAsync(sessionStartEvent).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        RoslynDebug.Assert(_dotnetTestConnection is not null);

        TestSessionEvent sessionEndEvent = new(
            SessionEventTypes.TestSessionEnd,
            testSessionContext.SessionUid.Value,
            _executionId);

        await _dotnetTestConnection.SendMessageAsync(sessionEndEvent).ConfigureAwait(false);
    }
}
