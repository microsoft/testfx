// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ServerModeManagerTests
{
    [TestMethod]
    public void Build_MissingClientPortOption_Throws()
    {
        ServiceProvider serviceProvider = CreateServiceProvider();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => ServerModeManager.Build(serviceProvider));

        Assert.AreEqual(PlatformResources.MissingClientPortFoJsonRpc, exception.Message);
    }

    [TestMethod]
    public void Build_ClientPortOptionWithoutClientHost_DefaultsHostToLoopbackAddress()
    {
        ServiceProvider serviceProvider = CreateServiceProvider("--client-port", "12345");

        ServerModeManager.MessageHandlerFactory factory = Assert.IsInstanceOfType<ServerModeManager.MessageHandlerFactory>(ServerModeManager.Build(serviceProvider));

        Assert.AreEqual("127.0.0.1", GetFieldValue<string>(factory, "_host"));
        Assert.AreEqual(12345, GetFieldValue<int>(factory, "_port"));
    }

    [TestMethod]
    public void Build_ClientPortAndClientHostOptions_UsesProvidedHostAndPort()
    {
        ServiceProvider serviceProvider = CreateServiceProvider("--client-port", "54321", "--client-host", "example.test");

        ServerModeManager.MessageHandlerFactory factory = Assert.IsInstanceOfType<ServerModeManager.MessageHandlerFactory>(ServerModeManager.Build(serviceProvider));

        Assert.AreEqual("example.test", GetFieldValue<string>(factory, "_host"));
        Assert.AreEqual(54321, GetFieldValue<int>(factory, "_port"));
    }

    private static ServiceProvider CreateServiceProvider(params string[] arguments)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(arguments, new SystemEnvironment());
        CommandLineHandler commandLineHandler = new(
            parseResult,
            extensionsCommandLineOptionsProviders: [],
            systemCommandLineOptionsProviders: [new PlatformCommandLineProvider()],
            Mock.Of<ITestApplicationModuleInfo>(),
            Mock.Of<IRuntimeFeature>());
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(commandLineHandler);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(Mock.Of<IOutputDevice>());
        return serviceProvider;
    }

    private static T GetFieldValue<T>(object instance, string fieldName)
        => (T)instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    [TestMethod]
    [DataRow("localhost", "127.0.0.1")]
    [DataRow("127.0.0.7", "127.0.0.7")]
    [UnsupportedOSPlatform("browser")]
    public async Task MessageHandlerFactory_DisabledFactoryPublishesEndpointBeforeTransport(string host, string expectedHost)
    {
        using CancellationTokenSource cancellation = new();
        InvalidOperationException expectedFailure = new("owned output failure before socket construction");
        Mock<IOutputDevice> output = new();
        List<string> messages = [];
        output.Setup(o => o.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                cancellation.Token))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (producer, data, _) =>
                {
                    Assert.AreEqual("MessageHandlerFactory", producer.Uid);
                    messages.Add(Assert.IsInstanceOfType<TextOutputDeviceData>(data).Text);
                })
            .ThrowsAsync(expectedFailure);
        ServerModeManager.MessageHandlerFactory factory = new(host, 12345, output.Object);
        Assert.IsFalse(await factory.IsEnabledAsync());

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => factory.CreateMessageHandlerAsync(cancellation.Token));

        Assert.AreSame(expectedFailure, actual);
        Assert.AreSequenceEqual(new[] { $"Connecting to client host '{expectedHost}' port '12345'" }, messages);
        output.Verify(
            o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), cancellation.Token),
            Times.Once);
    }
}
