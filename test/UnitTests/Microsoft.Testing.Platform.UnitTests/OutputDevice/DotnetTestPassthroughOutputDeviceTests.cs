// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DotnetTestPassthroughOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>();

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_LikeNopDevice()
    {
        var device = new DotnetTestPassthroughOutputDevice(Mock.Of<IServiceProvider>());
        Assert.IsFalse(await device.IsEnabledAsync());
    }

    [TestMethod]
    public async Task DisplayAsync_WithNonMarkerData_IsSwallowedWithoutResolvingTheConnection()
    {
        // A strict mock fails the test if the device touches the service provider at all: non-marker
        // output must be discarded as early as NopPlatformOutputDevice would, preserving the deliberate
        // pipe-protocol suppression.
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new FormattedTextOutputDeviceData("##[group]not a marker"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(It.IsAny<Type>()), Times.Never);
    }

    [TestMethod]
    public async Task DisplayAsync_WithMarkerButNoConnection_IsSwallowedWithoutThrowing()
    {
        // The marker is recognized (so the connection is looked up), but with no dotnet test connection
        // resolved there is nothing to forward to and the line is dropped.
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IPushOnlyProtocol))).Returns(null!);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new AzureDevOpsCommandOutputDeviceData("##[group]Tests: A (net9.0)"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(typeof(IPushOnlyProtocol)), Times.Once);
    }

    [TestMethod]
    public async Task DisplayAsync_WithWarningButNoConnection_IsSwallowedWithoutThrowing()
    {
        // A warning message is recognized as forwardable (so the connection is looked up), but with no dotnet
        // test connection resolved there is nothing to forward to and the message is dropped.
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IPushOnlyProtocol))).Returns(null!);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new WarningMessageOutputDeviceData("a warning"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(typeof(IPushOnlyProtocol)), Times.Once);
    }

    [TestMethod]
    public async Task DisplayAsync_WithErrorButNoConnection_IsSwallowedWithoutThrowing()
    {
        // An error message is recognized as forwardable (so the connection is looked up), but with no dotnet
        // test connection resolved there is nothing to forward to and the message is dropped.
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IPushOnlyProtocol))).Returns(null!);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new ErrorMessageOutputDeviceData("an error"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(typeof(IPushOnlyProtocol)), Times.Once);
    }

    [TestMethod]
    public async Task DisplayAsync_WithInformationalText_IsSwallowedWithoutResolvingTheConnection()
    {
        // Plain informational text (not an Azure DevOps marker, not a warning/error) must be discarded as
        // early as NopPlatformOutputDevice would, without touching the service provider.
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new TextOutputDeviceData("just some info"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(It.IsAny<Type>()), Times.Never);
    }

    [TestMethod]
    public async Task DisplayAsync_WithSessionMessageButNoConnection_IsSwallowedWithoutThrowing()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IPushOnlyProtocol))).Returns(null!);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("Restoring test assets"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(typeof(IPushOnlyProtocol)), Times.Once);
    }

    [TestMethod]
    public async Task DisplayAsync_WithProgressMessage_IsSwallowedWithoutResolvingTheConnection()
    {
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring test assets"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(It.IsAny<Type>()), Times.Never);
    }
}
