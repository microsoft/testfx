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

    [TestMethod]
    public async Task SystemProviderLocksDeadlineSnapshotAgainstChildOnlyOverrides()
    {
        var sourceVariables = new Hashtable
        {
            [EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE] = "2030-01-01T00:00:00Z",
        };
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariables()).Returns(sourceVariables);
        var systemProvider = new SystemEnvironmentVariableProvider(environment.Object);
        EnvironmentVariables environmentVariables = new(new TestLoggerFactory())
        {
            CurrentProvider = systemProvider,
        };

        await systemProvider.UpdateAsync(environmentVariables);

        Assert.IsTrue(environmentVariables.TryGetVariable(
            EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
            out OwnedEnvironmentVariable? deadline));
        Assert.AreEqual("2030-01-01T00:00:00Z", deadline!.Value);
        Assert.IsTrue(deadline.IsLocked);

        Assert.IsTrue(environmentVariables.TryGetVariable(
            EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN,
            out OwnedEnvironmentVariable? stopMargin));
        Assert.AreEqual(string.Empty, stopMargin!.Value);
        Assert.IsTrue(stopMargin.IsLocked);

        TestConfigurationEnvironmentVariableProvider testConfigurationProvider = await CreateProviderAsync(
            $"{{\"environmentVariables\": {{\"{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}\": \"2040-01-01T00:00:00Z\"}}}}");
        Assert.IsTrue(await testConfigurationProvider.IsEnabledAsync());
        environmentVariables.CurrentProvider = testConfigurationProvider;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => testConfigurationProvider.UpdateAsync(environmentVariables));
        Assert.Contains(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, exception.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    public async Task SystemProviderLocksDeadlineSnapshotCaseInsensitivelyOnWindows()
    {
        string lowerCaseDeadline = EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE.ToLowerInvariant();
        var sourceVariables = new Hashtable
        {
            [lowerCaseDeadline] = "2030-01-01T00:00:00Z",
        };
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariables()).Returns(sourceVariables);
        var systemProvider = new SystemEnvironmentVariableProvider(environment.Object);
        EnvironmentVariables environmentVariables = new(new TestLoggerFactory())
        {
            CurrentProvider = systemProvider,
        };
        await systemProvider.UpdateAsync(environmentVariables);

        Assert.IsTrue(environmentVariables.TryGetVariable(
            EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
            out OwnedEnvironmentVariable? deadline));
        Assert.AreEqual("2030-01-01T00:00:00Z", deadline!.Value);
        Assert.IsTrue(deadline.IsLocked);

        TestConfigurationEnvironmentVariableProvider testConfigurationProvider = await CreateProviderAsync(
            $"{{\"environmentVariables\": {{\"{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}\": \"2040-01-01T00:00:00Z\"}}}}");
        Assert.IsTrue(await testConfigurationProvider.IsEnabledAsync());
        environmentVariables.CurrentProvider = testConfigurationProvider;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => testConfigurationProvider.UpdateAsync(environmentVariables));
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

    [TestMethod]
    public async Task SystemProvider_TransfersOrdinaryValuesWithSystemProvenanceAndValidatesSnapshot()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariables()).Returns(new Hashtable { ["CUSTOM"] = "inherited-value" });
        SystemEnvironmentVariableProvider provider = new(environment.Object);
        EnvironmentVariables variables = new(new TestLoggerFactory()) { CurrentProvider = provider };

        Assert.IsTrue(await provider.IsEnabledAsync());
        await provider.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable("CUSTOM", out OwnedEnvironmentVariable? value));
        Assert.AreEqual("inherited-value", value!.Value);
        Assert.IsFalse(value.IsSecret);
        Assert.IsFalse(value.IsLocked);
        Assert.AreSame(provider, value.Owner);
        Assert.AreEqual("System", value.Owner.Uid);
        Assert.AreEqual("N/A", value.Owner.Version);
        Assert.AreEqual("System", value.Owner.DisplayName);
        Assert.AreEqual("System", value.Owner.Description);
        foreach (string key in new[] { "TESTINGPLATFORM_DEADLINE", "TESTINGPLATFORM_DEADLINE_STOP_MARGIN", "TESTINGPLATFORM_DEADLINE_DUMP_MARGIN" })
        {
            Assert.IsTrue(variables.TryGetVariable(key, out OwnedEnvironmentVariable? reserved));
            Assert.AreEqual(string.Empty, reserved!.Value);
            Assert.IsTrue(reserved.IsLocked);
        }

        ValidationResult validation = await provider.ValidateTestHostEnvironmentVariablesAsync(variables);
        Assert.IsTrue(validation.IsValid);
        Assert.IsNull(validation.ErrorMessage);
    }

    [TestMethod]
    public async Task ConfigurationProvider_DoesNotApplyBeforeEnableAndAllowsLaterOverride()
    {
        TestConfigurationEnvironmentVariableProvider provider = await CreateProviderAsync("""{"environmentVariables":{"CUSTOM":"configured-value"}}""");
        EnvironmentVariables variables = new(new TestLoggerFactory()) { CurrentProvider = provider };
        await provider.UpdateAsync(variables);
        Assert.IsEmpty(variables.GetAll());
        Assert.IsTrue(await provider.IsEnabledAsync());
        await provider.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable("CUSTOM", out OwnedEnvironmentVariable? value));
        Assert.AreEqual("configured-value", value!.Value);
        Assert.AreSame(provider, value.Owner);
        Assert.AreEqual("TestConfigurationEnvironmentVariableProvider", provider.Uid);
        Assert.AreEqual("testconfig.json environment variables provider", provider.DisplayName);
        Assert.AreEqual(
            "Applies environment variables declared in the 'environmentVariables' section of the testconfig.json file to the test host child process.",
            provider.Description);
        Assert.AreEqual("2.6.0-dev", provider.Version);
        Mock<ITestHostEnvironmentVariableProvider> later = new();
        later.SetupGet(p => p.Uid).Returns("later");
        variables.CurrentProvider = later.Object;
        variables.SetVariable(new EnvironmentVariable("CUSTOM", "overridden", false, false));

        Assert.IsTrue(variables.TryGetVariable("CUSTOM", out value));
        Assert.AreEqual("overridden", value!.Value);
        Assert.AreSame(later.Object, value.Owner);
    }

    [TestMethod]
    public async Task ConfigurationProvider_EmptyAndNulNamesRejectBeforeMutation()
    {
        TestConfigurationEnvironmentVariableProvider empty = await CreateProviderAsync("""{"environmentVariables":{"":"value"}}""");
        FormatException emptyFailure = await Assert.ThrowsExactlyAsync<FormatException>(empty.IsEnabledAsync);
        Assert.AreEqual(
            "Environment variable names declared in the 'environmentVariables' section of the testconfig.json file must not be empty.",
            emptyFailure.Message);
        TestConfigurationEnvironmentVariableProvider nul = await CreateProviderAsync("""{"environmentVariables":{"Bad\u0000Name":"value"}}""");
        FormatException nulFailure = await Assert.ThrowsExactlyAsync<FormatException>(nul.IsEnabledAsync);
        Assert.AreEqual(
            "Environment variable name 'Bad\0Name' declared in the 'environmentVariables' section of the testconfig.json file is invalid. Names must not contain '=' or null characters.",
            nulFailure.Message);
        EnvironmentVariables variables = new(new TestLoggerFactory()) { CurrentProvider = nul };
        await nul.UpdateAsync(variables);
        Assert.IsEmpty(variables.GetAll());
    }
}
