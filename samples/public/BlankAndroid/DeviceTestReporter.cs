// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Android.Util;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace BlankAndroid;

/// <summary>
/// Builder hook that registers device-specific extensions with the testing platform.
/// </summary>
public static class DeviceTestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
    {
        // Register the test result reporter (IDataConsumer)
        builder.TestHost.AddDataConsumer(serviceProvider =>
            new DeviceTestReporter(serviceProvider.GetOutputDevice()));

        // Register the session lifetime handler
        builder.TestHost.AddTestSessionLifetimeHandle(serviceProvider =>
            new DeviceTestSessionHandler(serviceProvider.GetOutputDevice()));
    }
}

/// <summary>
/// Custom data consumer that outputs test results to Android logcat.
/// Implements IDataConsumer to receive test node updates and IOutputDeviceDataProducer to output results.
/// </summary>
internal sealed class DeviceTestReporter : IDataConsumer, IOutputDeviceDataProducer
{
    private const string TAG = "MTP.TestResults";
    private readonly IOutputDevice _outputDevice;
    private int _passed;
    private int _failed;
    private int _skipped;

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(DeviceTestReporter);

    public string Version => "1.0.0";

    public string DisplayName => "Device Test Reporter";

    public string Description => "Reports test results to Android logcat for device testing scenarios.";

    public DeviceTestReporter(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        string testDisplayName = testNodeUpdateMessage.TestNode.DisplayName;
        TestNodeUid testId = testNodeUpdateMessage.TestNode.Uid;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case InProgressTestNodeStateProperty:
                Log.Info(TAG, $"▶ Running: {testDisplayName}");
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"▶ Running: {testDisplayName}")
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Cyan }
                }, cancellationToken);
                break;

            case PassedTestNodeStateProperty:
                _passed++;
                Log.Info(TAG, $"✓ Passed:  {testDisplayName}");
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"✓ Passed:  {testDisplayName}")
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Green }
                }, cancellationToken);
                break;

            case FailedTestNodeStateProperty failedState:
                _failed++;
                string errorMessage = failedState.Exception?.Message ?? "Unknown error";
                Log.Info(TAG, $"✗ Failed:  {testDisplayName}");
                Log.Info(TAG, $"  Error:   {errorMessage}");
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"✗ Failed:  {testDisplayName}\n  Error:   {errorMessage}")
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Red }
                }, cancellationToken);
                break;

            case ErrorTestNodeStateProperty errorState:
                _failed++;
                string errMsg = errorState.Exception?.Message ?? "Unknown error";
                Log.Info(TAG, $"✗ Error:   {testDisplayName}");
                Log.Info(TAG, $"  Message: {errMsg}");
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"✗ Error:   {testDisplayName}\n  Message: {errMsg}")
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Red }
                }, cancellationToken);
                break;

            case SkippedTestNodeStateProperty:
                _skipped++;
                Log.Info(TAG, $"○ Skipped: {testDisplayName}");
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"○ Skipped: {testDisplayName}")
                {
                    ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Yellow }
                }, cancellationToken);
                break;

            default:
                // Log unexpected state for debugging
                Log.Info(TAG, $"? Unknown state for: {testDisplayName} - {nodeState.GetType().Name}");
                break;
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

/// <summary>
/// Test session lifetime handler that outputs summary at the start and end of test sessions.
/// </summary>
internal sealed class DeviceTestSessionHandler : ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string TAG = "MTP.TestSession";
    private readonly IOutputDevice _outputDevice;
    private DateTime _startTime;

    public string Uid => nameof(DeviceTestSessionHandler);

    public string Version => "1.0.0";

    public string DisplayName => "Device Test Session Handler";

    public string Description => "Handles test session lifecycle for device testing.";

    public DeviceTestSessionHandler(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        _startTime = DateTime.Now;
        string message = $"""
            
            ╔══════════════════════════════════════════════════════════════╗
            ║           Microsoft.Testing.Platform - Device Tests          ║
            ╠══════════════════════════════════════════════════════════════╣
            ║  Started: {_startTime:yyyy-MM-dd HH:mm:ss}                              ║
            ╚══════════════════════════════════════════════════════════════╝
            
            """;
        Log.Info(TAG, message);
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(message)
        {
            ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Cyan }
        }, testSessionContext.CancellationToken);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        var duration = DateTime.Now - _startTime;
        string message = $"""
            
            ══════════════════════════════════════════════════════════════
              Test Run Completed
              Duration: {duration.TotalSeconds:F2}s
            ══════════════════════════════════════════════════════════════
            
            """;
        Log.Info(TAG, message);
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(message)
        {
            ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Cyan }
        }, testSessionContext.CancellationToken);
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
