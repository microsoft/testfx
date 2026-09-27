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
    [DataRow("minimal")]
    [DataRow("MINIMAL")]
    [DataRow("normal")]
    [DataRow("NORMAL")]
    [DataRow("detailed")]
    [DataRow("DETAILED")]
    public async Task ValidateOptionArguments_OutputOption_AcceptsPresetValuesCaseInsensitively(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.OutputOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsTrue(result.IsValid, $"Expected '{value}' to be a valid --output value, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("quiet")]
    [DataRow("diagnostic")]
    public async Task ValidateOptionArguments_OutputOption_RejectsUnsupportedValues(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.OutputOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsFalse(result.IsValid, $"Expected '{value}' to be rejected as a --output value but it was accepted.");
        Assert.AreEqual(TerminalResources.TerminalOutputOptionInvalidArgument, result.ErrorMessage);
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

    [TestMethod]
    public void GetCommandLineOptions_IncludesShowTestResultsOption()
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        Assert.AreEqual(ArgumentArity.OneOrMore, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    [TestMethod]
    [DataRow("passed")]
    [DataRow("PASSED")]
    [DataRow("failed")]
    [DataRow("skipped")]
    [DataRow("all")]
    [DataRow("none")]
    [DataRow(" passed ")]
    public async Task ValidateOptionArguments_ShowTestResultsOption_AcceptsSingleValidValue(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsTrue(result.IsValid, $"Expected '{value}' to be a valid --show-test-results value, but got: {result.ErrorMessage}");
    }

    // A single comma-separated token, several space-separated tokens (one per array entry, as the CLI parser
    // hands them over), and repeated option occurrences (also merged into one array by the CLI parser before
    // validation runs) must all be accepted and treated identically. Multiple raw tokens are DataRow-encoded as a
    // single '|'-joined string (rather than a string[] DataRow argument) to sidestep IDE0300's "simplify to
    // collection expression" suggestion, which does not apply cleanly inside an attribute argument list.
    [TestMethod]
    [DataRow("passed,failed")]
    [DataRow("passed|failed")]
    [DataRow("failed|skipped")]
    [DataRow("passed,failed,skipped")]
    [DataRow("passed|failed,skipped")]
    public async Task ValidateOptionArguments_ShowTestResultsOption_AcceptsCommaAndSpaceSeparatedCombinations(string joinedArguments)
    {
        string[] arguments = joinedArguments.Split('|');
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, arguments);

        Assert.IsTrue(result.IsValid, $"Expected '{string.Join(" / ", arguments)}' to be a valid --show-test-results value, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("passing")]
    [DataRow("Fails")]
    public async Task ValidateOptionArguments_ShowTestResultsOption_RejectsUnknownValue(string value)
    {
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, [value]);

        Assert.IsFalse(result.IsValid, $"Expected '{value}' to be rejected as a --show-test-results value but it was accepted.");
        Assert.AreEqual(TerminalResources.TerminalShowTestResultsOptionUnknownValueInvalidArgument, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(",")]
    [DataRow(" , ")]
    [DataRow(",|,")]
    public async Task ValidateOptionArguments_ShowTestResultsOption_RejectsEmptySelection(string joinedArguments)
    {
        string[] arguments = joinedArguments.Split('|');
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, arguments);

        Assert.IsFalse(result.IsValid, $"Expected '{string.Join(" / ", arguments)}' to be rejected as an empty --show-test-results selection but it was accepted.");
        Assert.AreEqual(TerminalResources.TerminalShowTestResultsOptionEmptySelectionInvalidArgument, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("all,failed")]
    [DataRow("none|passed")]
    [DataRow("all|none")]
    [DataRow("passed,all")]
    public async Task ValidateOptionArguments_ShowTestResultsOption_RejectsAllOrNoneCombinedWithOtherValues(string joinedArguments)
    {
        string[] arguments = joinedArguments.Split('|');
        CommandLineOption option = GetOption(TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption);

        ValidationResult result = await _provider.ValidateOptionArgumentsAsync(option, arguments);

        Assert.IsFalse(result.IsValid, $"Expected '{string.Join(" / ", arguments)}' to be rejected but it was accepted.");
        Assert.AreEqual(TerminalResources.TerminalShowTestResultsOptionAllOrNoneCombinedInvalidArgument, result.ErrorMessage);
    }

    // Ordinary values are unioned and de-duplicated (case-insensitively) into the resolved flag set.
    [TestMethod]
    public void TryParseShowTestResultsArguments_WhenOrdinaryValuesRepeatAndVary_UnionsAndDedupes()
    {
        bool success = TerminalTestReporterCommandLineOptionsProvider.TryParseShowTestResultsArguments(
            ["Failed", "skipped", "failed,SKIPPED"], out TestResultVisibility visibility, out ShowTestResultsValidationError error);

        Assert.IsTrue(success);
        Assert.AreEqual(ShowTestResultsValidationError.None, error);
        Assert.AreEqual(TestResultVisibility.Failed | TestResultVisibility.Skipped, visibility);
    }

    [TestMethod]
    public void TryParseShowTestResultsArguments_WhenAll_ResolvesToEveryOutcome()
    {
        bool success = TerminalTestReporterCommandLineOptionsProvider.TryParseShowTestResultsArguments(
            ["all"], out TestResultVisibility visibility, out _);

        Assert.IsTrue(success);
        Assert.AreEqual(TestResultVisibility.All, visibility);
    }

    [TestMethod]
    public void TryParseShowTestResultsArguments_WhenNone_ResolvesToNoOutcome()
    {
        bool success = TerminalTestReporterCommandLineOptionsProvider.TryParseShowTestResultsArguments(
            ["none"], out TestResultVisibility visibility, out _);

        Assert.IsTrue(success);
        Assert.AreEqual(TestResultVisibility.None, visibility);
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenOptionAbsent_ReturnsNull()
    {
        var options = new Helpers.TestCommandLineOptions([]);

        Assert.IsNull(TerminalTestReporterCommandLineOptionsProvider.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenOptionPresent_ReturnsParsedFlags()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["passed,skipped"],
        });

        Assert.AreEqual(TestResultVisibility.Passed | TestResultVisibility.Skipped, TerminalTestReporterCommandLineOptionsProvider.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenPassiveDefaultPresent_ReturnsParsedFlags()
    {
        var options = new Helpers.TestCommandLineOptions(
            [],
            new Dictionary<string, string[]>
            {
                [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["failed"],
            });

        Assert.AreEqual(TestResultVisibility.Failed, TerminalTestReporterCommandLineOptionsProvider.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenOptionAndPassiveDefaultPresent_OptionWins()
    {
        var options = new Helpers.TestCommandLineOptions(
            new Dictionary<string, string[]>
            {
                [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["skipped"],
            },
            new Dictionary<string, string[]>
            {
                [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["failed"],
            });

        Assert.AreEqual(TestResultVisibility.Skipped, TerminalTestReporterCommandLineOptionsProvider.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenPresentValueWasNotValidated_Throws()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["invalid"],
        });

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => TerminalTestReporterCommandLineOptionsProvider.GetShowTestResultsVisibility(options));
        Assert.AreEqual("The --show-test-results option was not validated.", exception.Message);
    }

    // TerminalOutputDevice.GetShowTestResultsVisibility resolution/precedence: an explicit or passive-default
    // --show-test-results value wins over --output; absent both, --output's 'Minimal' maps to 'failed', 'Detailed'
    // maps to 'all', and everything else (including no --output at all) maps to 'failed'+'skipped'. Since both
    // options are read independently by name from the same options bag, resolution cannot depend on CLI order.
    //
    // Comparisons cast to int: TerminalOutputDevice itself is not compiled into this test project (only the
    // OutputDevice\Terminal folder is, via the Compile Include in the .csproj), so its return value is the
    // Microsoft.Testing.Platform.dll copy of TestResultVisibility (reached through InternalsVisibleTo), a distinct
    // CLR type from the TestResultVisibility compiled locally into this assembly that TestResultVisibility.All
    // etc. below resolve to. Assert.AreEqual<T> can't unify the two identically-named-but-distinct enum types, so
    // the comparison is done on the underlying int instead.
    [TestMethod]
    public void GetShowTestResultsVisibility_WhenNeitherOptionSet_DefaultsToFailedAndSkipped()
    {
        var options = new Helpers.TestCommandLineOptions([]);

        Assert.AreEqual((int)(TestResultVisibility.Failed | TestResultVisibility.Skipped), (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenOutputDetailed_DefaultsToAll()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = ["detailed"],
        });

        Assert.AreEqual((int)TestResultVisibility.All, (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenOutputNormal_DefaultsToFailedAndSkipped()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = ["normal"],
        });

        Assert.AreEqual((int)(TestResultVisibility.Failed | TestResultVisibility.Skipped), (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    [DataRow("minimal")]
    [DataRow("MINIMAL")]
    public void GetShowTestResultsVisibility_WhenOutputMinimal_DefaultsToFailed(string outputValue)
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = [outputValue],
        });

        Assert.AreEqual((int)TestResultVisibility.Failed, (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenExplicitAndOutputDetailedBothSet_ExplicitWins()
    {
        // --output detailed alone would resolve to 'all'; the explicit, narrower --show-test-results must still
        // win, regardless of the fact that --output would have produced a different (wider) default.
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = ["detailed"],
            [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["passed"],
        });

        Assert.AreEqual((int)TestResultVisibility.Passed, (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenExplicitAndOutputNormalBothSet_ExplicitWins()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = ["normal"],
            [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["all"],
        });

        Assert.AreEqual((int)TestResultVisibility.All, (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    [TestMethod]
    public void GetShowTestResultsVisibility_WhenExplicitAndOutputMinimalBothSet_ExplicitWins()
    {
        var options = new Helpers.TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.OutputOption] = ["minimal"],
            [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["skipped"],
        });

        Assert.AreEqual((int)TestResultVisibility.Skipped, (int)TerminalOutputDevice.GetShowTestResultsVisibility(options));
    }

    private CommandLineOption GetOption(string name)
        => _provider.GetCommandLineOptions().Single(o => o.Name == name);
}
