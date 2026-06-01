// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineOptionsProviderBaseTests
{
    [TestMethod]
    public async Task DefaultImplementations_ReturnExpectedValues()
    {
        var provider = new TestCommandLineOptionsProvider();

        Assert.AreEqual("uid", provider.Uid);
        Assert.AreEqual("1.2.3", provider.Version);
        Assert.AreEqual("display", provider.DisplayName);
        Assert.AreEqual("description", provider.Description);
        Assert.IsTrue(await provider.IsEnabledAsync().ConfigureAwait(false));

        ValidationResult optionValidationResult = await provider.ValidateOptionArgumentsAsync(provider.GetCommandLineOptions().Single(), []).ConfigureAwait(false);
        Assert.IsTrue(optionValidationResult.IsValid);

        ValidationResult commandLineValidationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(commandLineValidationResult.IsValid);
    }

    [TestMethod]
    public void GetCommandLineOptions_ReturnsConstructorOptions()
    {
        var provider = new TestCommandLineOptionsProvider();

        CommandLineOption option = provider.GetCommandLineOptions().Single();
        Assert.AreEqual("option", option.Name);
        Assert.AreEqual(ArgumentArity.Zero, option.Arity);
        Assert.AreEqual("description", option.Description);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Constructor_WhiteSpaceUid_DoesNotThrow(string uid)
    {
        var provider = new ConfigurableProvider(uid: uid, version: "1.0.0", displayName: "display", description: "description", options: []);
        Assert.AreEqual(uid, provider.Uid);
    }

    [TestMethod]
    public void Constructor_NullUid_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ConfigurableProvider(uid: null!, version: "1.0.0", displayName: "display", description: "description", options: []));
        Assert.AreEqual("uid", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Constructor_WhiteSpaceVersion_DoesNotThrow(string version)
    {
        var provider = new ConfigurableProvider(uid: "uid", version: version, displayName: "display", description: "description", options: []);
        Assert.AreEqual(version, provider.Version);
    }

    [TestMethod]
    public void Constructor_NullVersion_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ConfigurableProvider(uid: "uid", version: null!, displayName: "display", description: "description", options: []));
        Assert.AreEqual("version", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullDisplayName_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ConfigurableProvider(uid: "uid", version: "1.0.0", displayName: null!, description: "description", options: []));
        Assert.AreEqual("displayName", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullDescription_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ConfigurableProvider(uid: "uid", version: "1.0.0", displayName: "display", description: null!, options: []));
        Assert.AreEqual("description", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullCommandLineOptions_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ConfigurableProvider(uid: "uid", version: "1.0.0", displayName: "display", description: "description", options: null!));
        Assert.AreEqual("commandLineOptions", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_ExtensionOverload_UsesExtensionMetadata()
    {
        var provider = new ExtensionBackedProvider(new TestExtension());

        Assert.AreEqual("extension-uid", provider.Uid);
        Assert.AreEqual("2.0.0", provider.Version);
        Assert.AreEqual("extension-display", provider.DisplayName);
        Assert.AreEqual("extension-description", provider.Description);
    }

    [TestMethod]
    public void Constructor_NullExtension_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new ExtensionBackedProvider(null!));
        Assert.AreEqual("extension", exception.ParamName);
    }

    private sealed class TestCommandLineOptionsProvider : CommandLineOptionsProviderBase
    {
        public TestCommandLineOptionsProvider()
            : base(
                "uid",
                "1.2.3",
                "display",
                "description",
                [new("option", "description", ArgumentArity.Zero, false)])
        {
        }
    }

    private sealed class ConfigurableProvider : CommandLineOptionsProviderBase
    {
        public ConfigurableProvider(string uid, string version, string displayName, string description, IReadOnlyCollection<CommandLineOption> options)
            : base(uid, version, displayName, description, options)
        {
        }
    }

    private sealed class ExtensionBackedProvider : CommandLineOptionsProviderBase
    {
        public ExtensionBackedProvider(IExtension extension)
            : base(extension, [new("option", "description", ArgumentArity.Zero, false)])
        {
        }
    }

    private sealed class TestExtension : IExtension
    {
        public string Uid => "extension-uid";

        public string Version => "2.0.0";

        public string DisplayName => "extension-display";

        public string Description => "extension-description";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
