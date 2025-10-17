// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace MauiApp1.InProcess;

internal sealed class DisplayCompositeExtensionFactorySample : ITestSessionLifetimeHandler, IDataConsumer, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;
    private int _testNodeUpdateMessageCount;

    public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

    public string Uid => nameof(DisplayCompositeExtensionFactorySample);

    public string Version => "1.0.0";

    public string DisplayName => nameof(DisplayCompositeExtensionFactorySample);

    public string Description => "";

    public DisplayCompositeExtensionFactorySample(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        _testNodeUpdateMessageCount++;
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        string testNodeDisplayName = testNodeUpdateMessage.TestNode.DisplayName;
        TestNodeUid testNodeId = testNodeUpdateMessage.TestNode.Uid;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case InProgressTestNodeStateProperty _:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayCompositeExtensionFactorySample]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is in progress")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    }, cancellationToken);
                    break;
                }
            case PassedTestNodeStateProperty _:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayCompositeExtensionFactorySample]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is completed")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    }, cancellationToken);
                    break;
                }
            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayCompositeExtensionFactorySample]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is failed with '{failedTestNodeStateProperty?.Exception?.Message}'")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red }
                    }, cancellationToken);
                    break;
                }
            case SkippedTestNodeStateProperty _:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayCompositeExtensionFactorySample]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is skipped")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.White }
                    }, cancellationToken);
                    break;
                }
            default:
                break;
        }
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("[DisplayCompositeExtensionFactorySample]Hello from OnTestSessionStartingAsync")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.DarkGreen }
        }, testSessionContext.CancellationToken);

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayCompositeExtensionFactorySample]Total received 'TestNodeUpdateMessage': {_testNodeUpdateMessageCount}")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
        }, testSessionContext.CancellationToken);
}
