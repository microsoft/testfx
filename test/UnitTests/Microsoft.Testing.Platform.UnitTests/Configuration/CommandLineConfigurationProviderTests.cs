// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineConfigurationProviderTests
{
    [TestMethod]
    public void ZeroArityOption_StoresBooleanPresenceMarker()
    {
        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("hangdump", [])],
            errors: []);

        CommandLineConfigurationProvider provider = new(parseResult);

        Assert.IsTrue(provider.TryGet("commandLineOptions:hangdump", out string? value));
        Assert.AreEqual(bool.TrueString, value);
    }

    [TestMethod]
    public void SingleValueOption_StoresFirstIndexedEntry()
    {
        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("hangdump-timeout", ["5m"])],
            errors: []);

        CommandLineConfigurationProvider provider = new(parseResult);

        Assert.IsTrue(provider.TryGet("commandLineOptions:hangdump-timeout:0", out string? value));
        Assert.AreEqual("5m", value);
        Assert.IsFalse(provider.TryGet("commandLineOptions:hangdump-timeout", out _));
    }

    [TestMethod]
    public void MultiValueOption_StoresEveryArgumentUnderIndexedKey()
    {
        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("filter-uid", ["a", "b", "c"])],
            errors: []);

        CommandLineConfigurationProvider provider = new(parseResult);

        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:0", out string? v0));
        Assert.AreEqual("a", v0);
        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:1", out string? v1));
        Assert.AreEqual("b", v1);
        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:2", out string? v2));
        Assert.AreEqual("c", v2);
        Assert.IsFalse(provider.TryGet("commandLineOptions:filter-uid:3", out _));
    }

    [TestMethod]
    public void RepeatedOption_FlattenedIntoContiguousIndexedSequence()
    {
        CommandLineParseResult parseResult = new(
            toolName: null,
            options:
            [
                new CommandLineParseOption("filter-uid", ["a"]),
                new CommandLineParseOption("filter-uid", ["b", "c"]),
            ],
            errors: []);

        CommandLineConfigurationProvider provider = new(parseResult);

        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:0", out string? v0));
        Assert.AreEqual("a", v0);
        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:1", out string? v1));
        Assert.AreEqual("b", v1);
        Assert.IsTrue(provider.TryGet("commandLineOptions:filter-uid:2", out string? v2));
        Assert.AreEqual("c", v2);
    }

    [TestMethod]
    public void OptionLookup_IsCaseInsensitive()
    {
        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("HangDump-Timeout", ["5m"])],
            errors: []);

        CommandLineConfigurationProvider provider = new(parseResult);

        Assert.IsTrue(provider.TryGet("commandLineOptions:hangdump-timeout:0", out string? value));
        Assert.AreEqual("5m", value);
    }

    [TestMethod]
    public void UnknownKey_ReturnsFalse()
    {
        CommandLineConfigurationProvider provider = new(CommandLineParseResult.Empty);

        Assert.IsFalse(provider.TryGet("commandLineOptions:does-not-exist", out _));
        Assert.IsFalse(provider.TryGet("totally-unrelated-key", out _));
    }

    [TestMethod]
    public async Task RegisteredAsConfigurationSource_TopsAllOtherProvidersForCommandLineOptionsKeys()
    {
        // Arrange: JSON has an array that lands at "commandLineOptions:hangdump-timeout:0".
        // CLI also writes the same key with a different value via the same shape. The CLI
        // source (Order=0) must win over the JSON source (Order=3). Using a JSON array (not
        // a scalar) is important: it forces both providers to compete for the SAME storage
        // key, so a precedence inversion would actually flip the observed value.
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(
                Encoding.UTF8.GetBytes("{\"commandLineOptions\": {\"hangdump-timeout\": [\"10m\"]}}")));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new CommandLineConfigurationSource());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("hangdump-timeout", ["5m"])],
            errors: []);

        // Act
        IConfiguration configuration = await configurationManager.BuildAsync(null, parseResult);

        // Assert: CLI wins for the shared key (would return "10m" if precedence were inverted).
        Assert.AreEqual("5m", configuration["commandLineOptions:hangdump-timeout:0"]);
    }

    [TestMethod]
    public async Task ProviderAwareResolution_CliZeroArityShadowsJsonIndexedArgs()
    {
        // Regression test for issue #6349 review: CLI explicitly passes a zero-arity flag.
        // JSON has the same option as an array of arguments. The CLI shape (bare key with
        // boolean) and the JSON shape (indexed entries) DO NOT overlap on any single key,
        // so a naive per-key precedence would merge them and surface JSON's arguments.
        // The provider-aware resolver must instead pick the CLI provider entirely and
        // report the option as set with zero arguments.
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(
                Encoding.UTF8.GetBytes("{\"commandLineOptions\": {\"list-tests\": [\"from-json\"]}}")));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new CommandLineConfigurationSource());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        CommandLineParseResult parseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("list-tests", [])],
            errors: []);

        IConfiguration configuration = await configurationManager.BuildAsync(null, parseResult);

        CommandLineHandler handler = new(
            parseResult,
            extensionsCommandLineOptionsProviders: [],
            systemCommandLineOptionsProviders: [],
            testApplicationModuleInfo,
            new Mock<IRuntimeFeature>().Object,
            configuration);

        ICommandLineOptions options = handler;

        Assert.IsTrue(options.IsOptionSet("list-tests"));
        Assert.IsTrue(options.TryGetOptionArgumentList("list-tests", out string[]? args));
        Assert.IsNotNull(args);
        Assert.IsEmpty(args);
    }

    [TestMethod]
    public async Task RegisteredAsConfigurationSource_FallsBackToJsonForOptionsAbsentFromCli()
    {
        // Arrange: option is only in JSON, not on the CLI.
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(
                Encoding.UTF8.GetBytes("{\"commandLineOptions\": {\"hangdump-timeout\": \"10m\"}}")));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new CommandLineConfigurationSource());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        // Act
        IConfiguration configuration = await configurationManager.BuildAsync(null, CommandLineParseResult.Empty);

        // Assert: JSON value is exposed via the merged view.
        Assert.AreEqual("10m", configuration["commandLineOptions:hangdump-timeout"]);
    }

    [TestMethod]
    public void ProviderAwareResolution_FirstProviderWithDataShadowsLaterProvidersForSameOption()
    {
        // Lock the "first provider wins outright" invariant using two in-memory providers
        // whose shapes intentionally don't overlap on any single key. A naive per-key
        // precedence would merge them; the provider-aware resolver must not.
        InMemoryConfigurationProvider cliShape = new(new()
        {
            ["commandLineOptions:list-tests"] = bool.TrueString,
        });
        InMemoryConfigurationProvider jsonShape = new(new()
        {
            ["commandLineOptions:list-tests:0"] = "from-json-a",
            ["commandLineOptions:list-tests:1"] = "from-json-b",
        });

        AggregatedConfiguration configuration = new(
            [cliShape, jsonShape],
            new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()),
            new Mock<IFileSystem>().Object,
            new SystemEnvironment(),
            CommandLineParseResult.Empty);

        Assert.IsTrue(configuration.IsCommandLineOptionSet("list-tests"));
        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("list-tests", out string[]? args));
        Assert.IsNotNull(args);
        Assert.IsEmpty(args);
    }

    [TestMethod]
    public void ProviderAwareResolution_ExplicitDisableAtFirstProviderShortCircuits()
    {
        // First provider says "false" → entire chain is short-circuited as "not set" even
        // though a later provider would otherwise enable the option.
        InMemoryConfigurationProvider disableShape = new(new()
        {
            ["commandLineOptions:hangdump"] = bool.FalseString,
        });
        InMemoryConfigurationProvider enableShape = new(new()
        {
            ["commandLineOptions:hangdump"] = bool.TrueString,
        });

        AggregatedConfiguration configuration = new(
            [disableShape, enableShape],
            new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()),
            new Mock<IFileSystem>().Object,
            new SystemEnvironment(),
            CommandLineParseResult.Empty);

        Assert.IsFalse(configuration.IsCommandLineOptionSet("hangdump"));
        Assert.IsFalse(configuration.TryGetCommandLineOptionArguments("hangdump", out _));
    }

    private sealed class InMemoryConfigurationProvider(Dictionary<string, string?> entries) : IConfigurationProvider
    {
        public Task LoadAsync() => Task.CompletedTask;

        public bool TryGet(string key, out string? value) => entries.TryGetValue(key, out value);
    }
}
