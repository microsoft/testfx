// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpTests
{
    private HangDumpCommandLineProvider GetProvider()
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        _ = testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("FullPath");
        return new();
    }

    [TestMethod]
    public async Task IsValid_If_Timeout_Value_Has_CorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_Timeout_Value_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTimeoutOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
#if NETCOREAPP
    [DataRow("Triage")]
#endif
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("None")]
    public async Task IsValid_If_HangDumpType_Has_CorrectValue(string dumpType)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_HangDumpType_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                ExtensionResources.HangDumpTypeOptionInvalidType,
                "invalid",
                GetExpectedFormattedOptions()),
            validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public void HangDumpTypeOptionDescription_ListsValidValues()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionDescription, GetExpectedFormattedOptions()),
            option.Description);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task Missing_HangDumpMainOption_ShouldReturn_IsInvalid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual("You specified one or more hang dump parameters but did not enable it, add --hangdump to the command line", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task If_HangDumpMainOption_IsSpecified_ShouldReturn_IsValid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific quoting workaround for dotnet/diagnostics#5020")]
    public void GetDumpFileNames_WindowsPathWithSpaces_QuotesOnlyWriteDumpArgument()
    {
        string dumpFileName = @"C:\results directory with spaces\hangdump.dmp";

        HangDumpProcessLifetimeHandler.DumpFileNames dumpFileNames = HangDumpProcessLifetimeHandler.GetDumpFileNames(dumpFileName);

        Assert.AreEqual($"\"{dumpFileName}\"", dumpFileNames.WriteDumpFileName);
        Assert.AreEqual(dumpFileName, dumpFileNames.ArtifactDumpFileName);
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("Triage")]
    [DataRow("None")]
    public async Task IsValid_If_HangDumpTypeIfSupported_HasAnyKnownValue_RegardlessOfTfm(string dumpType)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_HangDumpTypeIfSupported_HasIncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        // The "-if-supported" variant lists the full set of dump types in its error so the user
        // is not misled into thinking values like Triage are unavailable on the current runtime.
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                ExtensionResources.HangDumpTypeOptionInvalidType,
                "invalid",
                "'Mini', 'Heap', 'Full', 'Triage', 'None'"),
            validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task Missing_HangDumpMainOption_WithHangDumpTypeIfSupported_ShouldReturn_IsInvalid()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Mini"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.MissingHangDumpMainOption, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task If_HangDumpTypeIfSupported_IsSpecified_WithHangDump_ShouldReturn_IsValid()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Triage"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task HangDumpType_And_HangDumpTypeIfSupported_AreMutuallyExclusive()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { HangDumpCommandLineProvider.HangDumpTypeOptionName, ["Mini"] },
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Heap"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTypeAndIfSupportedAreMutuallyExclusiveErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public void HangDumpTypeIfSupportedOption_IsRegisteredWithExactlyOneArity()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
    }

    [TestMethod]
    public void IsHangDumpTypeSupportedOnCurrentRuntime_ReturnsTrue_ForAlwaysAvailableTypes()
    {
        // Mini/Heap/Full/None are supported on every runtime the platform targets, so the
        // -if-supported variant must never trigger a runtime fallback for these values.
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Mini"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Heap"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Full"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("None"));
    }

    [TestMethod]
    public void IsHangDumpTypeSupportedOnCurrentRuntime_TriageMatchesCurrentTfm()
    {
        // 'Triage' is only available on .NET (Core), because the .NET Framework hang dump
        // path goes through MiniDumpWriteDump which has no equivalent flag.
#if NETCOREAPP
        bool expected = true;
#else
        bool expected = false;
#endif
        Assert.AreEqual(expected, HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Triage"));
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("None")]
    public void MapToSupportedDumpType_ReturnsRequested_WhenAlreadySupported(string value)
        // Supported values must round-trip unchanged so the user does not get a surprise
        // substitution when the runtime can actually honor their request.
        => Assert.AreEqual(value, HangDumpCommandLineProvider.MapToSupportedDumpType(value));

    [TestMethod]
    public void MapToSupportedDumpType_TriageOnNetFramework_FallsBackToMini()
        // The mapping intentionally prefers a "closest in size/intent" fallback over the
        // global default 'Full' so a user asking for the lightest dump does not end up with
        // the heaviest one. 'Mini' is the closest .NET Framework equivalent of 'Triage'.
#if NETCOREAPP
        => Assert.AreEqual("Triage", HangDumpCommandLineProvider.MapToSupportedDumpType("Triage"));
#else
        => Assert.AreEqual("Mini", HangDumpCommandLineProvider.MapToSupportedDumpType("Triage"));
#endif

#if NETCOREAPP
    private static string GetExpectedFormattedOptions() => "'Mini', 'Heap', 'Full', 'Triage', 'None'";
#else
    private static string GetExpectedFormattedOptions() => "'Mini', 'Heap', 'Full', 'None'";
#endif
}
