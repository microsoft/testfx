// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Globalization;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class RetryTests
{
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32")]
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
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32.32")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionSingleIntegerArgumentErrorMessage, optionName), validateOptionsResult.ErrorMessage);
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
}
