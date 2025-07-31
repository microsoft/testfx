// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = MSTest.PlatformServices.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.PlatformServices.Extensions.UnitTests;

public class UnitTestOutcomeExtensionsTests : TestContainer
{
    public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Passed);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.InProgress);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Timeout);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        UnitTestOutcome frameworkOutcome = UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Error);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Passed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        Verify(resultOutcome == UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        UnitTestOutcome resultOutcome = UnitTestOutcome.Failed.GetMoreImportantOutcome(UnitTestOutcome.Failed);
        Verify(resultOutcome == UnitTestOutcome.Failed);
    }
}
