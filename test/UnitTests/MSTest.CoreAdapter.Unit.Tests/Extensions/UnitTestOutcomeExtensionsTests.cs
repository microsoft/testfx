// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.Passed, convertedOutcome);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.Failed, convertedOutcome);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.InProgress, convertedOutcome);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedOutcome);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.Timeout, convertedOutcome);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Assert.AreEqual(AdapterTestOutcome.Error, convertedOutcome);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Inconclusive);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Inconclusive);
        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, resultOutcome);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Failed);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Failed);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
    }
}
