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
        using var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

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
        using var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restored"), CancellationToken.None);

        ServerLogMessage[] messages = GetMessages(device);
        Assert.HasCount(2, messages);
        Assert.AreSequenceEqual(["Restoring", "Restored"], messages.Select(static message => message.Message));
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterRemoval_QueuesSameValueAgain()
    {
        using var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.HasCount(2, GetMessages(device));
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterSessionEnds_QueuesSameValueAgain()
    {
        using var device = new ServerModePerCallOutputDevice(fileLoggerProvider: null, Mock.Of<IStopPoliciesService>());

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAfterSessionEndRunAsync(CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.HasCount(2, GetMessages(device).Where(static message => message.Level == LogLevel.Information));
    }

    private static ServerLogMessage[] GetMessages(ServerModePerCallOutputDevice device)
        => ((ConcurrentQueue<ServerLogMessage>)typeof(ServerModePerCallOutputDevice)
            .GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(device)!)
            .ToArray();

    [TestMethod]
    [UnsupportedOSPlatform("browser")]
    public Task PerCall_AllSeverityAndIndentedMultilineMessagesReachActualProtocolInOrder()
        => ServerTestHostTests.WithRunningHostAsync(async (host, next) =>
        {
            Mock<IStopPoliciesService> policies = new();
            Func<int, CancellationToken, Task>? maximumFailed = null;
            policies.Setup(p => p.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()))
                .Callback<Func<int, CancellationToken, Task>>(callback => maximumFailed = callback)
                .Returns(Task.CompletedTask);
            using ServerModePerCallOutputDevice device = new(null, policies.Object);
            await device.DisplayBannerAsync(null, CancellationToken.None);
            await device.DisplayBannerAsync("banner", CancellationToken.None);
            await device.DisplayBeforeSessionStartAsync(CancellationToken.None);
            await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("session"), CancellationToken.None);
            await device.DisplayAsync(Producer, new FormattedTextOutputDeviceData("first\r\nsecond") { Padding = 2 }, CancellationToken.None);
            await device.DisplayAsync(Producer, new TextOutputDeviceData("plain"), CancellationToken.None);
            await device.DisplayAsync(Producer, new WarningMessageOutputDeviceData("warning"), CancellationToken.None);
            await device.DisplayAsync(Producer, new ErrorMessageOutputDeviceData("error"), CancellationToken.None);
            await device.DisplayAsync(Producer, new ExceptionOutputDeviceData(new Exception("problem")), CancellationToken.None);
            await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("key", "step"), CancellationToken.None);
            await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("key", "step"), CancellationToken.None);
            await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("key", null), CancellationToken.None);
            await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("key", "step"), CancellationToken.None);
            await device.DisplayAfterSessionEndRunAsync(CancellationToken.None);
            await device.HandleProcessRoleAsync(TestProcessRole.TestHostController, CancellationToken.None);
            Assert.IsNull(maximumFailed);
            await device.HandleProcessRoleAsync(TestProcessRole.TestHost, CancellationToken.None);
            Assert.IsNotNull(maximumFailed);
            await maximumFailed(3, CancellationToken.None);

            await device.InitializeAsync(host);

            (LogLevel Level, string Text)[] expected =
            [
                (LogLevel.Debug, "banner"),
                (LogLevel.Trace, "Starting test session."),
                (LogLevel.Information, "session"),
                (LogLevel.Information, "  first" + Environment.NewLine + "  second" + Environment.NewLine),
                (LogLevel.Information, "plain"),
                (LogLevel.Warning, "warning"),
                (LogLevel.Error, "error"),
                (LogLevel.Error, "System.Exception: problem"),
                (LogLevel.Information, "step"),
                (LogLevel.Information, "step"),
                (LogLevel.Trace, "Finished test session."),
                (LogLevel.Information, "Test session is aborting due to reaching failures ('3') specified by the '--maximum-failed-tests' option."),
            ];
            foreach ((LogLevel level, string text) in expected)
            {
                NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(await next());
                Assert.AreEqual("client/log", notification.Method);
                ServerLogMessage message = Assert.IsInstanceOfType<LogEventArgs>(notification.Params).LogMessage;
                Assert.AreEqual(level, message.Level);
                Assert.AreEqual(text, message.Message);
            }

            Assert.AreEqual("ServerModePerCallOutputDevice", device.Uid);
            Assert.AreEqual("ServerModePerCallOutputDevice", device.DisplayName);
            Assert.AreEqual("ServerModePerCallOutputDevice", device.Description);
            Assert.AreEqual(PlatformTestMetadata.Version, device.Version);
            Assert.IsTrue(await device.IsEnabledAsync());
            policies.Verify(p => p.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()), Times.Once);
        });
}
