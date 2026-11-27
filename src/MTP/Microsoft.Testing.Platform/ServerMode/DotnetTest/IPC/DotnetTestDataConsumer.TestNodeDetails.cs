// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class DotnetTestDataConsumer
{
    private static TestNodeDetails? GetTestNodeDetails(TestNodeUpdateMessage testNodeUpdateMessage)
    {
        byte? state = null;
        long? duration = null;
        string? reason = string.Empty;
        string? expected = null;
        string? actual = null;
        ExceptionMessage[]? exceptions = null;
        TestNodeStateProperty? nodeState = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        bool executionCompleted = testNodeUpdateMessage.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>();
        if (nodeState is null && !executionCompleted)
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
        AssertionFailureProperty? assertionFailureProperty = null;
        RetryAttemptProperty? retryAttemptProperty = null;

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
                case AssertionFailureProperty assertionFailure:
                    assertionFailureProperty = GetSingleOrDefaultValue(assertionFailureProperty, assertionFailure);
                    break;
                case RetryAttemptProperty retryAttempt:
                    retryAttemptProperty = GetSingleOrDefaultValue(retryAttemptProperty, retryAttempt);
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

        // RetryAttemptProperty is a sibling of the outcome state property (see RetryAttemptPropertyExtensions'
        // remarks), so it is extracted unconditionally here rather than per-state below - it can accompany a
        // passed, failed, or any other terminal outcome.
        int? retryAttemptNumber = retryAttemptProperty?.AttemptNumber;
        bool? isSuperseded = retryAttemptProperty?.IsSuperseded;

        if (executionCompleted)
        {
            // The pipe protocol has no outcome-less terminal state. Return the test to discovered so
            // clients clear in-progress state without recording a pass, failure, or skip.
            state = TestStates.Discovered;
        }
        else
        {
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

                    // Mirror TerminalOutputDevice's single-assembly rendering so the SDK can show an
                    // expected-vs-actual diff. AssertionFailureProperty is the supported channel;
                    // Exception.Data is the legacy fallback for producers that have not been updated yet. The
                    // choice is all-or-nothing so the two halves of a diff always come from the same producer.
                    // Only failed tests carry these (error/timeout pass null, as in single-assembly).
                    expected = assertionFailureProperty is not null ? assertionFailureProperty.Expected : failedTestNodeStateProperty.Exception?.Data["assert.expected"] as string;
                    actual = assertionFailureProperty is not null ? assertionFailureProperty.Actual : failedTestNodeStateProperty.Exception?.Data["assert.actual"] as string;
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

                case InProgressTestNodeStateProperty:
                    state = TestStates.InProgress;
                    break;
            }
        }

        return new TestNodeDetails(state, duration, reason, exceptions, standardOutput, standardError, artifacts, traits, expected, actual, retryAttemptNumber, isSuperseded);

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

    public sealed record TestNodeDetails(byte? State, long? Duration, string? Reason, ExceptionMessage[]? Exceptions, string? StandardOutput, string? StandardError, FileArtifactProperty[] Artifacts, TestMetadataProperty[] Traits, string? Expected, string? Actual, int? RetryAttemptNumber, bool? IsSuperseded);
}
