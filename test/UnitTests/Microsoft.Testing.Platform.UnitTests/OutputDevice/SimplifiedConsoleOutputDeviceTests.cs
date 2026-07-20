// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class SimplifiedConsoleOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

    [TestMethod]
    public async Task DisplayAsync_SessionMessage_WritesDurableOutput()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("Restoring assets"), CancellationToken.None);

        Assert.HasCount(1, device.Messages);
        Assert.AreEqual("Restoring assets", device.Messages[0]);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessage_WritesOnlyChangedValues()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restored"), CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "Restoring", "Restored" }, device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterRemoval_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "Restoring", "Restoring" }, device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterSessionFinishes_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "Restoring", "Restoring" }, device.Messages);
    }

    private static RecordingSimplifiedOutputDevice CreateOutputDevice(IAsyncMonitor asyncMonitor)
    {
        var moduleInfo = new Mock<ITestApplicationModuleInfo>();
        moduleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");

        return new RecordingSimplifiedOutputDevice(
            Mock.Of<IConsole>(),
            moduleInfo.Object,
            asyncMonitor,
            Mock.Of<IRuntimeFeature>(),
            Mock.Of<IEnvironment>(),
            Mock.Of<IPlatformInformation>(),
            Mock.Of<IStopPoliciesService>());
    }

    private sealed class RecordingSimplifiedOutputDevice : SimplifiedConsoleOutputDeviceBase
    {
        public RecordingSimplifiedOutputDevice(
            IConsole console,
            ITestApplicationModuleInfo testApplicationModuleInfo,
            IAsyncMonitor asyncMonitor,
            IRuntimeFeature runtimeFeature,
            IEnvironment environment,
            IPlatformInformation platformInformation,
            IStopPoliciesService policiesService)
            : base(console, testApplicationModuleInfo, asyncMonitor, runtimeFeature, environment, platformInformation, policiesService)
        {
        }

        public List<string?> Messages { get; } = [];

        public override string DisplayName => nameof(RecordingSimplifiedOutputDevice);

        public override string Description => nameof(RecordingSimplifiedOutputDevice);

        protected override void ConsoleWarn(string? message) => Messages.Add(message);

        protected override void ConsoleError(string? message) => Messages.Add(message);

        protected override void ConsoleLog(string? message) => Messages.Add(message);
    }
}
