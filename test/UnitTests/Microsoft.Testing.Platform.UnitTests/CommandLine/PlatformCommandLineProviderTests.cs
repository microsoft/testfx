// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestGroup]
public class PlatformCommandLineProviderTests : TestBase
{
    public PlatformCommandLineProviderTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [Arguments(PlatformCommandLineProvider.VerbosityTrace, true)]
    [Arguments(PlatformCommandLineProvider.VerbosityDebug, true)]
    [Arguments(PlatformCommandLineProvider.VerbosityInformation, true)]
    [Arguments(PlatformCommandLineProvider.VerbosityWarning, true)]
    [Arguments(PlatformCommandLineProvider.VerbosityError, true)]
    [Arguments(PlatformCommandLineProvider.VerbosityCritical, true)]
    [Arguments("invalid", false)]
    public async Task IsValid_If_Verbosity_Has_CorrectValue(string dumpType, bool isValid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.DiagnosticVerbosityOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.AreEqual(isValid, validateOptionsResult.IsValid);

        if (!isValid)
        {
            Assert.AreEqual(PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage, validateOptionsResult.ErrorMessage);
        }
    }

    [Arguments("32", true)]
    [Arguments("32.32", false)]
    [Arguments("invalid", false)]
    public async Task IsValid_If_Port_Is_Integer(string timeout, bool isValid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.PortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [timeout]).ConfigureAwait(false);
        Assert.AreEqual(isValid, validateOptionsResult.IsValid);

        if (!isValid)
        {
            Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PlatformCommandLineProvider.PortOptionKey), validateOptionsResult.ErrorMessage);
        }
    }

    [Arguments("32", true)]
    [Arguments("32.32", false)]
    [Arguments("invalid", false)]
    public async Task IsValid_If_ClientPort_Is_Integer(string timeout, bool isValid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ClientPortOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [timeout]).ConfigureAwait(false);
        Assert.AreEqual(isValid, validateOptionsResult.IsValid);

        if (!isValid)
        {
            Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PlatformCommandLineProvider.ClientPortOptionKey), validateOptionsResult.ErrorMessage);
        }
    }

    [Arguments("32", true)]
    [Arguments("32.32", false)]
    [Arguments("invalid", false)]
    public async Task IsValid_If_ExitOnProcessExit_Is_Integer(string timeout, bool isValid)
    {
        var provider = new PlatformCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == PlatformCommandLineProvider.ExitOnProcessExitOptionKey);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [timeout]).ConfigureAwait(false);
        Assert.AreEqual(isValid, validateOptionsResult.IsValid);

        if (!isValid)
        {
            Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitSingleArgument, PlatformCommandLineProvider.ExitOnProcessExitOptionKey), validateOptionsResult.ErrorMessage);
        }
    }

    public async Task IsValid_If_Diagnostics_Provided_With_Other_Diagnostics_Provided()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Mock<ICommandLineOptions>();
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOptionKey)).Returns(true);
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)).Returns(true);
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)).Returns(true);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(options.Object).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }

    public async Task IsValid_If_AllDiagnostics_Missing()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Mock<ICommandLineOptions>();
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOptionKey)).Returns(false);
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)).Returns(false);
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)).Returns(false);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(options.Object).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }

    [Arguments(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)]
    [Arguments(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)]

    public async Task IsNotValid_If_Diagnostics_Missing_When_OthersDiagnostics_Provided(string optionName)
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Mock<ICommandLineOptions>();
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiagnosticOptionKey)).Returns(false);
        _ = options.Setup(o => o.IsOptionSet(optionName)).Returns(true);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(options.Object).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, optionName), validateOptionsResult.ErrorMessage);
    }

    [Arguments(true, true, false)]
    [Arguments(true, false, true)]
    [Arguments(false, true, true)]
    [Arguments(false, false, true)]
    public async Task IsNotValid_When_Both_DiscoverTests_MinimumExpectedTests_Provided(bool discoverTestsSet, bool minimumExpectedTestsSet, bool isValid)
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Mock<ICommandLineOptions>();
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)).Returns(discoverTestsSet);
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.MinimumExpectedTestsOptionKey)).Returns(minimumExpectedTestsSet);

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(options.Object).ConfigureAwait(false);
        Assert.AreEqual(isValid, validateOptionsResult.IsValid);

        if (!isValid)
        {
            Assert.AreEqual(PlatformResources.PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests, validateOptionsResult.ErrorMessage);
        }
    }

    public async Task IsNotValid_If_ExitOnProcess_Not_Running()
    {
        var provider = new PlatformCommandLineProvider();
        var options = new Mock<ICommandLineOptions>();
        _ = options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.ExitOnProcessExitOptionKey)).Returns(true);
        string pid = "-32";
        string[]? args = [pid];
        _ = options.Setup(o => o.TryGetOptionArgumentList(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, out args));

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(options.Object).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.IsTrue(validateOptionsResult.ErrorMessage.StartsWith($"Invalid PID '{pid}'", StringComparison.OrdinalIgnoreCase));
    }
}
