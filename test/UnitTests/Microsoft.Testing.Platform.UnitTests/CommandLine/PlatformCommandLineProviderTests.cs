// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestGroup]
public class PlatformCommandLineProviderTests : TestBase
{
    public PlatformCommandLineProviderTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [Arguments("Trace")]
    [Arguments("Debug")]
    [Arguments("Information")]
    [Arguments("Warning")]
    [Arguments("Error")]
    [Arguments("Critical")]
    public async Task IsValid_If_Verbosity_Has_CorrectValue(string dumpType)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.DiagnosticVerbosityOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    public async Task IsInvalid_If_Verbosity_Has_IncorrectValue()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.DiagnosticVerbosityOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage, validateOptionsResult.ErrorMessage);
    }

    public async Task IsValid_If_Port_Is_Integer()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.PortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments("32.32")]
    [Arguments("invalid")]
    public async Task IsValid_If_Port_Is_Not_Integer(string port)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.PortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [port]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PlatformCommandLineProvider.PortOptionKey), validateOptionsResult.ErrorMessage);
    }

    public async Task IsValid_If_ClientPort_Is_Integer()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ClientPortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments("32.32")]
    [Arguments("invalid")]
    public async Task IsInvalid_If_ClientPort_Is_Not_Integer(string clientPort)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ClientPortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [clientPort]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PlatformCommandLineProvider.ClientPortOptionKey), validateOptionsResult.ErrorMessage);
    }

    public async Task IsValid_If_ExitOnProcessExit_Is_Integer()
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ExitOnProcessExitOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments("32.32")]
    [Arguments("invalid")]
    public async Task IsInvalid_If_ExitOnProcessExit_Is_Not_Integer(string pid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ExitOnProcessExitOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [pid]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitSingleArgument, PlatformCommandLineProvider.ExitOnProcessExitOptionKey), validateOptionsResult.ErrorMessage);
    }

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

    public async Task IsValid_When_NoOptionSpecified()
    {
        var provider = new PlatformCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)]
    [Arguments(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)]

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

    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
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

    public async Task IsNotValid_If_ExitOnProcess_Not_Running()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Dictionary<string, string[]>();
        string pid = "-32";
        string[]? args = [pid];
        options.Add(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, args);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.IsTrue(validateOptionsResult.ErrorMessage.StartsWith($"Invalid PID '{pid}'", StringComparison.OrdinalIgnoreCase));
    }
}
