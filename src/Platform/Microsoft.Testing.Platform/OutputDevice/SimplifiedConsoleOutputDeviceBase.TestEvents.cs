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

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                // Single-pass collection: replaces SingleOrDefault<TimingProperty>() + SingleOrDefault<TestNodeStateProperty>()
                // with one zero-allocation GetStructEnumerator() walk. Note: TestNodeStateProperty was already O(1) via direct
                // field access, so the win here is code-path simplification and consistency with the established single-pass pattern.
                TimingProperty? timingProp = null;
                TestNodeStateProperty? nodeStateProp = null;
                bool executionCompleted = false;
                PropertyBag.PropertyBagEnumerator enumerator = testNodeStateChanged.TestNode.Properties.GetStructEnumerator();
                while (enumerator.MoveNext())
                {
                    switch (enumerator.Current)
                    {
                        case TimingProperty t: timingProp = t; break;
                        case TestNodeStateProperty s: nodeStateProp = s; break;
                        case TestNodeExecutionCompletedProperty: executionCompleted = true; break;
                    }
                }

                TimeSpan? duration = timingProp?.GlobalTiming.Duration;
                bool testCompleted = executionCompleted;

                if (nodeStateProp is InProgressTestNodeStateProperty)
                {
                    _activeTestTracker.Start(testNodeStateChanged.TestNode.Uid, testNodeStateChanged.TestNode.DisplayName);
                }
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                else if (executionCompleted
                    || nodeStateProp is PassedTestNodeStateProperty or ErrorTestNodeStateProperty or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                    or FailedTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty)
                {
                    _activeTestTracker.Complete(testNodeStateChanged.TestNode.Uid);
                }

                switch (nodeStateProp)
                {
                    case InProgressTestNodeStateProperty:
                        if (DisplayActiveTestProgress)
                        {
                            return DisplayAsync(
                                this,
                                new ProgressMessageOutputDeviceData(
                                    testNodeStateChanged.TestNode.Uid.Value,
                                    $"running {testNodeStateChanged.TestNode.DisplayName}"),
                                cancellationToken);
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

                if (testCompleted && DisplayActiveTestProgress)
                {
                    return DisplayAsync(
                        this,
                        new ProgressMessageOutputDeviceData(testNodeStateChanged.TestNode.Uid.Value, message: null),
                        cancellationToken);
                }

                // Tracked by https://github.com/microsoft/testfx/issues/8086:
                // surface per-test file artifacts in the simplified console output once the format is defined.
                break;

            case SessionFileArtifact:
                {
                    // Tracked by https://github.com/microsoft/testfx/issues/8086:
                    // session-level artifacts are currently ignored by this output device.
                }

                break;
            case FileArtifact:
                {
                    // Tracked by https://github.com/microsoft/testfx/issues/8086:
                    // file artifacts are currently ignored by this output device.
                }

                break;
        }

        return Task.CompletedTask;
    }
}
