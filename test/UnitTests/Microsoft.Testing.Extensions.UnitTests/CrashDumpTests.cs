// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    [TestMethod]
    [DataRow("MyApp_%p_crash.dmp", "MyApp_*_crash.dmp")]
    [DataRow("%e_%p_crash.dmp", "*_*_crash.dmp")]
    [DataRow("%p%t_crash.dmp", "*_crash.dmp")]
    [DataRow("customdumpname.dmp", "customdumpname.dmp")]
    [DataRow("dump_%p_%t_%h.dmp", "dump_*_*_*.dmp")]
    [DataRow("trailing%", "trailing%")]
    public void ReplaceCrashDumpPlaceholdersWithWildcard_ConvertsPlaceholdersToWildcards(string fileName, string expected)
    {
        string actual = CrashDumpProcessLifetimeHandler.ReplaceCrashDumpPlaceholdersWithWildcard(fileName);
        Assert.AreEqual(expected, actual);
    }
}
