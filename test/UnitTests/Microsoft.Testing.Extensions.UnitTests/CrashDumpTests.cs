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
    public async Task CrashDump_CommandLineOptions_Are_Valid_ByDefault()
    {
        var provider = new CrashDumpCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_Without_CrashDump_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_Alongside_CrashDump_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific rejection of --crash-report")]
    public async Task CrashReport_OnWindows_IsInvalid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.Contains("'--crash-report' is not supported on Windows", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific rejection of --crash-report")]
    public async Task CrashReport_WithCrashDump_OnWindows_IsInvalid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.Contains("'--crash-report' is not supported on Windows", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("MyApp_%p_crash.dmp", @"^MyApp_.*_crash\.dmp$")]
    [DataRow("%e_%p_crash.dmp", @"^.*_.*_crash\.dmp$")]
    [DataRow("%p%t_crash.dmp", @"^.*_crash\.dmp$")]
    [DataRow("customdumpname.dmp", @"^customdumpname\.dmp$")]
    [DataRow("dump_%p_%t_%h.dmp", @"^dump_.*_.*_.*\.dmp$")]
    [DataRow("trailing%", "^trailing%$")]
    // Glob metacharacters that may appear literally in a user-supplied filename must be escaped so they are
    // matched literally, not treated as wildcards. This guards against picking up unrelated dump files on
    // file systems that allow these characters in file names (e.g. Linux/macOS).
    [DataRow("my*dump_%p.dmp", @"^my\*dump_.*\.dmp$")]
    [DataRow("dump?_%p.dmp", @"^dump\?_.*\.dmp$")]
    public void BuildDumpFileNameRegexPattern_ConvertsPlaceholdersToRegex(string fileName, string expected)
    {
        string actual = CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegexPattern(fileName);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BuildDumpFileNameRegex_LiteralGlobMetacharactersInName_DoesNotOverMatch()
    {
        Regex regex = CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegex("my*dump_%p.dmp");
        Assert.IsTrue(regex.IsMatch("my*dump_123.dmp"), "Literal '*' must be matched literally.");
        Assert.IsFalse(regex.IsMatch("myXYZdump_123.dmp"), "Literal '*' must not act as a wildcard.");
        Assert.IsFalse(regex.IsMatch("mydump_123.dmp"), "Literal '*' must require at least the '*' character to be present.");
    }

    [TestMethod]
    [DataRow("dump_%p.dmp")]
    [DataRow("")]
    [DataRow("customdumpname.dmp")]
    public void GetDumpDirectory_WhenPatternHasNoDirectoryComponent_ReturnsCurrentDirectory(string pattern)
    {
        // The CrashDump runtime writes dumps to the current working directory when the configured pattern
        // contains no directory prefix. Previously the extension's enumeration was silently skipped in that
        // case because Path.GetDirectoryName returns "" (not null), and Directory.Exists("") is false.
        string actual = CrashDumpProcessLifetimeHandler.GetDumpDirectory(pattern);

        Assert.AreEqual(".", actual);
    }

    [TestMethod]
    public void GetDumpDirectory_WhenPatternHasDirectoryComponent_ReturnsDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dumps");
        string pattern = Path.Combine(directory, "dump_%p.dmp");

        string actual = CrashDumpProcessLifetimeHandler.GetDumpDirectory(pattern);

        Assert.AreEqual(directory, actual);
    }
}
