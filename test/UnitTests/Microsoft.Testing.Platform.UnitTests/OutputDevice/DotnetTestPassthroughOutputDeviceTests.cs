// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

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
    [DataRow("##[group]not a marker", DisplayName = "Unmarked_AzureGroup")]
    [DataRow("::group::not a marker", DisplayName = "Unmarked_GitHubGroup")]
    [DataRow("::endgroup::", DisplayName = "Unmarked_GitHubEndGroup")]
    [DataRow("::error title=Failure::not a marker", DisplayName = "Unmarked_GitHubError")]
    [DataRow("::warning title=Skipped::not a marker", DisplayName = "Unmarked_GitHubWarning")]
    [DataRow("::notice title=Slow test::not a marker", DisplayName = "Unmarked_GitHubNotice")]
    public async Task DisplayAsync_WithNonMarkerData_IsSwallowedWithoutResolvingTheConnection(string text)
    {
        // A strict mock fails the test if the device touches the service provider at all: non-marker
        // output must be discarded as early as NopPlatformOutputDevice would, preserving the deliberate
        // pipe-protocol suppression.
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new FormattedTextOutputDeviceData(text), CancellationToken.None);
        await device.DisplayAsync(Producer, new TextOutputDeviceData(text), CancellationToken.None);

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
    public async Task DisplayAsync_WithGitHubActionsMarkerButNoConnection_IsSwallowedWithoutThrowing()
    {
        // A missing pipe connection must not turn a workflow command into a console fallback.
        // Strictness also prevents resolving an IConsole or any service other than the protocol.
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        serviceProvider.Setup(p => p.GetService(typeof(IPushOnlyProtocol))).Returns(null!);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider.Object);

        await device.DisplayAsync(Producer, new GitHubActionsCommandOutputDeviceData("::group::Tests: A (net9.0)"), CancellationToken.None);

        serviceProvider.Verify(p => p.GetService(typeof(IPushOnlyProtocol)), Times.Once);
        serviceProvider.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisplayAsync_WithDiagnosticButNoConnection_WritesToConsole(bool isError)
    {
        const string message = """
            Invalid command line arguments:
                A diagnostic produced before the pipe connection exists.
            """;
        var console = new Mock<IConsole>(MockBehavior.Strict);
        console.Setup(c => c.WriteLine(message));
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(console.Object);
        var device = new DotnetTestPassthroughOutputDevice(serviceProvider);
        IOutputDeviceData data = isError
            ? new ErrorMessageOutputDeviceData(message)
            : new WarningMessageOutputDeviceData(message);

        await device.DisplayAsync(Producer, data, CancellationToken.None);

        console.Verify(c => c.WriteLine(message), Times.Once);
        console.VerifyNoOtherCalls();
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
