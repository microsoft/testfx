// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Configurations;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Configurations;

[TestClass]
public sealed class RunSettingsConfigurationProviderTests
{
    private const string RunSettingsContentEnvironmentVariable = "TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS";
    private const string RunSettingsFileEnvironmentVariable = "TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE";
    private const string RunSettingsFilePath = "test.runsettings";

    private const string RunSettingsWithResultsDirectory = """
        <?xml version="1.0" encoding="utf-8"?>
        <RunSettings>
            <RunConfiguration>
                <ResultsDirectory>C:\MyResults</ResultsDirectory>
            </RunConfiguration>
        </RunSettings>
        """;

    [TestMethod]
    public void Properties_ReturnExpectedValues()
    {
        RunSettingsConfigurationProvider provider = CreateProvider();

        Assert.AreEqual(nameof(RunSettingsConfigurationProvider), provider.Uid);
        Assert.AreEqual(ExtensionVersion.DefaultSemVer, provider.Version);
        Assert.AreEqual("VSTest Helpers: runsettings configuration", provider.DisplayName);
        Assert.AreEqual("Configuration source to bridge VSTest xml runsettings configuration into Microsoft Testing Platform configuration model.", provider.Description);
        Assert.AreEqual(2, provider.Order);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue()
    {
        RunSettingsConfigurationProvider provider = CreateProvider();

        bool isEnabled = await provider.IsEnabledAsync();

        Assert.IsTrue(isEnabled);
    }

    [TestMethod]
    public void LoadAsync_ReturnsCompletedTask()
    {
        RunSettingsConfigurationProvider provider = CreateProvider();

        Task loadTask = provider.LoadAsync();

        Assert.AreSame(Task.CompletedTask, loadTask);
    }

    [TestMethod]
    public async Task BuildAsync_ReturnsSameProvider()
    {
        RunSettingsConfigurationProvider provider = CreateProvider(RunSettingsWithResultsDirectory);

        IConfigurationProvider builtProvider = await provider.BuildAsync(CreateParseResult());

        Assert.AreSame(provider, builtProvider);
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task BuildAsync_WithEmptyParseResult_ClearsConfiguration()
    {
        string? originalRunSettingsContent = Environment.GetEnvironmentVariable(RunSettingsContentEnvironmentVariable);
        string? originalRunSettingsFile = Environment.GetEnvironmentVariable(RunSettingsFileEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RunSettingsContentEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(RunSettingsFileEnvironmentVariable, null);

            RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(RunSettingsWithResultsDirectory);

            IConfigurationProvider builtProvider = await provider.BuildAsync(CommandLineParseResult.Empty);
            bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

            Assert.AreSame(provider, builtProvider);
            Assert.IsFalse(result);
            Assert.IsNull(value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RunSettingsContentEnvironmentVariable, originalRunSettingsContent);
            Environment.SetEnvironmentVariable(RunSettingsFileEnvironmentVariable, originalRunSettingsFile);
        }
    }

    [TestMethod]
    public void TryGet_BeforeBuildAsync_ReturnsFalse()
    {
        RunSettingsConfigurationProvider provider = CreateProvider();

        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGet_WithResultsDirectory_ReturnsValue()
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(RunSettingsWithResultsDirectory);

        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        Assert.IsTrue(result);
        Assert.AreEqual("""C:\MyResults""", value);
    }

    [TestMethod]
    public async Task TryGet_WithEmptyResultsDirectory_ReturnsEmptyValue()
    {
        const string runSettings = """
            <RunSettings>
                <RunConfiguration>
                    <ResultsDirectory />
                </RunConfiguration>
            </RunSettings>
            """;
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(runSettings);

        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        Assert.IsTrue(result);
        Assert.AreEqual(string.Empty, value);
    }

    [TestMethod]
    [DataRow("<RunSettings />")]
    [DataRow("<RunSettings><RunConfiguration /></RunSettings>")]
    [DataRow("<OtherRoot><RunConfiguration><ResultsDirectory>ignored</ResultsDirectory></RunConfiguration></OtherRoot>")]
    public async Task TryGet_WithMissingConfigurationElement_ReturnsFalse(string runSettings)
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(runSettings);

        bool result = provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGet_WithUnrelatedKey_ReturnsFalse()
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(RunSettingsWithResultsDirectory);

        bool result = provider.TryGet("unrelatedKey", out string? value);

        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task TryGetScalar_UsesSameLookupAsTryGet()
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(RunSettingsWithResultsDirectory);

        bool result = provider.TryGetScalar(PlatformConfigurationConstants.PlatformResultDirectory, out string? value);

        Assert.IsTrue(result);
        Assert.AreEqual("""C:\MyResults""", value);
    }

    [TestMethod]
    public async Task GetChildKeys_WithResultsDirectory_ReturnsExpectedHierarchy()
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync(RunSettingsWithResultsDirectory);

        Assert.AreSequenceEqual(["platformOptions"], provider.GetChildKeys(null).ToArray());
        Assert.AreSequenceEqual(["resultDirectory"], provider.GetChildKeys("platformOptions").ToArray());
        Assert.IsEmpty(provider.GetChildKeys("platformOptions:resultDirectory"));
        Assert.IsEmpty(provider.GetChildKeys("unrelatedKey"));
    }

    [TestMethod]
    public async Task GetChildKeys_WithoutResultsDirectory_ReturnsEmpty()
    {
        RunSettingsConfigurationProvider provider = await CreateBuiltProviderAsync("<RunSettings />");

        Assert.IsEmpty(provider.GetChildKeys(null));
        Assert.IsEmpty(provider.GetChildKeys("platformOptions"));
    }

    private static RunSettingsConfigurationProvider CreateProvider(string? runSettings = null)
    {
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        if (runSettings is not null)
        {
            fileSystem.Setup(x => x.ExistFile(RunSettingsFilePath)).Returns(true);
            fileSystem.Setup(x => x.ReadAllText(RunSettingsFilePath)).Returns(runSettings);
        }

        return new RunSettingsConfigurationProvider(fileSystem.Object);
    }

    private static async Task<RunSettingsConfigurationProvider> CreateBuiltProviderAsync(string runSettings)
    {
        RunSettingsConfigurationProvider provider = CreateProvider(runSettings);
        await provider.BuildAsync(CreateParseResult());
        return provider;
    }

    private static CommandLineParseResult CreateParseResult()
        => new(
            toolName: null,
            options: [new CommandLineParseOption("settings", [RunSettingsFilePath])],
            errors: []);
}
