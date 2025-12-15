// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Passed);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.InProgress);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Timeout);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Error);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UnitTestOutcome.Failed);
    }
}
