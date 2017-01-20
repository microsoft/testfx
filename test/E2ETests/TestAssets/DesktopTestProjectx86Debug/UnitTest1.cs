// Copyright (c) Microsoft. All rights reserved.

namespace SampleUnitTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTest1
    {
        /// <summary>
        /// The passing test.
        /// </summary>
        [TestMethod]
        public void PassingTest()
        {
            Assert.AreEqual(2, 2);
        }

        /// <summary>
        /// The failing test.
        /// </summary>
        [TestMethod]
        public void FailingTest()
        {
            Assert.AreEqual(2, 3);
        }

        /// <summary>
        /// The skipping test.
        /// </summary>
        [Ignore]
        [TestMethod]
        public void SkippingTest()
        {
        }
    }
}
