// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Text.Json;
#endif

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationManagerTests
{
    private readonly ServiceProvider _serviceProvider;

    public ConfigurationManagerTests()
    {
        _serviceProvider = new();
        _serviceProvider.AddService(new SystemFileSystem());
    }

    [TestMethod]
    [DynamicData(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJson(string jsonFileConfig, string key, string? result)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryFileStream(Encoding.UTF8.GetBytes(jsonFileConfig)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));
        IConfiguration configuration = await configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>()));
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

    [TestMethod]
    public async ValueTask InvalidJson_Fail()
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read)).Returns(() => new MemoryFileStream(Encoding.UTF8.GetBytes(string.Empty)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        // The behavior difference is System.Text.Json vs Jsonite
#if NETFRAMEWORK
        await Assert.ThrowsAsync<FormatException>(() => configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>())), ex => ex?.ToString() ?? "No exception was thrown");
#else
        await Assert.ThrowsAsync<JsonException>(() => configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>())), ex => ex?.ToString() ?? "No exception was thrown");
#endif
    }

    [TestMethod]
    [DynamicData(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJsonWithFileLoggerProvider(string jsonFileConfig, string key, string? result)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(jsonFileConfig);

        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new MemoryFileStream(bytes));

        Mock<ILogger> loggerMock = new();
        loggerMock.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);

        Mock<IFileLoggerProvider> loggerProviderMock = new();
        loggerProviderMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        IConfiguration configuration = await configurationManager.BuildAsync(loggerProviderMock.Object, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>()));
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");

        loggerMock.Verify(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, LoggingExtensions.Formatter), Times.Once);
    }

    [TestMethod]
    public async ValueTask BuildAsync_EmptyConfigurationSources_ThrowsException()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(new SystemFileSystem(), testApplicationModuleInfo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>())));
    }

    [TestMethod]
    public async ValueTask BuildAsync_ConfigurationSourcesNotEnabledAsync_ThrowsException()
    {
        Mock<IConfigurationSource> mockConfigurationSource = new();
        mockConfigurationSource.Setup(x => x.IsEnabledAsync()).ReturnsAsync(false);

        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(new SystemFileSystem(), testApplicationModuleInfo);
        configurationManager.AddConfigurationSource(() => mockConfigurationSource.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>())));

        mockConfigurationSource.Verify(x => x.IsEnabledAsync(), Times.Once);
    }

    [TestMethod]
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), Array.Empty<string>())));
    }

    private class FakeConfigurationSource : IConfigurationSource, IAsyncInitializableExtension
    {
        public string Uid => nameof(FakeConfigurationSource);

        public string Version => "1.0.0";

        public string DisplayName => nameof(FakeConfigurationSource);

        public string Description => nameof(FakeConfigurationSource);

        public required IConfigurationProvider ConfigurationProvider { get; set; }

        public int Order => 100;

        public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult) => Task.FromResult(ConfigurationProvider);

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
