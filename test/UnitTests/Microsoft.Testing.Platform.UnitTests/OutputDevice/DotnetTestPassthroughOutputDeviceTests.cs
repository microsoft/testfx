// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
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

    [TestMethod]
    [DataRow("1.1.0", 0, 0)]
    [DataRow("1.2.0", 0, 1)]
    [DataRow("1.3.0", 4, 1)]
    public async Task Passthrough_NegotiatedVersionsGateTypedCiAndDisplayMessages(string version, int expectedDisplays, int expectedAzure)
    {
        using CancellationTokenSource application = new();
        using CancellationTokenSource call = new();
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(application.Token);
        fixture.Client.Respond(
            new HandshakeMessage(new()
            {
                [HandshakeMessagePropertyNames.SupportedProtocolVersions] = version,
            }),
            application.Token);
        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));
        List<DisplayMessage> displays = [];
        List<AzureDevOpsLogMessage> azure = [];
        fixture.Client.RespondVoid<DisplayMessage>(application.Token, displays.Add);
        fixture.Client.RespondVoid<AzureDevOpsLogMessage>(application.Token, azure.Add);
        Mock<IConsole> console = new(MockBehavior.Strict);
        ServiceProvider services = new();
        services.AddServices([fixture.Connection, fixture.Environment.Object, console.Object]);
        DotnetTestPassthroughOutputDevice device = new(services);

        await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("durable session"), call.Token);
        await device.DisplayAsync(Producer, new GitHubActionsCommandOutputDeviceData("::group::exact workflow"), call.Token);
        await device.DisplayAsync(Producer, new WarningMessageOutputDeviceData("warning payload"), call.Token);
        await device.DisplayAsync(Producer, new ErrorMessageOutputDeviceData("error payload"), call.Token);
        await device.DisplayAsync(Producer, new AzureDevOpsCommandOutputDeviceData("##[group]exact azure"), call.Token);
        await device.DisplayAsync(Producer, new TextOutputDeviceData("must be discarded"), call.Token);
        await device.DisplayBannerAsync("must be discarded", call.Token);
        await device.DisplayBeforeSessionStartAsync(call.Token);
        await device.DisplayAfterSessionEndRunAsync(call.Token);
        await device.HandleProcessRoleAsync(TestProcessRole.TestHost, call.Token);

        Assert.HasCount(expectedDisplays, displays);
        Assert.HasCount(expectedAzure, azure);
        if (expectedDisplays > 0)
        {
            Assert.AreSequenceEqual(new byte[] { 0, 0, 1, 2 }, displays.Select(message => message.Level));
            Assert.AreSequenceEqual(["durable session", "::group::exact workflow", "warning payload", "error payload"], displays.Select(message => message.Text));
            Assert.IsTrue(displays.All(message => message.ExecutionId == "execution" && message.InstanceId == DotnetTestConnection.InstanceId));
        }

        if (expectedAzure > 0)
        {
            Assert.AreEqual("##[group]exact azure", azure[0].LogText);
            Assert.AreEqual("execution", azure[0].ExecutionId);
            Assert.AreEqual(DotnetTestConnection.InstanceId, azure[0].InstanceId);
        }

        Assert.IsFalse(await device.IsEnabledAsync());
        Assert.AreEqual("DotnetTestPassthroughOutputDevice", device.Uid);
        Assert.AreEqual("DotnetTestPassthroughOutputDevice", device.DisplayName);
        Assert.AreEqual("2.6.0-dev", device.Version);
        Assert.AreEqual(
            "Output device that discards regular informational host output but forwards durable session messages, CI logging commands, and warning/error messages to the SDK under the dotnet test pipe protocol.",
            device.Description);
        Assert.IsTrue(fixture.Client.Mock.Invocations
            .Where(invocation => invocation.Method.Name == "RequestReplyAsync")
            .All(invocation => (CancellationToken)invocation.Arguments[1] == application.Token));
        console.VerifyNoOtherCalls();
    }
}
