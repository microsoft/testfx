// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.ServerMode;

[TestClass]
public sealed class ServerModePerCallOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

    [TestMethod]
    public async Task DisplayAsync_SessionMessage_QueuesInformationalLog()
    {
        var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("Restoring assets"), CancellationToken.None);

        ServerLogMessage[] messages = GetMessages(device);
        Assert.HasCount(1, messages);
        ServerLogMessage message = messages[0];
        Assert.AreEqual(LogLevel.Information, message.Level);
        Assert.AreEqual("Restoring assets", message.Message);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessage_QueuesOnlyChangedValues()
    {
        var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restored"), CancellationToken.None);

        ServerLogMessage[] messages = GetMessages(device);
        Assert.HasCount(2, messages);
        Assert.Contains("Restoring", messages.Select(static message => message.Message));
        Assert.Contains("Restored", messages.Select(static message => message.Message));
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterRemoval_QueuesSameValueAgain()
    {
        var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.HasCount(2, GetMessages(device));
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterSessionEnds_QueuesSameValueAgain()
    {
        var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAfterSessionEndRunAsync(CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.HasCount(2, GetMessages(device).Where(static message => message.Level == LogLevel.Information));
    }

    private static ServerLogMessage[] GetMessages(ServerModePerCallOutputDevice device)
        => ((ConcurrentBag<ServerLogMessage>)typeof(ServerModePerCallOutputDevice)
            .GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(device)!)
            .ToArray();
}
