// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestConfigurationEnvironmentVariableProviderTests
{
    [TestMethod]
    public async Task IsEnabledAsync_AbsentSection_ReturnsFalse()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync("{\"other\": {}}");
        Assert.IsFalse(await provider.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_EmptySection_ReturnsFalse()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync("{\"environmentVariables\": {}}");
        Assert.IsFalse(await provider.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_PopulatedSection_ReturnsTrue()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync(
            "{\"environmentVariables\": {\"DOTNET_ENVIRONMENT\": \"Development\"}}");
        Assert.IsTrue(await provider.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_InvalidVariableName_Throws()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync(
            "{\"environmentVariables\": {\"FOO=BAR\": \"value\"}}");

        FormatException exception = await Assert.ThrowsAsync<FormatException>(() => provider.IsEnabledAsync());
        Assert.Contains("FOO=BAR", exception.Message);
    }

    [TestMethod]
    public async Task UpdateAsync_AppliesAllEntries()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync(
            "{\"environmentVariables\": {\"DOTNET_ENVIRONMENT\": \"Development\", \"HEADED\": \"1\"}}");

        Assert.IsTrue(await provider.IsEnabledAsync());

        EnvironmentVariables environmentVariables = new(new TestLoggerFactory())
        {
            CurrentProvider = provider,
        };
        await provider.UpdateAsync(environmentVariables);

        Assert.IsTrue(environmentVariables.TryGetVariable("DOTNET_ENVIRONMENT", out OwnedEnvironmentVariable? envVar));
        Assert.AreEqual("Development", envVar!.Value);
        Assert.IsFalse(envVar.IsLocked, "Entry must not be locked so other providers (e.g. runsettings) can override.");
        Assert.IsFalse(envVar.IsSecret);

        Assert.IsTrue(environmentVariables.TryGetVariable("HEADED", out envVar));
        Assert.AreEqual("1", envVar!.Value);
    }

    [TestMethod]
    public async Task ValidateAsync_AlwaysSucceeds()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync(
            "{\"environmentVariables\": {\"FOO\": \"bar\"}}");

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(
            new EnvironmentVariables(new TestLoggerFactory()));
        Assert.IsTrue(result.IsValid);
    }

    private static async Task<TestConfigurationEnvironmentVariableProvider> CreateProviderAsync(string jsonFileContent)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes(jsonFileContent)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));
        IConfiguration configuration = await configurationManager.BuildAsync(null, new CommandLineParseResult(null, new List<CommandLineParseOption>(), []));
        return new TestConfigurationEnvironmentVariableProvider(configuration);
    }

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger();

        private sealed class TestLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => false;

            public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Task.CompletedTask;

            public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
