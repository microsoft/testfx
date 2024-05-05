// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;

namespace TestingPlatformExplorer.In_process_extensions;
internal class DisplayDataConsumer : IDataConsumer, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

    public string Uid => nameof(DisplayDataConsumer);

    public string Version => "1.0.0";

    public string DisplayName => nameof(DisplayDataConsumer);

    public string Description => "This extension display in console the testnode id and display name of TestNodeUpdateMessage data type.";

    public DisplayDataConsumer(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        string testNodeDisplayName = testNodeUpdateMessage.TestNode.DisplayName;
        TestNodeUid testNodeId = testNodeUpdateMessage.TestNode.Uid;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case InProgressTestNodeStateProperty _:
                {
                    _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is in progress")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    });
                    break;
                }
            case PassedTestNodeStateProperty _:
                {
                    _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is completed")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    });
                    break;
                }
            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                {
                    _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is failed with '{failedTestNodeStateProperty?.Exception?.Message}'")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red }
                    });
                    break;
                }
            case SkippedTestNodeStateProperty _:
                {
                    _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is skipped")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.White }
                    });
                    break;
                }
            default:
                break;
        }

        return Task.CompletedTask;
    }
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
