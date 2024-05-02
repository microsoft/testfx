// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Passed);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.InProgress);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Timeout);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        UTF.UnitTestOutcome frameworkOutcome = UTF.UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Error);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        UTF.UnitTestOutcome resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        UTF.UnitTestOutcome resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        UTF.UnitTestOutcome resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Failed);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        UTF.UnitTestOutcome resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Failed);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }
}
