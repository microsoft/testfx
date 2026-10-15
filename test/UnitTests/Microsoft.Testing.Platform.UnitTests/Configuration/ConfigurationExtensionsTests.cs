// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationExtensionsTests
{
    private static string GetActualValueFromConfiguration(IConfiguration configuration, string key) => key switch
    {
        PlatformConfigurationConstants.PlatformResultDirectory => configuration.GetTestResultDirectory(),
        PlatformConfigurationConstants.PlatformCurrentWorkingDirectory => configuration.GetCurrentWorkingDirectory(),
        _ => throw new ArgumentException("Unsupported key."),
    };

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformResultDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    public void ConfigurationExtensions_TestedMethod_ReturnsExpectedPath(string key)
    {
        string expectedPath = Path.Combine("a", "b", "c");

        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[key])
            .Returns(expectedPath);

        Assert.AreEqual(expectedPath, GetActualValueFromConfiguration(configuration.Object, key));
    }

    [TestMethod]
    [DataRow(PlatformConfigurationConstants.PlatformResultDirectory)]
    [DataRow(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    public void ConfigurationExtensions_TestedMethod_ThrowsArgumentNullException(string key)
    {
        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[key])
            .Returns(value: null);

        // This should never happen in practice. We always have AggregatedConfiguration which will ensure non-null values.
        // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so asserting on the generic
        // type parameter would compare against this test assembly's copy and fail due to type identity mismatch across
        // assemblies. Assert on the full type name instead.
        Exception exception = Assert.Throws<Exception>(() => GetActualValueFromConfiguration(configuration.Object, key));
        Assert.AreEqual(typeof(global::System.Diagnostics.UnreachableException).FullName, exception.GetType().FullName);
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "Absent option")]
    [DataRow("false", false, DisplayName = "Explicit disabled option")]
    [DataRow("TRUE", true, DisplayName = "Case-insensitive presence marker")]
    [DataRow("literal", true, DisplayName = "Scalar argument")]
    [DataRow("", true, DisplayName = "Empty scalar argument")]
    public void CommandLineOptionFallback_InterpretsBarePresenceAndScalarValues(string? bare, bool expectedPresent)
    {
        IConfiguration configuration = CreateFallbackConfiguration(new() { ["commandLineOptions:filter"] = bare });

        Assert.AreEqual(expectedPresent, configuration.IsCommandLineOptionSet("--filter"));
        Assert.AreEqual(expectedPresent, configuration.TryGetCommandLineOptionArguments("--filter", out string[]? arguments));
        if (!expectedPresent)
        {
            Assert.IsNull(arguments);
        }
        else
        {
            string[] expectedArguments = bare == "TRUE" ? [] : [bare!];
            Assert.AreSequenceEqual(expectedArguments, arguments);
        }
    }

    [TestMethod]
    public void CommandLineOptionFallback_IndexedArgumentsOverrideBareDisableAndStopAtFirstGap()
    {
        IConfiguration configuration = CreateFallbackConfiguration(new()
        {
            ["commandLineOptions:filter"] = "false",
            ["commandLineOptions:filter:0"] = string.Empty,
            ["commandLineOptions:filter:1"] = "second",
            ["commandLineOptions:filter:3"] = "must not leak past gap",
        });

        Assert.IsTrue(configuration.IsCommandLineOptionSet("filter"));
        Assert.IsTrue(configuration.TryGetCommandLineOptionArguments("filter", out string[]? arguments));
        Assert.AreSequenceEqual(new[] { string.Empty, "second" }, arguments);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Scalar passive default")]
    [DataRow(true, DisplayName = "Indexed passive default")]
    public void CommandLineOptionDefaults_FallbackDoesNotActivateOptionAndExplicitArgumentsWin(bool indexed)
    {
        Dictionary<string, string?> values = new()
        {
            ["commandLineOptionDefaults:filter"] = "false",
        };
        if (indexed)
        {
            values["commandLineOptionDefaults:filter:0"] = "first";
            values["commandLineOptionDefaults:filter:1"] = "second";
            values["commandLineOptionDefaults:filter:3"] = "ignored";
        }

        IConfiguration configuration = CreateFallbackConfiguration(values);
        string[] expectedDefault = indexed ? ["first", "second"] : ["false"];

        Assert.IsFalse(configuration.IsCommandLineOptionSet("filter"));
        Assert.IsFalse(configuration.TryGetCommandLineOptionArguments("filter", out _));
        Assert.IsTrue(configuration.TryGetCommandLineOptionDefaultArguments("--filter", out string[]? defaults));
        Assert.AreSequenceEqual(expectedDefault, defaults);
        Assert.IsTrue(configuration.TryGetCommandLineOptionArgumentsOrDefault("filter", out string[]? resolved));
        Assert.AreSequenceEqual(expectedDefault, resolved);

        values["commandLineOptions:filter:0"] = "explicit";
        Assert.IsTrue(configuration.TryGetCommandLineOptionArgumentsOrDefault("--filter", out resolved));
        Assert.AreSequenceEqual(new[] { "explicit" }, resolved);
    }

    private static IConfiguration CreateFallbackConfiguration(Dictionary<string, string?> values)
    {
        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x[It.IsAny<string>()]).Returns<string>(key => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
