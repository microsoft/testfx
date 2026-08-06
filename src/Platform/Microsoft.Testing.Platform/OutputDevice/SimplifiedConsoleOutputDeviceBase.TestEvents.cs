// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Consumption and display of test node updates and output device messages.
/// </summary>
internal abstract partial class SimplifiedConsoleOutputDeviceBase
{
    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
    ];

    /// <summary>
    /// Displays provided data through IConsole, which is typically System.Console.
    /// </summary>
    /// <param name="producer">The producer that sent the data.</param>
    /// <param name="data">The data to be displayed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            switch (data)
            {
                case SessionMessageOutputDeviceData sessionMessageData:
                    ConsoleLog(sessionMessageData.Message);
                    break;

                case ProgressMessageOutputDeviceData progressMessageData:
                    var identity = new ProgressMessageIdentity(producer.Uid, progressMessageData.Key);
                    if (progressMessageData.Message is null)
                    {
                        _progressMessages.Remove(identity);
                    }
                    else if (!_progressMessages.TryGetValue(identity, out string? existingMessage)
                        || existingMessage != progressMessageData.Message)
                    {
                        ConsoleLog(progressMessageData.Message);
                        _progressMessages[identity] = progressMessageData.Message;
                    }

                    break;

                case FormattedTextOutputDeviceData formattedTextData:
                    ConsoleLog(formattedTextData.Text);
                    break;

                case TextOutputDeviceData textData:
                    ConsoleLog(textData.Text);
                    break;

                case WarningMessageOutputDeviceData warningData:
                    ConsoleWarn(warningData.Message);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    ConsoleError(errorData.Message);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    ConsoleError(exceptionOutputDeviceData.Exception.ToString());
                    break;
            }
        }
    }

    private readonly record struct ProgressMessageIdentity(string ProducerUid, string Key);

    private void OnFailedTest(TestNodeUpdateMessage testNodeStateChanged, TestNodeStateProperty state, Exception? exception, TimeSpan? duration)
    {
        _failedTests++;
        var builder = new StringBuilder();
        builder.Append("failed ");
        builder.Append(testNodeStateChanged.TestNode.DisplayName);

        if (duration.HasValue)
        {
            builder.Append(' ');
            HumanReadableDurationFormatter.Append(builder, static (builder, s) => builder!.Append(s), duration.Value);
        }

        if (state.Explanation is not null)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(state.Explanation);
        }

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(exception.ToString());
        }

        ConsoleError(builder.ToString());
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                // Single-pass collection: replaces SingleOrDefault<TimingProperty>() + SingleOrDefault<TestNodeStateProperty>()
                // with one zero-allocation GetStructEnumerator() walk. Note: TestNodeStateProperty was already O(1) via direct
                // field access, so the win here is code-path simplification and consistency with the established single-pass pattern.
                TimingProperty? timingProp = null;
                TestNodeStateProperty? nodeStateProp = null;
                List<FileArtifactProperty>? fileArtifacts = null;
                bool executionCompleted = false;
                PropertyBag.PropertyBagEnumerator enumerator = testNodeStateChanged.TestNode.Properties.GetStructEnumerator();
                while (enumerator.MoveNext())
                {
                    switch (enumerator.Current)
                    {
                        case TimingProperty t: timingProp = t; break;
                        case TestNodeStateProperty s: nodeStateProp = s; break;
                        case FileArtifactProperty f: (fileArtifacts ??= []).Add(f); break;
                        case TestNodeExecutionCompletedProperty: executionCompleted = true; break;
                    }
                }

                TimeSpan? duration = timingProp?.GlobalTiming.Duration;
                bool testCompleted = executionCompleted;

                // A test framework that retries in-process reports every attempt under the same test node uid. A
                // superseded attempt is not the test's outcome, so it must not be counted or printed as a failure -
                // a fail-then-pass [Retry] test would otherwise show a failed summary here.
                bool isSupersededAttempt = testNodeStateChanged.TestNode.IsSupersededRetryAttempt();

                if (nodeStateProp is InProgressTestNodeStateProperty)
                {
                    _activeTestTracker.Start(testNodeStateChanged.TestNode.Uid, testNodeStateChanged.TestNode.DisplayName);
                }
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                else if ((executionCompleted
                    || nodeStateProp is PassedTestNodeStateProperty or ErrorTestNodeStateProperty or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                    or FailedTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty)
                    // A retry sequence has one in-progress update for the whole test but a terminal update per
                    // attempt, so completing the tracker on a superseded one would stop tracking the test while
                    // its next attempt is still running. Mirrors HangDumpActivityIndicator.
                    && !isSupersededAttempt)
                {
                    _activeTestTracker.Complete(testNodeStateChanged.TestNode.Uid);
                }

                switch (isSupersededAttempt ? null : nodeStateProp)
                {
                    case InProgressTestNodeStateProperty:
                        if (DisplayActiveTestProgress)
                        {
                            await DisplayAsync(
                                this,
                                new ProgressMessageOutputDeviceData(
                                    testNodeStateChanged.TestNode.Uid.Value,
                                    $"running {testNodeStateChanged.TestNode.DisplayName}"),
                                cancellationToken).ConfigureAwait(false);
                        }

                        break;

                    case ErrorTestNodeStateProperty errorState:
                        OnFailedTest(testNodeStateChanged, errorState, errorState.Exception, duration);
                        testCompleted = true;
                        break;

                    case FailedTestNodeStateProperty failedState:
                        OnFailedTest(testNodeStateChanged, failedState, failedState.Exception, duration);
                        testCompleted = true;
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        OnFailedTest(testNodeStateChanged, timeoutState, timeoutState.Exception, duration);
                        testCompleted = true;
                        break;

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                    case CancelledTestNodeStateProperty cancelledState:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                        OnFailedTest(testNodeStateChanged, cancelledState, cancelledState.Exception, duration);
                        testCompleted = true;
                        break;

                    case PassedTestNodeStateProperty:
                        _passedTests++;
                        testCompleted = true;
                        break;

                    case SkippedTestNodeStateProperty:
                        _skippedTests++;
                        testCompleted = true;
                        break;
                }

                // A superseded attempt leaves the test tracked (its next attempt is still running), so its
                // "running" progress message must stay too - only the final attempt clears it.
                if (testCompleted && DisplayActiveTestProgress)
                {
                    await DisplayAsync(
                        this,
                        new ProgressMessageOutputDeviceData(testNodeStateChanged.TestNode.Uid.Value, message: null),
                        cancellationToken).ConfigureAwait(false);
                }

                if (fileArtifacts is not null)
                {
                    foreach (FileArtifactProperty artifact in fileArtifacts)
                    {
                        await DisplayArtifactAsync(artifact.FileInfo, artifact.DisplayName, cancellationToken).ConfigureAwait(false);
                    }
                }

                break;

            case SessionFileArtifact sessionFileArtifact:
                await DisplayArtifactAsync(sessionFileArtifact.FileInfo, sessionFileArtifact.DisplayName, cancellationToken).ConfigureAwait(false);
                break;
            case FileArtifact fileArtifact:
                await DisplayArtifactAsync(fileArtifact.FileInfo, fileArtifact.DisplayName, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    // Report artifacts as soon as they arrive instead of buffering them for the run summary. A browser or
    // WASI session can terminate before normal session teardown, and immediate output still tells the user
    // which artifact was produced while DisplayAsync serializes the line with the other console output.
    private Task DisplayArtifactAsync(FileInfo fileInfo, string displayName, CancellationToken cancellationToken)
        => DisplayAsync(this, new TextOutputDeviceData($"{displayName}: {fileInfo.FullName}"), cancellationToken);
}
