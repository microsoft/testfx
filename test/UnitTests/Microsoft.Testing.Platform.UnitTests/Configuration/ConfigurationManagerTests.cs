// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ConfigurationManagerTests : TestBase
{
    public ConfigurationManagerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [ArgumentsProvider(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJson(string jsonFileConfig, string key, string? result)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(jsonFileConfig)));
        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(
                new SystemRuntime(new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler()),
                fileSystem.Object, null));
        IConfiguration configuration = await configurationManager.BuildAsync(new ServiceProvider(), null);
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");
    }

    internal static IEnumerable<(string JsonFileConfig, string Key, string? Result)> GetConfigurationValueFromJsonData()
    {
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:Enable", "True");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:enable", "True");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:Missing", null);
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true} , \"CrashDump2\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:", null);
        yield return ("{}", "TestingPlatform:Troubleshooting:CrashDump:Enable", null);
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform:0", "1");
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform:1", "2");
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform", "[1,2]");
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:0", null);
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:0:Key", "Value");
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:1:Key", "3");
    }

    public async ValueTask InvalidJson_Fail()
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open)).Returns(new MemoryStream(Encoding.UTF8.GetBytes(string.Empty)));
        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(
                new SystemRuntime(new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler()),
                fileSystem.Object, null));
        await Assert.ThrowsAsync<Exception>(() => configurationManager.BuildAsync(new ServiceProvider(), null));
    }

    [ArgumentsProvider(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJsonWithFileLoggerProvider(string jsonFileConfig, string key, string? result)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(jsonFileConfig);

        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open))
            .Returns(new MemoryStream(bytes));
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryStream(bytes));

        Mock<ServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetServicesInternal(typeof(IFileSystem), It.IsAny<bool>(), It.IsAny<bool>())).Returns(new List<IFileSystem>() { fileSystem.Object });

        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(
                new SystemRuntime(new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler()),
                fileSystem.Object, null));

        IConfiguration configuration = await configurationManager.BuildAsync(
            serviceProviderMock.Object,
            new FileLoggerProvider(string.Empty, new SystemClock(), LogLevel.Trace, "testLog_", customDirectory: false, syncFlush: true));
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");

        // TODO: Assert log was written.
    }

    public async ValueTask BuildAsync_EmptyConfigurationSources_ThrowsException()
    {
        Mock<ServiceProvider> mockServiceProvider = new();

        ConfigurationManager configurationManager = new();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => configurationManager.BuildAsync(
                mockServiceProvider.Object,
                null));
    }

    public async ValueTask BuildAsync_ConfigurationSourcesNotEnabledAsync_ThrowsException()
    {
        Mock<IConfigurationSource> mockConfigurationSource = new();
        mockConfigurationSource.Setup(x => x.IsEnabledAsync()).ReturnsAsync(false);

        Mock<ServiceProvider> mockServiceProvider = new();

        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() => mockConfigurationSource.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => configurationManager.BuildAsync(
                mockServiceProvider.Object,
                null));

        mockConfigurationSource.Verify(x => x.IsEnabledAsync(), Times.Once);
    }

    public async ValueTask BuildAsync_ConfigurationSourceIsAsyncInitializableExtension_InitializeAsyncIsCalled()
    {
        Mock<IConfigurationProvider> mockConfigurationProvider = new();
        mockConfigurationProvider.Setup(x => x.LoadAsync()).Callback(() => { });

        Mock<ServiceProvider> mockServiceProvider = new();

        Mock<FakeConfigurationSource> fakeConfigurationSource = new();
        fakeConfigurationSource.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        fakeConfigurationSource.Setup(x => x.InitializeAsync()).Callback(() => { });
        fakeConfigurationSource.Setup(x => x.Build()).Returns(mockConfigurationProvider.Object);

        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() => fakeConfigurationSource.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => configurationManager.BuildAsync(
                mockServiceProvider.Object,
                null));

        fakeConfigurationSource.Verify(x => x.IsEnabledAsync(), Times.Once);
        fakeConfigurationSource.Verify(x => x.InitializeAsync(), Times.Once);
    }
}

#pragma warning disable CA1852
internal class FakeConfigurationSource : IConfigurationSource, IAsyncInitializableExtension
#pragma warning restore CA1852
{
    public string Uid => nameof(FakeConfigurationSource);

    public string Version => "1.0.0";

    public string DisplayName => nameof(FakeConfigurationSource);

    public string Description => nameof(FakeConfigurationSource);

    public virtual IConfigurationProvider Build() => throw new NotImplementedException();

    public virtual Task InitializeAsync() => throw new NotImplementedException();

    public virtual Task<bool> IsEnabledAsync() => throw new NotImplementedException();
}
