// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

namespace MauiApp1;

internal sealed class DisplayDataConsumer : IDataConsumer, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

    public string Uid => nameof(DisplayDataConsumer);

    public string Version => "1.0.0";

    public string DisplayName => nameof(DisplayDataConsumer);

    public string Description => "This extension display in console the testnode id and display name of TestNodeUpdateMessage data type.";

    ObservableCollection<string> _logs;
    public DisplayDataConsumer(IOutputDevice outputDevice, ObservableCollection<string> logs)
    {
        _outputDevice = outputDevice;
        _logs = logs;
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        string testNodeDisplayName = testNodeUpdateMessage.TestNode.DisplayName;
        TestNodeUid testNodeId = testNodeUpdateMessage.TestNode.Uid;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case InProgressTestNodeStateProperty _:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is in progress")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    }, cancellationToken);
                    break;
                }
            case PassedTestNodeStateProperty _:
                {
                    _logs.Add($"Test name '{testNodeDisplayName}' is completed");
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is completed")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green }
                    }, cancellationToken);
                    break;
                }
            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is failed with '{failedTestNodeStateProperty?.Exception?.Message}'")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red }
                    }, cancellationToken);
                    break;
                }
            case SkippedTestNodeStateProperty _:
                {
                    await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"[DisplayDataConsumer]TestNode '{testNodeId}' with display name '{testNodeDisplayName}' is skipped")
                    {
                        ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.White }
                    }, cancellationToken);
                    break;
                }
            default:
                break;
        }
    }
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
