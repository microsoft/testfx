// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationSourceDefaultsTests
{
    [TestMethod]
    public async Task BuiltInSources_UseExpectedCommonDefaults()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        CommandLineConfigurationSource commandLineSource = new();
        EnvironmentVariablesConfigurationSource environmentVariablesSource = new(new SystemEnvironment());
        JsonConfigurationSource jsonConfigurationSource = new(testApplicationModuleInfo, new SystemFileSystem(), null);

        IConfigurationSource[] sources =
        [
            commandLineSource,
            environmentVariablesSource,
            jsonConfigurationSource,
        ];
        string expectedVersion = commandLineSource.Version;
        Assert.IsFalse(string.IsNullOrWhiteSpace(expectedVersion));
        Assert.AreEqual(expectedVersion, environmentVariablesSource.Version);
        Assert.AreEqual(expectedVersion, jsonConfigurationSource.Version);

        foreach (IConfigurationSource source in sources)
        {
            Assert.AreEqual(expectedVersion, source.Version);
            Assert.AreEqual(string.Empty, source.DisplayName);
            Assert.AreEqual(string.Empty, source.Description);
            Assert.IsTrue(await source.IsEnabledAsync());
        }
    }

    [TestMethod]
    public async Task Sources_IdentifyDistinctPriorityLayersAndBuildUsableProviders()
    {
        Moq.Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariables()).Returns(new Hashtable { ["Layer__Value"] = "environment" });
        Moq.Mock<IFileSystem> files = new();
        files.Setup(f => f.ExistFile(Moq.It.IsAny<string>())).Returns(true);
        files.Setup(f => f.NewFileStream(Moq.It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes("""{"Layer":{"Value":"json"}}""")));
        Moq.Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.TryGetAssemblyName()).Returns("ConfiguredTests");
        IConfigurationSource[] sources =
        [
            new CommandLineConfigurationSource(),
            new EnvironmentVariablesConfigurationSource(environment.Object),
            new JsonConfigurationSource(module.Object, files.Object, null),
        ];
        Assert.AreSequenceEqual(
            ["CommandLineConfigurationSource", "EnvironmentVariablesConfigurationSource", "JsonConfigurationSource"],
            sources.Select(source => source.Uid));
        Assert.AreSequenceEqual([0, 1, 3], sources.Select(source => source.Order));
        Microsoft.Testing.Platform.CommandLine.CommandLineParseResult parsed = new(
            null, [new("layer", ["command line"])], []);

        IConfigurationProvider cli = await sources[0].BuildAsync(parsed);
        IConfigurationProvider env = await sources[1].BuildAsync(parsed);
        IConfigurationProvider json = await sources[2].BuildAsync(parsed);
        await cli.LoadAsync();
        await env.LoadAsync();
        await json.LoadAsync();
        Assert.IsTrue(cli.TryGet("commandLineOptions:layer:0", out string? cliValue));
        Assert.AreEqual("command line", cliValue);
        Assert.IsTrue(env.TryGet("Layer:Value", out string? envValue));
        Assert.AreEqual("environment", envValue);
        Assert.IsTrue(json.TryGet("Layer:Value", out string? jsonValue));
        Assert.AreEqual("json", jsonValue);
        files.Verify(f => f.NewFileStream("ConfiguredTests.testconfig.json", FileMode.Open, FileAccess.Read), Moq.Times.Once);
    }
}
