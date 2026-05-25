// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
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
}
