// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class PlatformCommandLineProviderTests
{
    [TestMethod]
    [DataRow("Trace")]
    [DataRow("Debug")]
    [DataRow("Information")]
    [DataRow("Warning")]
    [DataRow("Error")]
    [DataRow("Critical")]
    public async Task IsValid_If_Verbosity_Has_CorrectValue(string dumpType)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.DiagnosticVerbosityOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_Verbosity_Has_IncorrectValue()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.DiagnosticVerbosityOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_If_ClientPort_Is_Integer()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ClientPortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("32.32")]
    [DataRow("invalid")]
    public async Task IsInvalid_If_ClientPort_Is_Not_Integer(string clientPort)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ClientPortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [clientPort]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PlatformCommandLineProvider.ClientPortOptionKey), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_If_ExitOnProcessExit_Is_Integer()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ExitOnProcessExitOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("32.32")]
    [DataRow("invalid")]
    public async Task IsInvalid_If_ExitOnProcessExit_Is_Not_Integer(string pid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ExitOnProcessExitOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [pid]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitSingleArgument, PlatformCommandLineProvider.ExitOnProcessExitOptionKey), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_If_Diagnostics_Provided_With_Other_Diagnostics_Provided()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { PlatformCommandLineProvider.DiagnosticOptionKey, [] },
            { PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey, [] },
            { PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsValid_When_NoOptionSpecified()
    {
        var provider = new PlatformCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)]

    [TestMethod]
    public async Task IsNotValid_If_Diagnostics_Missing_When_OthersDiagnostics_Provided(string optionName)
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { optionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, optionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task IsValid_When_Both_DiscoverTests_MinimumExpectedTests_NotProvided(bool discoverTestsSet, bool minimumExpectedTestsSet)
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>();
        if (discoverTestsSet)
        {
            options.Add(PlatformCommandLineProvider.DiscoverTestsOptionKey, []);
        }

        if (minimumExpectedTestsSet)
        {
            options.Add(PlatformCommandLineProvider.MinimumExpectedTestsOptionKey, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_When_Both_DiscoverTests_MinimumExpectedTests_Provided()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { PlatformCommandLineProvider.DiscoverTestsOptionKey, [] },
            { PlatformCommandLineProvider.MinimumExpectedTestsOptionKey, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsNotValid_If_ExitOnProcess_Not_Running()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>();
        string pid = "-32";
        string[] args = [pid];
        options.Add(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, args);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.IsTrue(validateOptionsResult.ErrorMessage.StartsWith($"Invalid PID '{pid}'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow("1.5s")]
    [DataRow("2.0m")]
    [DataRow("0.5h")]
    [DataRow("10s")]
    [DataRow("30m")]
    [DataRow("1h")]
    public async Task IsValid_If_Timeout_Has_CorrectFormat_InvariantCulture(string timeout)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.TimeoutOptionKey);

        // Save current culture
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Test with various cultures to ensure invariant parsing works
            foreach (string cultureName in new[] { "en-US", "de-DE", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [timeout]).ConfigureAwait(false);
                Assert.IsTrue(validateOptionsResult.IsValid, $"Failed with culture {cultureName} and timeout {timeout}");
                Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
            }
        }
        finally
        {
            // Restore original culture
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow("1,5s")] // German decimal separator
    [DataRow("invalid")]
    [DataRow("1.5")]  // Missing unit
    [DataRow("abc.5s")]
    public async Task IsInvalid_If_Timeout_Has_IncorrectFormat(string timeout)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.TimeoutOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [timeout]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.PlatformCommandLineTimeoutArgumentErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task Timeout_Parsing_Uses_InvariantCulture_NotCurrentCulture()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.TimeoutOptionKey);

        // Save current culture
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        
        try
        {
            // Set culture to German where decimal separator is comma
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            
            // This should work because we use invariant culture (period as decimal separator)
            ValidationResult validResult = await provider.ValidateOptionArgumentsAsync(option, ["1.5s"]).ConfigureAwait(false);
            Assert.IsTrue(validResult.IsValid, "1.5s should be valid when using invariant culture");
            
            // This should fail because comma is not valid in invariant culture
            ValidationResult invalidResult = await provider.ValidateOptionArgumentsAsync(option, ["1,5s"]).ConfigureAwait(false);
            Assert.IsFalse(invalidResult.IsValid, "1,5s should be invalid when using invariant culture");
        }
        finally
        {
            // Restore original culture
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
