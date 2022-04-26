// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class UnitTestOutcomeHelperTests
    {
        private MSTestSettings adapterSettings;

        [TestInitialize]
        public void TestInit()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            this.adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Passed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, this.adapterSettings);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, this.adapterSettings);
            Assert.AreEqual(TestOutcome.NotFound, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, this.adapterSettings);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }
    }
}
