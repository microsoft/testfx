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
        var frameworkOutcome = UTF.UnitTestOutcome.Passed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Passed);
    }

    public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Failed;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.InProgress;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.InProgress);
    }

    public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Inconclusive;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Timeout;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Timeout);
    }

    public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
    {
        var frameworkOutcome = UTF.UnitTestOutcome.Unknown;
        var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

        Verify(convertedOutcome == AdapterTestOutcome.Error);
    }

    public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Inconclusive);
        Verify(resultOutcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Failed);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }

    public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
    {
        var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Failed);
        Verify(resultOutcome == UTF.UnitTestOutcome.Failed);
    }
}
