// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions.Serializers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions;

internal class MSBuildConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private MSBuildTestApplicationLifecycleCallbacks? _msBuildTestApplicationLifecycleCallbacks;
    private bool _sessionEnded;
    private int _totalTests;
    private int _totalFailedTests;
    private int _totalPassedTests;
    private int _totalSkippedTests;

    public MSBuildConsumer(IServiceProvider serviceProvider, ICommandLineOptions commandLineOptions)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = commandLineOptions;
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(TestRequestExecutionTimeInfo),
    ];

    public string Uid => nameof(MSBuildConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(MSBuildConsumer);

    public string Description => Resources.MSBuildResources.MSBuildExtensionsDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(MSBuildCommandLineProvider.MSBuildNodeOptionKey));

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        // We get the pipe from the MSBuildTestApplicationLifecycleCallbacks only if we're enabled.
        _msBuildTestApplicationLifecycleCallbacks = _serviceProvider.GetRequiredService<MSBuildTestApplicationLifecycleCallbacks>();
        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        _sessionEnded = true;
        return Task.CompletedTask;
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Avoid processing messages if the session has ended.
        if (_sessionEnded)
        {
            return;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:
                TimingProperty? timingProperty = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TimingProperty>();
                string? duration = timingProperty is null ? null :
                    ToHumanReadableDuration(timingProperty.GlobalTiming.Duration.TotalMilliseconds);

                TestFileLocationProperty? testFileLocationProperty = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestFileLocationProperty>();

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case ErrorTestNodeStateProperty errorState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: false,
                            duration: duration,
                            errorMessage: errorState.Exception?.Message ?? errorState.Explanation,
                            errorStackTrace: errorState.Exception?.StackTrace,
                            expected: null,
                            actual: null,
                            testFileLocationProperty?.FilePath,
                            testFileLocationProperty?.LineSpan.Start.Line ?? 0,
                            cancellationToken);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: false,
                            duration: duration,
                            errorMessage: failedState.Exception?.Message ?? failedState.Explanation,
                            errorStackTrace: failedState.Exception?.StackTrace,
                            expected: failedState.Exception?.Data["assert.expected"] as string,
                            actual: failedState.Exception?.Data["assert.actual"] as string,
                            testFileLocationProperty?.FilePath,
                            testFileLocationProperty?.LineSpan.Start.Line ?? 0,
                            cancellationToken);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: true,
                            duration: duration,
                            errorMessage: timeoutState.Exception?.Message ?? timeoutState.Explanation,
                            errorStackTrace: timeoutState.Exception?.StackTrace,
                            expected: null,
                            actual: null,
                            testFileLocationProperty?.FilePath,
                            testFileLocationProperty?.LineSpan.Start.Line ?? 0,
                            cancellationToken);
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: true,
                            duration: duration,
                            errorMessage: cancelledState.Exception?.Message ?? cancelledState.Explanation,
                            errorStackTrace: cancelledState.Exception?.StackTrace,
                            expected: null,
                            actual: null,
                            testFileLocationProperty?.FilePath,
                            testFileLocationProperty?.LineSpan.Start.Line ?? 0,
                            cancellationToken);
                        break;

                    case PassedTestNodeStateProperty:
                        _totalTests++;
                        _totalPassedTests++;
                        break;

                    case SkippedTestNodeStateProperty:
                        _totalTests++;
                        _totalSkippedTests++;
                        break;
                }

                break;

            case TestRequestExecutionTimeInfo testRequestExecutionTimeInfo:
                await HandleSummaryAsync(testRequestExecutionTimeInfo, cancellationToken);

                break;
        }
    }

    private async Task HandleFailuresAsync(string testDisplayName, bool isCancelled, string? duration, string? errorMessage, string? errorStackTrace, string? expected, string? actual, string? codeFilePath, int lineNumber, CancellationToken cancellationToken)
    {
        _totalTests++;
        _totalFailedTests++;
        ApplicationStateGuard.Ensure(_msBuildTestApplicationLifecycleCallbacks != null);
        ApplicationStateGuard.Ensure(_msBuildTestApplicationLifecycleCallbacks.PipeClient != null);
        var failedTestInfoRequest = new FailedTestInfoRequest(testDisplayName, isCancelled, duration, errorMessage, errorStackTrace, expected, actual, codeFilePath, lineNumber);
        await _msBuildTestApplicationLifecycleCallbacks.PipeClient.RequestReplyAsync<FailedTestInfoRequest, VoidResponse>(failedTestInfoRequest, cancellationToken);
    }

    private async Task HandleSummaryAsync(TestRequestExecutionTimeInfo timeInfo, CancellationToken cancellationToken)
    {
        string? duration = ToHumanReadableDuration(timeInfo.TimingInfo.Duration.TotalMilliseconds);

        ApplicationStateGuard.Ensure(_msBuildTestApplicationLifecycleCallbacks != null);
        ApplicationStateGuard.Ensure(_msBuildTestApplicationLifecycleCallbacks.PipeClient != null);
        var runSummaryInfoRequest = new RunSummaryInfoRequest(_totalTests, _totalFailedTests, _totalPassedTests, _totalSkippedTests, duration);
        await _msBuildTestApplicationLifecycleCallbacks.PipeClient.RequestReplyAsync<RunSummaryInfoRequest, VoidResponse>(runSummaryInfoRequest, cancellationToken);
    }

    private static string? ToHumanReadableDuration(double? durationInMs)
    {
        if (durationInMs is null or < 0)
        {
            return null;
        }

        var time = TimeSpan.FromMilliseconds(durationInMs.Value);

        StringBuilder stringBuilder = new();
        bool hasParentValue = false;

        if (time.Days > 0)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{time.Days}d");
            hasParentValue = true;
        }

        if (time.Hours > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Hours.ToString(CultureInfo.InvariantCulture))}h");
            hasParentValue = true;
        }

        if (time.Minutes > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Minutes.ToString(CultureInfo.InvariantCulture))}m");
            hasParentValue = true;
        }

        if (time.Seconds > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Seconds.ToString(CultureInfo.InvariantCulture))}s");
            hasParentValue = true;
        }

        if (time.Milliseconds >= 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') : time.Milliseconds.ToString(CultureInfo.InvariantCulture))}ms");
        }

        return stringBuilder.ToString();
    }
}
