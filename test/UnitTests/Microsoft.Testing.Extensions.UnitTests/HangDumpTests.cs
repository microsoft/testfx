﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionInvalidType, "invalid"), validateOptionsResult.ErrorMessage);
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
}
