// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Tests for the unified read model helpers introduced by Option C of issue #6349:
/// <see cref="ConfigurationExtensions.IsCommandLineOptionSet"/> and
/// <see cref="ConfigurationExtensions.TryGetCommandLineOptionArguments"/>.
/// </summary>
[TestClass]
public sealed class CommandLineConfigurationExtensionsTests
{
    [TestMethod]
    public void IsCommandLineOptionSet_AbsentOption_ReturnsFalse()
    {
        IConfiguration configuration = BuildConfiguration([]);

        Assert.IsFalse(configuration.IsCommandLineOptionSet("hangdump"));
        Assert.IsFalse(configuration.IsCommandLineOptionSet("--hangdump"));
    }

    [TestMethod]
    public void IsCommandLineOptionSet_ZeroArityFlagSet_ReturnsTrue()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump", bool.TrueString),
        ]);

        Assert.IsTrue(configuration.IsCommandLineOptionSet("hangdump"));
    }

    [TestMethod]
    public void IsCommandLineOptionSet_BooleanFalseInJson_ReturnsFalse()
    {
        // Mirrors what JsonConfigurationFileParser produces for "hangdump": false.
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump", bool.FalseString),
        ]);

        Assert.IsFalse(configuration.IsCommandLineOptionSet("hangdump"));
    }

    [TestMethod]
    public void IsCommandLineOptionSet_NonBooleanScalar_ReturnsTrue()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump-timeout", "5m"),
        ]);

        Assert.IsTrue(configuration.IsCommandLineOptionSet("hangdump-timeout"));
    }

    [TestMethod]
    public void IsCommandLineOptionSet_IndexedEntriesPresent_ReturnsTrue()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:filter-uid:0", "a"),
            new("commandLineOptions:filter-uid:1", "b"),
        ]);

        Assert.IsTrue(configuration.IsCommandLineOptionSet("filter-uid"));
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_AbsentOption_ReturnsFalse()
    {
        IConfiguration configuration = BuildConfiguration([]);

        Assert.IsFalse(configuration.TryGetCommandLineOptionArguments("hangdump", out string[]? arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_ZeroArityFlag_ReturnsEmptyArray()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump", bool.TrueString),
        ]);

        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("hangdump", out string[]? arguments));
        Assert.IsNotNull(arguments);
        Assert.IsEmpty(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_BooleanFalseInJson_ReturnsFalse()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump", bool.FalseString),
        ]);

        Assert.IsFalse(configuration.TryGetCommandLineOptionArguments("hangdump", out string[]? arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_SingleScalarValue_ReturnsSingleElementArray()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:hangdump-timeout", "5m"),
        ]);

        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("hangdump-timeout", out string[]? arguments));
        Assert.IsNotNull(arguments);
        Assert.AreSequenceEqual(new[] { "5m" }, arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_IndexedEntries_ReturnsContiguousArray()
    {
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:filter-uid:0", "a"),
            new("commandLineOptions:filter-uid:1", "b"),
            new("commandLineOptions:filter-uid:2", "c"),
        ]);

        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("filter-uid", out string[]? arguments));
        Assert.IsNotNull(arguments);
        Assert.AreSequenceEqual(new[] { "a", "b", "c" }, arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArguments_IndexedEntriesWinOverBareKey()
    {
        // Should never happen with the in-memory provider, but make sure the precedence
        // is deterministic when both exist (e.g. CLI provider sets the indexed entries,
        // JSON layer happens to also set the bare key).
        IConfiguration configuration = BuildConfiguration([
            new("commandLineOptions:foo", "bare"),
            new("commandLineOptions:foo:0", "indexed"),
        ]);

        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("foo", out string[]? arguments));
        Assert.IsNotNull(arguments);
        Assert.AreSequenceEqual(new[] { "indexed" }, arguments);
    }

    [TestMethod]
    public async Task CommandLineHandler_DelegatesToConfiguration_WhenSupplied()
    {
        // End-to-end: a value present only in testconfig.json must be visible through
        // ICommandLineOptions when the handler is wired up with the unified IConfiguration.
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(
                Encoding.UTF8.GetBytes("{\"commandLineOptions\": {\"hangdump-timeout\": \"10m\", \"hangdump\": true, \"filter-uid\": [\"a\", \"b\"]}}")));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new CommandLineConfigurationSource());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        IConfiguration configuration = await configurationManager.BuildAsync(null, CommandLineParseResult.Empty);

        CommandLineHandler handler = new(
            CommandLineParseResult.Empty,
            extensionsCommandLineOptionsProviders: [],
            systemCommandLineOptionsProviders: [],
            testApplicationModuleInfo,
            new Mock<IRuntimeFeature>().Object,
            configuration);

        ICommandLineOptions options = handler;

        Assert.IsTrue(options.IsOptionSet("hangdump"));
        Assert.IsTrue(options.IsOptionSet("hangdump-timeout"));
        Assert.IsTrue(options.IsOptionSet("filter-uid"));

        Assert.IsTrue(options.TryGetOptionArgumentList("hangdump-timeout", out string[]? timeoutArgs));
        Assert.AreSequenceEqual(new[] { "10m" }, timeoutArgs);

        Assert.IsTrue(options.TryGetOptionArgumentList("hangdump", out string[]? hangdumpArgs));
        Assert.IsNotNull(hangdumpArgs);
        Assert.IsEmpty(hangdumpArgs);

        Assert.IsTrue(options.TryGetOptionArgumentList("filter-uid", out string[]? filterArgs));
        Assert.AreSequenceEqual(new[] { "a", "b" }, filterArgs);
    }

    private static IConfiguration BuildConfiguration(IReadOnlyList<KeyValuePair<string, string?>> entries)
    {
        Mock<IConfiguration> configuration = new();
        var map = entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        configuration.Setup(c => c[It.IsAny<string>()])
            .Returns<string>(key => map.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
