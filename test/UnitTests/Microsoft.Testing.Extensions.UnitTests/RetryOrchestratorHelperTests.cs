#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryOrchestratorHelperTests
{
    [TestMethod]
    public void RemoveOption_AllSupportedForms_RemovesOccurrencesAndValues()
    {
        List<string> arguments =
        [
            "before",
            "--target",
            "long-value",
            "-target",
            "short-value",
            "--target=long-equals",
            "long-equals-trailing-value",
            "--target:long-colon",
            "long-colon-trailing-value",
            "-target=short-equals",
            "short-equals-trailing-value",
            "-target:short-colon",
            "short-colon-trailing-value",
            "--other",
            "after",
        ];

        RetryOrchestratorHelper.RemoveOption(arguments, "target");

        Assert.HasCount(3, arguments);
        Assert.AreEqual("before", arguments[0]);
        Assert.AreEqual("--other", arguments[1]);
        Assert.AreEqual("after", arguments[2]);
    }

    [TestMethod]
    public void GetOptionArgumentIndex_WithLongForm_ReturnsOptionIndex()
    {
        string[] executableArguments = ["test.dll", "--target", "value"];

        Assert.AreEqual(1, RetryOrchestratorHelper.GetOptionArgumentIndex("target", executableArguments));
    }

    [TestMethod]
    public void GetOptionArgumentIndex_WithShortForm_ReturnsOptionIndex()
    {
        string[] executableArguments = ["test.dll", "-target", "value"];

        Assert.AreEqual(1, RetryOrchestratorHelper.GetOptionArgumentIndex("target", executableArguments));
    }

    [TestMethod]
    public void GetOptionArgumentIndex_WithBothForms_PrefersShortForm()
    {
        string[] executableArguments = ["--target", "long-value", "-target", "short-value"];

        Assert.AreEqual(2, RetryOrchestratorHelper.GetOptionArgumentIndex("target", executableArguments));
    }

    [TestMethod]
    public void GetOptionArgumentIndex_WithPrefixOnlyMatch_ReturnsMinusOne()
    {
        // Only exact matches count, so an option that merely starts with the searched name is not a hit.
        string[] executableArguments = ["test.dll", "--target-extended", "value"];

        Assert.AreEqual(-1, RetryOrchestratorHelper.GetOptionArgumentIndex("target", executableArguments));
    }

    [DataRow(0, "0ms")]
    [DataRow(500, "500ms")]
    [DataRow(999, "999ms")]
    [DataRow(1000, "1s")]
    [DataRow(1500, "1500ms")]
    [DataRow(60_000, "60s")]
    [DataRow(90_500, "90500ms")]
    [TestMethod]
    public void FormatDelay_RendersValueTheWayTheDelayOptionAcceptsIt(int milliseconds, string expected)
        => Assert.AreEqual(expected, RetryOrchestratorHelper.FormatDelay(TimeSpan.FromMilliseconds(milliseconds)));

    [DataRow(0, "0ms")]
    [DataRow(240, "240ms")]
    [DataRow(999, "999ms")]
    [DataRow(1000, "1s 000ms")]
    [DataRow(1240, "1s 240ms")]
    [DataRow(59_999, "59s 999ms")]
    [DataRow(60_000, "1m 00s")]
    [DataRow(123_000, "2m 03s")]
    [DataRow(3_600_000, "60m 00s")]
    [TestMethod]
    public void FormatDuration_RendersCompactHumanFriendlyDuration(int milliseconds, string expected)
        => Assert.AreEqual(expected, RetryOrchestratorHelper.FormatDuration(TimeSpan.FromMilliseconds(milliseconds)));
}
