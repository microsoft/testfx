// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationExtensionsOptionDefaultsTests
{
    [TestMethod]
    public void TryGetCommandLineOptionDefaultArguments_AggregatedConfiguration_ReturnsDeclaredDefaults()
    {
        AggregatedConfiguration configuration = CreateAggregated(new DictionaryConfigurationProvider(new()
        {
            ["commandLineOptionDefaults:opt:0"] = "a",
            ["commandLineOptionDefaults:opt:1"] = "b",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionDefaultArguments("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["a", "b"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultArguments_AggregatedConfigurationWithoutDefault_ReturnsFalse()
    {
        AggregatedConfiguration configuration = CreateAggregated(new DictionaryConfigurationProvider(new()
        {
            ["commandLineOptions:opt:0"] = "active-only",
        }));

        Assert.IsFalse(configuration.TryGetCommandLineOptionDefaultArguments("opt", out string[]? arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultArguments_GenericConfigurationWithoutDefault_ReturnsFalse()
    {
        IConfiguration configuration = CreateGeneric([]);

        Assert.IsFalse(configuration.TryGetCommandLineOptionDefaultArguments("opt", out string[]? arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionDefaultArguments_GenericConfiguration_ReadsIndexedThenScalarDefaults()
    {
        IConfiguration indexed = CreateGeneric(new()
        {
            ["commandLineOptionDefaults:opt:0"] = "a",
            ["commandLineOptionDefaults:opt:1"] = "b",
            ["commandLineOptionDefaults:opt"] = "ignored-scalar",
        });
        IConfiguration scalar = CreateGeneric(new() { ["commandLineOptionDefaults:opt"] = "s" });

        Assert.IsTrue(indexed.TryGetCommandLineOptionDefaultArguments("opt", out string[]? indexedArguments));
        Assert.AreSequenceEqual(["a", "b"], indexedArguments);
        Assert.IsTrue(scalar.TryGetCommandLineOptionDefaultArguments("opt", out string[]? scalarArguments));
        Assert.AreSequenceEqual(["s"], scalarArguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArgumentsOrDefault_ExplicitArgumentsBeatDefaults()
    {
        AggregatedConfiguration configuration = CreateAggregated(new DictionaryConfigurationProvider(new()
        {
            ["commandLineOptions:opt:0"] = "explicit",
            ["commandLineOptionDefaults:opt:0"] = "default",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionArgumentsOrDefault("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["explicit"], arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArgumentsOrDefault_ExplicitDisable_IsNotReplacedByDefault()
    {
        AggregatedConfiguration configuration = CreateAggregated(new DictionaryConfigurationProvider(new()
        {
            ["commandLineOptions:opt"] = "false",
            ["commandLineOptionDefaults:opt:0"] = "default",
        }));

        Assert.IsFalse(configuration.TryGetCommandLineOptionArgumentsOrDefault("opt", out string[]? arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetCommandLineOptionArgumentsOrDefault_NoExplicitValue_FallsBackToDefault()
    {
        AggregatedConfiguration configuration = CreateAggregated(new DictionaryConfigurationProvider(new()
        {
            ["commandLineOptionDefaults:opt:0"] = "default",
        }));

        Assert.IsTrue(configuration.TryGetCommandLineOptionArgumentsOrDefault("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["default"], arguments);
    }

    private static AggregatedConfiguration CreateAggregated(IConfigurationProvider provider)
        => new(
            [provider],
            new Mock<ITestApplicationModuleInfo>().Object,
            new Mock<IFileSystem>().Object,
            new Mock<IEnvironment>().Object,
            CommandLineParseResult.Empty);

    private static IConfiguration CreateGeneric(Dictionary<string, string?> values)
    {
        Mock<IConfiguration> configuration = new();
        configuration.Setup(c => c[It.IsAny<string>()]).Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
