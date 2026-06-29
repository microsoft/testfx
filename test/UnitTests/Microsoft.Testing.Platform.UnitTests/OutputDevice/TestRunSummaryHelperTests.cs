// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class TestRunSummaryHelperTests
{
    [TestMethod]
    public void IsRunFailed_WhenTestsFailed_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 10, failedTests: 2, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void IsRunFailed_WhenBelowMinimumExpectedTests_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 3, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 10));

    [TestMethod]
    public void IsRunFailed_WhenZeroTests_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 0, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void IsRunFailed_WhenAllTestsSkipped_WithStrictPolicy_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0, ZeroTestsPolicy.Strict));

    [TestMethod]
    public void IsRunFailed_WhenAllTestsSkipped_ByDefault_ReturnsFalse()
        => Assert.IsFalse(TestRunSummaryHelper.IsRunFailed(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void IsRunFailed_WhenAllTestsSkipped_WithAllowSkippedPolicy_ReturnsFalse()
        => Assert.IsFalse(TestRunSummaryHelper.IsRunFailed(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0, ZeroTestsPolicy.AllowSkipped));

    [TestMethod]
    public void IsRunFailed_WhenZeroTests_WithAllowSkippedPolicy_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 0, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0, ZeroTestsPolicy.AllowSkipped));

    [TestMethod]
    public void IsRunFailed_WhenCancelled_ReturnsTrue()
        => Assert.IsTrue(TestRunSummaryHelper.IsRunFailed(totalTests: 10, failedTests: 0, skippedTests: 0, wasCancelled: true, minimumExpectedTests: 0));

    [TestMethod]
    public void IsRunFailed_WhenAllPassed_ReturnsFalse()
        => Assert.IsFalse(TestRunSummaryHelper.IsRunFailed(totalTests: 10, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void GetVerdictText_WhenCancelled_ReturnsAborted()
        => Assert.AreEqual(PlatformResources.Aborted, TestRunSummaryHelper.GetVerdictText(totalTests: 10, failedTests: 5, skippedTests: 0, wasCancelled: true, minimumExpectedTests: 0));

    [TestMethod]
    public void GetVerdictText_WhenBelowMinimumExpectedTests_ReturnsPolicyViolation()
    {
        string result = TestRunSummaryHelper.GetVerdictText(totalTests: 3, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 10);

        Assert.Contains("3", result, StringComparison.Ordinal);
        Assert.Contains("10", result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetVerdictText_WhenAllTestsSkipped_WithStrictPolicy_ReturnsZeroTestsRan()
        => Assert.AreEqual(PlatformResources.ZeroTestsRan, TestRunSummaryHelper.GetVerdictText(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0, zeroTestsPolicy: ZeroTestsPolicy.Strict));

    [TestMethod]
    public void GetVerdictText_WhenAllTestsSkipped_ByDefault_ReturnsPassed()
        => Assert.AreEqual($"{PlatformResources.Passed}!", TestRunSummaryHelper.GetVerdictText(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void GetVerdictText_WhenAllTestsSkipped_WithAllowSkippedPolicy_ReturnsPassed()
        => Assert.AreEqual($"{PlatformResources.Passed}!", TestRunSummaryHelper.GetVerdictText(totalTests: 5, failedTests: 0, skippedTests: 5, wasCancelled: false, minimumExpectedTests: 0, zeroTestsPolicy: ZeroTestsPolicy.AllowSkipped));

    [TestMethod]
    public void GetVerdictText_WhenZeroTests_WithAllowSkippedPolicy_ReturnsZeroTestsRan()
        => Assert.AreEqual(PlatformResources.ZeroTestsRan, TestRunSummaryHelper.GetVerdictText(totalTests: 0, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0, zeroTestsPolicy: ZeroTestsPolicy.AllowSkipped));

    [TestMethod]
    public void GetVerdictText_WhenZeroTests_ReturnsZeroTestsRan()
        => Assert.AreEqual(PlatformResources.ZeroTestsRan, TestRunSummaryHelper.GetVerdictText(totalTests: 0, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void GetVerdictText_WhenTestsFailed_ReturnsFailed()
        => Assert.AreEqual($"{PlatformResources.Failed}!", TestRunSummaryHelper.GetVerdictText(totalTests: 10, failedTests: 2, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void GetVerdictText_WhenAllPassed_ReturnsPassed()
        => Assert.AreEqual($"{PlatformResources.Passed}!", TestRunSummaryHelper.GetVerdictText(totalTests: 10, failedTests: 0, skippedTests: 0, wasCancelled: false, minimumExpectedTests: 0));

    [TestMethod]
    public void FormatSummaryText_ContainsVerdictAndAllCounts()
    {
        string result = TestRunSummaryHelper.FormatSummaryText(totalTests: 10, failedTests: 2, passedTests: 7, skippedTests: 1, wasCancelled: false, minimumExpectedTests: 0);

        Assert.Contains($"{PlatformResources.Failed}!", result, StringComparison.Ordinal);
        Assert.Contains($"{PlatformResources.TotalLowercase}: 10", result, StringComparison.Ordinal);
        Assert.Contains($"{PlatformResources.FailedLowercase}: 2", result, StringComparison.Ordinal);
        Assert.Contains($"{PlatformResources.SucceededLowercase}: 7", result, StringComparison.Ordinal);
        Assert.Contains($"{PlatformResources.SkippedLowercase}: 1", result, StringComparison.Ordinal);
    }
}
