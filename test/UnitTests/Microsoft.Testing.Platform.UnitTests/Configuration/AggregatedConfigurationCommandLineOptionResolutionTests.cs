// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AggregatedConfigurationCommandLineOptionResolutionTests
{
    private const string Opt = "commandLineOptions:opt";
    private const string OptDefault = "commandLineOptionDefaults:opt";

    // ---------------------------------------------------------------------
    // Option resolution across providers
    // ---------------------------------------------------------------------
    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_IndexedEntries_ReturnsEveryArgumentInOrder()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            [Opt + ":0"] = "a",
            [Opt + ":1"] = "b",
            [Opt + ":2"] = "c",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["a", "b", "c"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_OptionNameWithPrefix_IsTrimmed()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            [Opt + ":0"] = "a",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("--opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["a"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_IndexedEntryWithNullValue_StopsCollection()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            [Opt + ":0"] = "a",
            [Opt + ":1"] = null,
            [Opt + ":2"] = "c",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["a"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_GapInIndexes_StopsCollection()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            [Opt + ":0"] = "a",
            [Opt + ":2"] = "c",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["a"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_BareKeyWithNullValue_IsTreatedAsAbsentAtThatProvider()
    {
        AggregatedConfiguration configuration = Create(
            new DictionaryConfigurationProvider(new() { [Opt] = null }),
            new DictionaryConfigurationProvider(new() { [Opt] = "value" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["value"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_BareScalar_IsReturnedAsSingleArgument()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { [Opt] = "30s" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["30s"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_BareTrue_IsPresenceMarkerWithoutArguments()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { [Opt] = "true" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.IsEmpty(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_BareFalse_IsExplicitDisableThatShadowsLowerProviders()
    {
        AggregatedConfiguration configuration = Create(
            new DictionaryConfigurationProvider(new() { [Opt] = "false" }),
            new DictionaryConfigurationProvider(new() { [Opt + ":0"] = "lower" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsFalse(isSet);
        Assert.IsEmpty(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_FirstProviderWithDataWins()
    {
        AggregatedConfiguration configuration = Create(
            new DictionaryConfigurationProvider(new() { ["other:key"] = "x" }),
            new DictionaryConfigurationProvider(new() { [Opt + ":0"] = "second", [Opt + ":1"] = "second-too" }),
            new DictionaryConfigurationProvider(new() { [Opt + ":0"] = "third" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["second", "second-too"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionFromProviders_NoProviderMentionsOption_ReturnsNotFound()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { ["commandLineOptions:another:0"] = "a" }));

        Assert.IsFalse(configuration.TryGetCommandLineOptionFromProviders("opt", out bool isSet, out string[] arguments));
        Assert.IsFalse(isSet);
        Assert.IsEmpty(arguments);
    }

    // ---------------------------------------------------------------------
    // Passive defaults across providers
    // ---------------------------------------------------------------------
    [TestMethod]
    public void TryGetCommandLineOptionDefaultFromProviders_IndexedEntries_ReturnsEveryArgumentInOrder()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            [OptDefault + ":0"] = "a",
            [OptDefault + ":1"] = "b",
            [OptDefault + ":2"] = "c",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionDefaultFromProviders("--opt", out string[] arguments));
        Assert.AreSequenceEqual(["a", "b", "c"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultFromProviders_IndexedEntryWithNullValueOrGap_StopsCollection()
    {
        AggregatedConfiguration withNull = Create(new DictionaryConfigurationProvider(new()
        {
            [OptDefault + ":0"] = "a",
            [OptDefault + ":1"] = null,
            [OptDefault + ":2"] = "c",
        }));
        AggregatedConfiguration withGap = Create(new DictionaryConfigurationProvider(new()
        {
            [OptDefault + ":0"] = "a",
            [OptDefault + ":2"] = "c",
        }));

        Assert.IsTrue(withNull.TryGetCommandLineOptionDefaultFromProviders("opt", out string[] nullArguments));
        Assert.AreSequenceEqual(["a"], nullArguments);
        Assert.IsTrue(withGap.TryGetCommandLineOptionDefaultFromProviders("opt", out string[] gapArguments));
        Assert.AreSequenceEqual(["a"], gapArguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultFromProviders_ScalarWithNullAtFirstProvider_UsesNextProvider()
    {
        AggregatedConfiguration configuration = Create(
            new DictionaryConfigurationProvider(new() { [OptDefault] = null }),
            new DictionaryConfigurationProvider(new() { [OptDefault] = "fallback" }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionDefaultFromProviders("opt", out string[] arguments));
        Assert.AreSequenceEqual(["fallback"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultFromProviders_NothingDeclared_ReturnsFalse()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { [Opt + ":0"] = "active-not-default" }));

        Assert.IsFalse(configuration.TryGetCommandLineOptionDefaultFromProviders("opt", out string[] arguments));
        Assert.IsEmpty(arguments);
    }

    // ---------------------------------------------------------------------
    // Indexer
    // ---------------------------------------------------------------------
    [TestMethod]
    public void Indexer_ArbitraryKey_IsResolvedFromProvidersAndNotFromWorkingDirectory()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { ["custom:key"] = "custom-value" }));

        Assert.AreEqual("custom-value", configuration["custom:key"]);
        Assert.IsNull(configuration["custom:missing"]);
    }

    [TestMethod]
    public void Indexer_WorkingDirectoryKeys_ReturnTheTestApplicationDirectoryInsteadOfProviderData()
    {
        AggregatedConfiguration configuration = Create();

        Assert.AreEqual("work", configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]);
        Assert.AreEqual("work", configuration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory]);
    }

    // ---------------------------------------------------------------------
    // JSON-backed helpers degrade to empty when no JSON source exists
    // ---------------------------------------------------------------------
    [TestMethod]
    public void JsonBackedHelpers_NoJsonSource_ReturnEmptyCollections()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new() { ["section:key"] = "value" }));

        Assert.IsEmpty(configuration.GetTestConfigJsonSection("section"));
        Assert.IsEmpty(configuration.EnumerateJsonCommandLineOptions());
        Assert.IsEmpty(configuration.EnumerateJsonCommandLineOptionDefaults());
    }

    // ---------------------------------------------------------------------
    // Results directory
    // ---------------------------------------------------------------------
    [TestMethod]
    public void ResultsDirectory_CommandLineSourceRegistered_HonorsOptionFromAnyProviderBeforeDefaults()
    {
        AggregatedConfiguration configuration = Create(
            new CommandLineConfigurationProvider(CommandLineParseResult.Empty),
            new DictionaryConfigurationProvider(new()
            {
                ["commandLineOptions:results-directory:0"] = "from-json",
                [PlatformConfigurationConstants.PlatformResultDirectory] = "legacy",
            }));

        Assert.AreEqual("from-json", configuration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void ResultsDirectory_CommandLineSourceRegistered_CommandLineValueWins()
    {
        CommandLineParseResult parseResult = new(null, [new CommandLineParseOption("results-directory", ["from-cli"])], []);
        AggregatedConfiguration configuration = Create(
            parseResult,
            new CommandLineConfigurationProvider(parseResult),
            new DictionaryConfigurationProvider(new() { ["commandLineOptions:results-directory:0"] = "from-json" }));

        Assert.AreEqual("from-cli", configuration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void ResultsDirectory_CommandLineSourceRegistered_OptionWithoutArguments_FallsBackToConfiguredDirectory()
    {
        AggregatedConfiguration configuration = Create(
            new CommandLineConfigurationProvider(CommandLineParseResult.Empty),
            new DictionaryConfigurationProvider(new()
            {
                ["commandLineOptions:results-directory"] = "true",
                [PlatformConfigurationConstants.PlatformResultDirectory] = "configured",
            }));

        Assert.AreEqual("configured", configuration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void ResultsDirectory_NoCommandLineSource_IgnoresProvidersAndUsesParseResult()
    {
        CommandLineParseResult parseResult = new(null, [new CommandLineParseOption("results-directory", ["from-cli"])], []);
        AggregatedConfiguration configuration = Create(
            parseResult,
            new DictionaryConfigurationProvider(new() { ["commandLineOptions:results-directory:0"] = "from-provider" }));

        Assert.AreEqual("from-cli", configuration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    [TestMethod]
    public void ResultsDirectory_NoCommandLineSourceAndNoParseResult_UsesDefaultFolderUnderWorkingDirectory()
    {
        AggregatedConfiguration configuration = Create(
            new DictionaryConfigurationProvider(new() { ["commandLineOptions:results-directory:0"] = "from-provider" }));

        Assert.AreEqual(Path.Combine("work", AggregatedConfiguration.DefaultTestResultFolderName), configuration[PlatformConfigurationConstants.PlatformResultDirectory]);
    }

    // ---------------------------------------------------------------------
    // Child enumeration order
    // ---------------------------------------------------------------------
    [TestMethod]
    public void GetChildren_OrdersIntegerKeysNumericallyBeforeNamesAndNamesIgnoringCase()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            ["section:b"] = "1",
            ["section:10"] = "1",
            ["section:!bang"] = "1",
            ["section:2"] = "1",
            ["section:Abd"] = "1",
            ["section:9"] = "1",
            ["section:abc"] = "1",
            ["section:?question"] = "1",
        }));

        string[] keys = [.. configuration.GetChildren("section").Select(child => child.Key)];

        Assert.AreSequenceEqual(["2", "9", "10", "!bang", "?question", "abc", "Abd", "b"], keys);
    }

    [TestMethod]
    public void GetChildren_IntegerKeysWithSameValue_AreOrderedByTextIgnoringCase()
    {
        AggregatedConfiguration configuration = Create(new DictionaryConfigurationProvider(new()
        {
            ["section:1"] = "1",
            ["section:01"] = "1",
            ["section:001"] = "1",
        }));

        string[] keys = [.. configuration.GetChildren("section").Select(child => child.Key)];

        Assert.AreSequenceEqual(["001", "01", "1"], keys);
    }

    [TestMethod]
    public void GetChildren_NameFollowedByInteger_PutsIntegerFirstRegardlessOfInsertionOrder()
    {
        AggregatedConfiguration first = Create(new DictionaryConfigurationProvider(new()
        {
            ["section:!x"] = "1",
            ["section:7"] = "1",
        }));
        AggregatedConfiguration second = Create(new DictionaryConfigurationProvider(new()
        {
            ["section:7"] = "1",
            ["section:!x"] = "1",
        }));

        Assert.AreSequenceEqual(["7", "!x"], first.GetChildren("section").Select(child => child.Key).ToArray());
        Assert.AreSequenceEqual(["7", "!x"], second.GetChildren("section").Select(child => child.Key).ToArray());
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private static AggregatedConfiguration Create(params IConfigurationProvider[] providers)
        => Create(CommandLineParseResult.Empty, providers);

    private static AggregatedConfiguration Create(CommandLineParseResult parseResult, params IConfigurationProvider[] providers)
    {
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.GetCurrentTestApplicationDirectory()).Returns("work");
        return new AggregatedConfiguration(
            providers,
            module.Object,
            new Mock<IFileSystem>().Object,
            new Mock<IEnvironment>().Object,
            parseResult);
    }
}
