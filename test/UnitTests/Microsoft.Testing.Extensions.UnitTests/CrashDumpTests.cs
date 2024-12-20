// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpTests
{
    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    public async Task IsValid_If_CrashDumpType_Has_CorrectValue(string crashDumpType)
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [crashDumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvValid_If_CrashDumpType_Has_IncorrectValue()
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpTypeOptionInvalidType, "invalid"), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task CrashDump_CommandLineOptions_Are_AlwaysValid()
    {
        var provider = new CrashDumpCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }
}
