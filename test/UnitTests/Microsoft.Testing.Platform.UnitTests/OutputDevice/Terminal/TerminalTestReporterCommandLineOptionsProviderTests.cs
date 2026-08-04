// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
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
        Assert.IsTrue(option.IsBuiltIn);
    }

    [TestMethod]
    public void GetCommandLineOptions_StillIncludesNoAnsiOption()
    {
        // Validate backward compatibility: --no-ansi is preserved alongside --ansi.
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);

        Assert.AreEqual(ArgumentArity.Zero, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

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
    public async Task ValidateOptionArguments_ProgressOption_AcceptsValidValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ProgressOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsTrue(result.IsValid, $"Expected '{value}' to be a valid --progress value, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("yes")]
    [DataRow("no")]
    [DataRow("enabled")]
    [DataRow("force")]
    [DataRow("2")]
    public async Task ValidateOptionArguments_ProgressOption_RejectsInvalidValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ProgressOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsFalse(result.IsValid, $"Expected '{value}' to be rejected as a --progress value but it was accepted.");
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public void GetCommandLineOptions_IncludesProgressOption()
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ProgressOption);

        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    [TestMethod]
    public void GetCommandLineOptions_StillIncludesNoProgressOption()
    {
        // Validate backward compatibility: --no-progress is preserved alongside --progress.
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption);

        Assert.AreEqual(ArgumentArity.Zero, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    [TestMethod]
    [DataRow("auto", true)]
    [DataRow("on", true)]
    [DataRow("off", false)]
    public void IsProgressEnabled_ProgressOption_ReturnsRequestedState(string argument, bool expected)
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = [argument],
        });

        Assert.AreEqual(expected, TerminalTestReporterCommandLineOptionsProvider.IsProgressEnabled(options));
    }

    [TestMethod]
    public void IsProgressEnabled_NoProgressOption_ReturnsFalse()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.NoProgressOption] = [],
        });

        Assert.IsFalse(TerminalTestReporterCommandLineOptionsProvider.IsProgressEnabled(options));
    }

    [TestMethod]
    public void IsProgressEnabled_NoProgressOptions_ReturnsTrue()
    {
        var options = new Helpers.TestCommandLineOptions([]);

        Assert.IsTrue(TerminalTestReporterCommandLineOptionsProvider.IsProgressEnabled(options));
    }

    [TestMethod]
    public void IsProgressEnabled_ProgressOption_TakesPrecedenceOverNoProgressOption()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["on"],
            [TerminalTestReporterCommandLineOptionsProvider.NoProgressOption] = [],
        });

        Assert.IsTrue(TerminalTestReporterCommandLineOptionsProvider.IsProgressEnabled(options));
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("5")]
    [DataRow("100")]
    public async Task ValidateOptionArguments_ShowSlowestTestsOption_AcceptsPositiveIntegers(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowSlowestTestsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsTrue(result.IsValid, $"Expected '{value}' to be a valid --show-slowest-tests value, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("abc")]
    [DataRow("1.5")]
    [DataRow("")]
    public async Task ValidateOptionArguments_ShowSlowestTestsOption_RejectsInvalidValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowSlowestTestsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsFalse(result.IsValid, $"Expected '{value}' to be rejected as a --show-slowest-tests value but it was accepted.");
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArguments_ShowSlowestTestsOption_RejectsMultipleArguments()
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowSlowestTestsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, ["1", "2"]);

        Assert.IsFalse(result.IsValid, "Expected --show-slowest-tests to reject more than one argument.");
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public void GetCommandLineOptions_IncludesShowSlowestTestsOption()
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowSlowestTestsOption);

        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    // Wiring test: the parsed --show-slowest-tests value must reach the reporter via
    // TerminalOutputDevice.GetSlowestTestsCount (which feeds TerminalTestReporterOptions.SlowestTestsCount), so a
    // validation/parse regression can't leave a help-only option.
    [TestMethod]
    [DataRow("1", 1)]
    [DataRow("5", 5)]
    [DataRow("100", 100)]
    public void GetSlowestTestsCount_WhenOptionSetToPositiveInteger_ReturnsThatCount(string argument, int expected)
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ShowSlowestTestsOption] = [argument],
        });

        Assert.AreEqual(expected, TerminalOutputDevice.GetSlowestTestsCount(options));
    }

    [TestMethod]
    public void GetSlowestTestsCount_WhenOptionAbsent_ReturnsZero()
    {
        var options = new Helpers.TestCommandLineOptions([]);

        Assert.AreEqual(0, TerminalOutputDevice.GetSlowestTestsCount(options));
    }

    private CommandLineOption GetOption(string name)
        => _provider.GetCommandLineOptions().Single(o => o.Name == name);
}
