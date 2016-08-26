// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities
{
    extern alias FrameworkV1;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class FileUtilityTests
    {
        private FileUtility fileUtility;

        [TestInitialize]
        public void TestInit()
        {
            this.fileUtility = new FileUtility();
        }

        [TestMethod]
        public void ReplaceInvalidFileNameCharactersShouldReturnFileNameIfItHasNoInvalidChars()
        {
            var fileName = "galaxy";
            Assert.AreEqual(fileName, this.fileUtility.ReplaceInvalidFileNameCharacters(fileName));
        }

        [TestMethod]
        public void ReplaceInvalidFileNameCharactersShouldReplaceInvalidChars()
        {
            var fileName = "galaxy<>far:far?away";
            Assert.AreEqual("galaxy__far_far_away", this.fileUtility.ReplaceInvalidFileNameCharacters(fileName));
        }
    }
}
