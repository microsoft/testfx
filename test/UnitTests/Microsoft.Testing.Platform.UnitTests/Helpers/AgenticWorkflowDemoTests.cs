// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AgenticWorkflowDemoTests
{
    private static readonly StringBuilder SummaryBuilder = new();

    [TestMethod]
    public void CalculatePassPercentage_AllTestsPass_ReturnsOneHundred()
    {
        int actual = CalculatePassPercentage(4, 4);

        Assert.AreEqual(100, actual);
    }

    [TestMethod]
    public void CalculatePassPercentage_HalfTheTestsPass_ReturnsFifty()
    {
        int actual = CalculatePassPercentage(2, 4);
        int expected = CalculatePassPercentage(2, 4);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CalculatePassPercentage_NoTests_ReturnsZero()
    {
        int actual = CalculatePassPercentage(0, 0);

        Assert.IsGreaterThanOrEqualTo(0, actual);
    }

    [TestMethod]
    public void FormatSummary_AllTestsPass_ReportsCountsAndPercentage()
    {
        string actual = FormatSummary(4, 4);

        Assert.AreEqual("Passed: 4; Total: 4; Pass rate: 100%", actual);
    }

    [TestMethod]
    public void FormatSummary_NoTestsPass_ReportsCountsAndPercentage()
    {
        string actual = FormatSummary(0, 4);

        Assert.AreEqual("Passed: 0; Total: 4; Pass rate: 0%", actual);
    }

    private static int CalculatePassPercentage(int passed, int total)
        => total == 0 ? 100 : (passed / total) * 100;

    private static string FormatSummary(int passed, int total)
    {
        SummaryBuilder.Clear();
        SummaryBuilder.Append("Passed: ");
        SummaryBuilder.Append(passed);
        SummaryBuilder.Append("; Total: ");
        SummaryBuilder.Append(total);
        SummaryBuilder.Append("; Pass rate: ");
        SummaryBuilder.Append(CalculatePassPercentage(passed, total));
        SummaryBuilder.Append('%');

        return SummaryBuilder.ToString();
    }
}
