#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class RetryTests
{
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "100")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "0")]
    [TestMethod]
    public async Task IsValid_If_CorrectInteger_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "-1")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "-1")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_NegativeValue_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionNonNegativeIntegerArgumentErrorMessage, optionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow("invalid")]
    [DataRow("32.32")]
    [DataRow("-1")]
    [DataRow("101")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_OutOfRangeValue_Is_Provided_For_MaxPercentageOption(string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsMaxPercentageOptionIntegerBetween0And100ArgumentErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_MaxTests_BothProvided()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsPercentageAndCountCannotBeMixedErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxTests_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow(true, false)]
    [DataRow(false, true)]
    [TestMethod]
    public async Task IsValid_When_TestOption_Provided_With_Either_MaxPercentage_MaxTests_Provided(bool isMaxPercentageSet, bool isMaxTestsSet)
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, [] },
        };
        if (isMaxPercentageSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, []);
        }

        if (isMaxTestsSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("0")]
    [DataRow("0s")]
    [DataRow("200")]
    [DataRow("1s")]
    [DataRow("2.5m")]
    [DataRow("1h")]
    [TestMethod]
    public async Task IsValid_If_CorrectTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("invalid")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("25d")]
    [TestMethod]
    public async Task IsInvalid_If_InvalidTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(Policy.Resources.ExtensionResources.RetryFailedTestsDelayOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_DelayOption_Provided_But_RetryOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_DelayOption_Provided_With_RetryOption()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, ["3"] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }
}
