// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

#pragma warning disable TPEXP // Command-line parse models are experimental.

[TestClass]
public sealed class ToolsHostTests
{
    [TestMethod]
    public void AggregateArguments_CombinesRepeatedOptionOccurrences()
    {
        CommandLineParseOption[] occurrences =
        [
            new("input", ["a.trx"]),
            new("input", ["b.trx"]),
        ];

        Assert.AreSequenceEqual(["a.trx", "b.trx"], ToolsHost.AggregateArguments(occurrences));
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsProviderFailure()
    {
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.Setup(candidate => candidate.ValidateCommandLineOptionsAsync(It.IsAny<ICommandLineOptions>()))
            .ReturnsAsync(ValidationResult.Invalid("invalid combination"));

        ValidationResult result = await ToolsHost.ValidateCommandLineOptionsAsync(
            [provider.Object],
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("invalid combination", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateToolProviders_DuplicateToolOption_ReturnsInvalid()
    {
        IToolCommandLineOptionsProvider first = CreateToolProvider("input");
        IToolCommandLineOptionsProvider second = CreateToolProvider("input");

        ValidationResult result = CommandLineOptionsValidator.ValidateToolProviders([first, second], []);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("declared by multiple providers", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateToolProviders_SystemOptionCollision_ReturnsInvalid()
    {
        IToolCommandLineOptionsProvider toolProvider = CreateToolProvider("system-option");
        var systemProvider = new Mock<ICommandLineOptionsProvider>();
        systemProvider.Setup(provider => provider.GetCommandLineOptions())
            .Returns([new("system-option", "system", ArgumentArity.Zero, isHidden: false)]);
        systemProvider.SetupGet(provider => provider.DisplayName).Returns("system");

        ValidationResult result = CommandLineOptionsValidator.ValidateToolProviders([toolProvider], [systemProvider.Object]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("reserved", result.ErrorMessage);
    }

    private static IToolCommandLineOptionsProvider CreateToolProvider(string optionName)
    {
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.Setup(candidate => candidate.GetCommandLineOptions())
            .Returns([new(optionName, optionName, ArgumentArity.ExactlyOne, isHidden: false)]);
        provider.SetupGet(candidate => candidate.DisplayName).Returns(Guid.NewGuid().ToString());
        return provider.Object;
    }
}
