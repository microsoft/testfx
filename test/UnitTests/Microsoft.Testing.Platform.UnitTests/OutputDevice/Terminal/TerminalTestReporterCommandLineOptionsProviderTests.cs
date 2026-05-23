// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TerminalTestReporterCommandLineOptionsProviderTests
{
    private readonly TerminalTestReporterCommandLineOptionsProvider _provider = new();

    [TestMethod]
    [DataRow("auto")]
    [DataRow("AUTO")]
    [DataRow("on")]
    [DataRow("true")]
    [DataRow("enable")]
    [DataRow("1")]
    [DataRow("off")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    public async Task ValidateOptionArguments_AnsiOption_AcceptsValidValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsTrue(result.IsValid, $"Expected '{value}' to be a valid --ansi value, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("yes")]
    [DataRow("no")]
    [DataRow("enabled")]
    [DataRow("force")]
    [DataRow("2")]
    public async Task ValidateOptionArguments_AnsiOption_RejectsInvalidValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsFalse(result.IsValid, $"Expected '{value}' to be rejected as a --ansi value but it was accepted.");
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public void GetCommandLineOptions_IncludesAnsiOption()
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
    }

    [TestMethod]
    public void GetCommandLineOptions_StillIncludesNoAnsiOption()
    {
        // Validate backward compatibility: --no-ansi is preserved alongside --ansi.
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);

        Assert.AreEqual(ArgumentArity.Zero, option.Arity);
        Assert.IsFalse(option.IsHidden);
    }

    private CommandLineOption GetOption(string name)
        => _provider.GetCommandLineOptions().Single(o => o.Name == name);
}
