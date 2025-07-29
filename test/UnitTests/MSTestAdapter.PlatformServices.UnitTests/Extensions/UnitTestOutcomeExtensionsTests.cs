// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Passed);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.InProgress);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Timeout);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        convertedOutcome.Should().Be(AdapterTestOutcome.Error);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UTF.UnitTestOutcome.Failed.GetMoreImportantOutcome(UTF.UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UTF.UnitTestOutcome.Passed.GetMoreImportantOutcome(UTF.UnitTestOutcome.Inconclusive);
        resultOutcome.Should().Be(UTF.UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        UnitTestOutcome resultOutcome = UTF.UnitTestOutcome.Passed.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        UnitTestOutcome resultOutcome = UTF.UnitTestOutcome.Failed.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed);
        resultOutcome.Should().Be(UTF.UnitTestOutcome.Failed);
    }
}
