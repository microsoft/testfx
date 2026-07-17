// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed partial class TerminalOutputDevice
{
    /// <summary>
    /// Displays provided data through IConsole, which is typically System.Console.
    /// </summary>
    /// <param name="producer">The producer that sent the data.</param>
    /// <param name="data">The data to be displayed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        if (_isListTestsJson)
        {
            // Machine-readable mode: keep stdout reserved for the JSON document so consumers can
            // pipe it directly. Errors and exceptions still need surfacing somewhere, so route
            // them to stderr via WriteToStandardErrorAsync (the only place that bypasses IConsole,
            // which does not abstract stderr today). Azure Pipelines ##vso commands are skipped
            // here: they must be written to stdout to be processed, but stdout belongs to JSON.
            // Warnings and informational text are dropped to keep stdout strictly JSON.
            switch (data)
            {
                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(errorData.Message).ConfigureAwait(false);
                    break;

                case ExceptionOutputDeviceData exceptionData:
                    string exceptionText = exceptionData.Exception.ToString();
                    await LogDebugAsync(exceptionText).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(exceptionText).ConfigureAwait(false);
                    break;
            }

            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            switch (data)
            {
                case SessionMessageOutputDeviceData sessionMessageData:
                    await LogDebugAsync(sessionMessageData.Message).ConfigureAwait(false);
                    _terminalTestReporter.WriteMessage(sessionMessageData.Message);
                    break;

                case ProgressMessageOutputDeviceData progressMessageData:
                    await LogDebugAsync(progressMessageData.Message ?? string.Empty).ConfigureAwait(false);
                    _terminalTestReporter.UpdateProgressMessage(
                        InProcessExecutionId,
                        InProcessExecutionId,
                        producer.Uid,
                        progressMessageData.Key,
                        progressMessageData.Message);
                    break;

                case FormattedTextOutputDeviceData formattedTextData:
                    await LogDebugAsync(formattedTextData.Text).ConfigureAwait(false);
                    _terminalTestReporter.WriteMessage(formattedTextData.Text, formattedTextData.ForegroundColor as SystemConsoleColor, formattedTextData.Padding);
                    break;

                case TextOutputDeviceData textData:
                    await LogDebugAsync(textData.Text).ConfigureAwait(false);
                    _terminalTestReporter.WriteMessage(textData.Text);
                    break;

                case WarningMessageOutputDeviceData warningData:
                    await LogDebugAsync(warningData.Message).ConfigureAwait(false);
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityWarning, warningData.Message));
                    }

                    _terminalTestReporter.WriteWarningMessage(warningData.Message, null);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message).ConfigureAwait(false);
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityError, errorData.Message));
                    }

                    _terminalTestReporter.WriteErrorMessage(errorData.Message, null);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    string exceptionMessage = exceptionOutputDeviceData.Exception.ToString();
                    await LogDebugAsync(exceptionMessage).ConfigureAwait(false);
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityError, exceptionMessage));
                    }

                    _terminalTestReporter.WriteErrorMessage(exceptionOutputDeviceData.Exception);
                    break;
            }
        }
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);
        cancellationToken.ThrowIfCancellationRequested();

        // Under --server (e.g. `dotnet test` with `--server dotnettestcli`) the terminal device does not
        // buffer or render anything: data flows to the SDK through the dotnet-test pipe instead, and the
        // SDK is responsible for producing the output (including the --list-tests json document).
        if (_isServerMode)
        {
            return Task.CompletedTask;
        }

        if (_isListTestsJson)
        {
            // Machine-readable mode: only buffer discovered tests, do not write anything to the terminal.
            if (value is TestNodeUpdateMessage testNodeUpdate
                && testNodeUpdate.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is DiscoveredTestNodeStateProperty)
            {
                _discoveredTestsForJson.Add(testNodeUpdate.TestNode);
            }

            return Task.CompletedTask;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                // Single-pass collection: replaces 3 × SingleOrDefault<T>() + 1 × OfType<T>() (4 O(n) traversals + 1 heap alloc)
                //   + 1 × SingleOrDefault<TestNodeStateProperty>() (O(1) fast path, now folded into the walk)
                // with one zero-allocation GetStructEnumerator() walk.
                TimingProperty? timing = null;
                StandardOutputProperty? stdoutProp = null;
                StandardErrorProperty? stderrProp = null;
                TestNodeStateProperty? nodeState = null;
                PropertyBag.PropertyBagEnumerator enumerator = testNodeStateChanged.TestNode.Properties.GetStructEnumerator();
                while (enumerator.MoveNext())
                {
                    switch (enumerator.Current)
                    {
                        case TimingProperty t: timing = t; break;
                        case StandardOutputProperty so: stdoutProp = so; break;
                        case StandardErrorProperty se: stderrProp = se; break;
                        case TestNodeStateProperty s: nodeState = s; break;
                        case FileArtifactProperty fa:
                            _terminalTestReporter.ArtifactAdded(
                                outOfProcess: _processRole != TestProcessRole.TestHost,
                                assembly: _assemblyName,
                                targetFramework: _targetFramework,
                                architecture: _shortArchitecture,
                                executionId: InProcessExecutionId,
                                testName: testNodeStateChanged.TestNode.DisplayName,
                                fa.FileInfo.FullName);
                            break;
                    }
                }

                TimeSpan? duration = timing?.GlobalTiming.Duration;
                string? standardOutput = stdoutProp?.StandardOutput;
                string? standardError = stderrProp?.StandardError;

                switch (nodeState)
                {
                    case InProgressTestNodeStateProperty:
                        _terminalTestReporter.TestInProgress(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName);
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Error,
                            duration,
                            null,
                            errorState.Explanation,
                            errorState.Exception,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Fail,
                            duration,
                            null,
                            failedState.Explanation,
                            failedState.Exception,
                            expected: failedState.Exception?.Data["assert.expected"] as string,
                            actual: failedState.Exception?.Data["assert.actual"] as string,
                            standardOutput,
                            standardError);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Timeout,
                            duration,
                            null,
                            timeoutState.Explanation,
                            timeoutState.Exception,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                    case CancelledTestNodeStateProperty cancelledState:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Canceled,
                            duration,
                            null,
                            cancelledState.Explanation,
                            cancelledState.Exception,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case PassedTestNodeStateProperty:
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            outcome: TestOutcome.Passed,
                            duration: duration,
                            informativeMessage: null,
                            errorMessage: null,
                            exception: null,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case SkippedTestNodeStateProperty skippedState:
                        _terminalTestReporter.TestCompleted(
                            InProcessExecutionId,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Skipped,
                            duration,
                            informativeMessage: skippedState.Explanation,
                            errorMessage: null,
                            exception: null,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case DiscoveredTestNodeStateProperty:
                        _terminalTestReporter.TestDiscovered(InProcessExecutionId, testNodeStateChanged.TestNode.DisplayName);
                        break;
                }

                break;

            case SessionFileArtifact artifact:
                {
                    _terminalTestReporter.ArtifactAdded(
                        outOfProcess: _processRole != TestProcessRole.TestHost,
                        assembly: _assemblyName,
                        targetFramework: _targetFramework,
                        architecture: _shortArchitecture,
                        executionId: InProcessExecutionId,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
            case FileArtifact artifact:
                {
                    _terminalTestReporter.ArtifactAdded(
                        outOfProcess: _processRole != TestProcessRole.TestHost,
                        assembly: _assemblyName,
                        targetFramework: _targetFramework,
                        architecture: _shortArchitecture,
                        executionId: InProcessExecutionId,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
        }

        return Task.CompletedTask;
    }
}
