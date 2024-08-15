// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ConfigurationManagerTests : TestBase
{
    private readonly ServiceProvider _serviceProvider;

    public ConfigurationManagerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _serviceProvider = new();
        _serviceProvider.AddService(new SystemFileSystem());
    }

    [ArgumentsProvider(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJson(string jsonFileConfig, string key, string? result)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryFileStream(Encoding.UTF8.GetBytes(jsonFileConfig)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));
        IConfiguration configuration = await configurationManager.BuildAsync(null);
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");
    }

    internal static IEnumerable<(string JsonFileConfig, string Key, string? Result)> GetConfigurationValueFromJsonData()
    {
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "platformOptions:Troubleshooting:CrashDump:Enable", "True");
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "platformOptions:Troubleshooting:CrashDump:enable", "True");
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "platformOptions:Troubleshooting:CrashDump:Missing", null);
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "platformOptions:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true} , \"CrashDump2\": {\"Enable\": true}}}}", "platformOptions:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"platformOptions\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "platformOptions:", null);
        yield return ("{}", "platformOptions:Troubleshooting:CrashDump:Enable", null);
        yield return ("{\"platformOptions\": [1,2] }", "platformOptions:0", "1");
        yield return ("{\"platformOptions\": [1,2] }", "platformOptions:1", "2");
        yield return ("{\"platformOptions\": [1,2] }", "platformOptions", "[1,2]");
        yield return ("{\"platformOptions\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "platformOptions:Array:0", null);
        yield return ("{\"platformOptions\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "platformOptions:Array:0:Key", "Value");
        yield return ("{\"platformOptions\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "platformOptions:Array:1:Key", "3");
    }

    public async ValueTask InvalidJson_Fail()
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open)).Returns(new MemoryFileStream(Encoding.UTF8.GetBytes(string.Empty)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));
        await Assert.ThrowsAsync<Exception>(() => configurationManager.BuildAsync(null));
    }

    [ArgumentsProvider(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJsonWithFileLoggerProvider(string jsonFileConfig, string key, string? result)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(jsonFileConfig);

        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open))
            .Returns(new MemoryFileStream(bytes));
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryFileStream(bytes));

        Mock<ILogger> loggerMock = new();
        loggerMock.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);

        Mock<IFileLoggerProvider> loggerProviderMock = new();
        loggerProviderMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        IConfiguration configuration = await configurationManager.BuildAsync(loggerProviderMock.Object);
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");

        loggerMock.Verify(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask BuildAsync_EmptyConfigurationSources_ThrowsException()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(new SystemFileSystem(), testApplicationModuleInfo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null));
    }

    public async ValueTask BuildAsync_ConfigurationSourcesNotEnabledAsync_ThrowsException()
    {
        Mock<IConfigurationSource> mockConfigurationSource = new();
        mockConfigurationSource.Setup(x => x.IsEnabledAsync()).ReturnsAsync(false);

        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(new SystemFileSystem(), testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() => mockConfigurationSource.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null));

        mockConfigurationSource.Verify(x => x.IsEnabledAsync(), Times.Once);
    }

    public async ValueTask BuildAsync_ConfigurationSourceIsAsyncInitializableExtension_InitializeAsyncIsCalled()
    {
        Mock<IConfigurationProvider> mockConfigurationProvider = new();
        mockConfigurationProvider.Setup(x => x.LoadAsync()).Callback(() => { });

        FakeConfigurationSource fakeConfigurationSource = new()
        {
            ConfigurationProvider = mockConfigurationProvider.Object,
        };

        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(new SystemFileSystem(), testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() => fakeConfigurationSource);

        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null));
    }

    private class FakeConfigurationSource : IConfigurationSource, IAsyncInitializableExtension
    {
        public string Uid => nameof(FakeConfigurationSource);

        public string Version => "1.0.0";

        public string DisplayName => nameof(FakeConfigurationSource);

        public string Description => nameof(FakeConfigurationSource);

        public required IConfigurationProvider ConfigurationProvider { get; set; }

        public IConfigurationProvider Build() => ConfigurationProvider;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private class MemoryFileStream : IFileStream
    {
        private readonly MemoryStream _stream;

        public MemoryFileStream(byte[] bytes) => _stream = new MemoryStream(bytes);

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose()
            => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync()
            => _stream.DisposeAsync();
#endif
    }
}
