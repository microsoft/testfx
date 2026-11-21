// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationManagerBuildOrderingTests
{
    [TestMethod]
    public async Task BuildAsync_InitializesEveryInitializableSourceBeforeBuildingItsProvider()
    {
        List<string> events = [];
        OrderedSource source = new(order: 10, name: "initializable", events, new DictionaryConfigurationProvider([]));
        ConfigurationManager manager = CreateManager(source);

        _ = await manager.BuildAsync(null, CommandLineParseResult.Empty);

        Assert.AreSequenceEqual(["initialize:initializable", "build:initializable"], events);
    }

    [TestMethod]
    public async Task BuildAsync_ProvidersAreOrderedByAscendingSourceOrder()
    {
        List<string> events = [];
        ConfigurationManager manager = CreateManager(
            new OrderedSource(order: 5, name: "five", events, new DictionaryConfigurationProvider(new() { ["shared"] = "five" })),
            new OrderedSource(order: 1, name: "one", events, new DictionaryConfigurationProvider(new() { ["shared"] = "one" })),
            new OrderedSource(order: 3, name: "three", events, new DictionaryConfigurationProvider(new() { ["shared"] = "three" })));

        IConfiguration configuration = await manager.BuildAsync(null, CommandLineParseResult.Empty);

        Assert.AreEqual("one", configuration["shared"]);
    }

    [TestMethod]
    public void ConfigurationSourcesOptions_RegistersEnvironmentVariablesSourceByDefault()
        => Assert.IsTrue(new ConfigurationSourcesOptions().RegisterEnvironmentVariablesConfigurationSource);

    [TestMethod]
    public void GetChildKeys_EmptyChildName_IsReturnedAsEmptyKey()
    {
        string[] children = [.. ConfigurationProviderHelpers.GetChildKeys(["parent::x", "parent:a:b", "parent:c", "other:d"], "parent")];

        Assert.HasCount(3, children);
        Assert.Contains(string.Empty, children);
        Assert.Contains("a", children);
        Assert.Contains("c", children);
    }

    [TestMethod]
    public void ConfigurationSection_Key_IsTheLastPathSegment()
    {
        AggregatedConfiguration configuration = new(
            [],
            new Mock<ITestApplicationModuleInfo>().Object,
            new Mock<IFileSystem>().Object,
            new Mock<IEnvironment>().Object,
            CommandLineParseResult.Empty);

        Assert.AreEqual("noDelimiter", configuration.GetSection("noDelimiter").Key);
        Assert.AreEqual("b", configuration.GetSection("a:b").Key);
        Assert.AreEqual("x", configuration.GetSection(":x").Key);
        Assert.AreEqual(":x", configuration.GetSection(":x").Path);
    }

    private static ConfigurationManager CreateManager(params IConfigurationSource[] sources)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes("{}")));
        CurrentTestApplicationModuleInfo module = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager manager = new(fileSystem.Object, module, new SystemEnvironment());
        manager.AddConfigurationSource(() => new JsonConfigurationSource(module, fileSystem.Object, null));
        foreach (IConfigurationSource source in sources)
        {
            manager.AddConfigurationSource(() => source);
        }

        return manager;
    }

    private sealed class OrderedSource(int order, string name, List<string> events, IConfigurationProvider provider) : IConfigurationSource, IAsyncInitializableExtension
    {
        public string Uid => name;

        public string Version => "1.0.0";

        public string DisplayName => name;

        public string Description => name;

        public int Order => order;

        public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        {
            events.Add("build:" + name);
            return Task.FromResult(provider);
        }

        public Task InitializeAsync()
        {
            events.Add("initialize:" + name);
            return Task.CompletedTask;
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
