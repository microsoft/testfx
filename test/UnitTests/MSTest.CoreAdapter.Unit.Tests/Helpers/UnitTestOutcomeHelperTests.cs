// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTestOutcomeHelperTests
    {
        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Passed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeNone()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.NotFound, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, mapInconclusiveToFailed: false);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }
    }
}
