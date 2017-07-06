// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTestOutcomeExtensionsTests
    {
        [TestMethod]
        public void ToUnitTestOutComeForPassedTestResultsConvertsToPassedUnitTestOutCome()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.Passed;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.Passed, convertedOutcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.Failed;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedOutcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.InProgress;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.InProgress, convertedOutcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.Inconclusive;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedOutcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.Timeout;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.Timeout, convertedOutcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
        {
            var frameworkOutcome = UTF.UnitTestOutcome.Unknown;
            var convertedOutcome = frameworkOutcome.ToUnitTestOutcome();

            Assert.AreEqual(AdapterTestOutcome.Error, convertedOutcome);
        }

        [TestMethod]
        public void GetMoreImportantOutcomeShouldReturnFailIfTwoOutcomesAreFailedAndInconclusive()
        {
            var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Inconclusive);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void GetMoreImportantOutcomeShouldReturnInconclusiveIfTwoOutcomesArePassedAndInconclusive()
        {
            var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Inconclusive);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, resultOutcome);
        }

        [TestMethod]
        public void GetMoreImportantOutcomeShouldReturnFailedIfTwoOutcomesArePassedAndFailed()
        {
            var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Passed, UTF.UnitTestOutcome.Failed);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void GetMoreImportantOutcomeShouldReturnFailedIfBothOutcomesAreFailed()
        {
            var resultOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(UTF.UnitTestOutcome.Failed, UTF.UnitTestOutcome.Failed);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, resultOutcome);
        }
    }
}
