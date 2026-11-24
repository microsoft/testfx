// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class FlakyTestsReportingOptionsTests
{
    [TestMethod]
    public void IsEnabled_OptionAbsent_IsOn()
        => Assert.IsTrue(FlakyTestsReportingOptions.IsEnabled(new TestCommandLineOptions([])));

    [TestMethod]
    public void IsEnabled_OptionPassedWithoutArgument_StaysOn()
        => Assert.IsTrue(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { [FlakyTestsReportingOptions.ShowFlakyTestsOptionName] = [] })));

    [TestMethod]
    [DataRow("off")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    [DataRow("OFF")]
    public void IsEnabled_ExplicitOffValue_IsOff(string value)
        => Assert.IsFalse(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { [FlakyTestsReportingOptions.ShowFlakyTestsOptionName] = [value] })));

    [TestMethod]
    [DataRow("on")]
    [DataRow("true")]
    [DataRow("1")]
    [DataRow("enable")]
    [DataRow("unrecognized")]
    public void IsEnabled_NonOffValue_IsOn(string value)
        => Assert.IsTrue(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { [FlakyTestsReportingOptions.ShowFlakyTestsOptionName] = [value] })));

    [TestMethod]
    public void IsEnabled_OnlyTheFirstArgumentIsConsidered()
    {
        Assert.IsTrue(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { [FlakyTestsReportingOptions.ShowFlakyTestsOptionName] = ["on", "off"] })));
        Assert.IsFalse(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { [FlakyTestsReportingOptions.ShowFlakyTestsOptionName] = ["off", "on"] })));
    }

    [TestMethod]
    public void IsEnabled_OtherOptionOff_DoesNotAffectResult()
        => Assert.IsTrue(FlakyTestsReportingOptions.IsEnabled(
            new TestCommandLineOptions(new() { ["other-option"] = ["off"] })));
}
