// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice.Terminal;

[TestClass]
public sealed class TerminalTestReporterCommandLineOptionsProviderTests
{
    [TestMethod]
    [DataRow("auto")]
    [DataRow("on")]
    [DataRow("true")]
    [DataRow("enable")]
    [DataRow("1")]
    [DataRow("off")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    public async Task IsValid_If_Ansi_Has_CorrectValue(string ansiValue)
    {
        var provider = new TerminalTestReporterCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [ansiValue]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("AUTO")]
    [DataRow("On")]
    [DataRow("TRUE")]
    [DataRow("Enable")]
    [DataRow("OFF")]
    [DataRow("False")]
    [DataRow("DISABLE")]
    public async Task IsValid_If_Ansi_Has_CorrectValue_CaseInsensitive(string ansiValue)
    {
        var provider = new TerminalTestReporterCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [ansiValue]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("yes")]
    [DataRow("no")]
    [DataRow("2")]
    [DataRow("")]
    public async Task IsInvalid_If_Ansi_Has_IncorrectValue(string ansiValue)
    {
        var provider = new TerminalTestReporterCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TerminalTestReporterCommandLineOptionsProvider.AnsiOption);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [ansiValue]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.TerminalAnsiOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("normal")]
    [DataRow("detailed")]
    public async Task IsValid_If_Output_Has_CorrectValue(string outputValue)
    {
        var provider = new TerminalTestReporterCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TerminalTestReporterCommandLineOptionsProvider.OutputOption);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [outputValue]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("verbose")]
    public async Task IsInvalid_If_Output_Has_IncorrectValue(string outputValue)
    {
        var provider = new TerminalTestReporterCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TerminalTestReporterCommandLineOptionsProvider.OutputOption);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [outputValue]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.TerminalOutputOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }
}
