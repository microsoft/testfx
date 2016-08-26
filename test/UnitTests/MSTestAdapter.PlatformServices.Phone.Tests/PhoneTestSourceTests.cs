// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Phone.UnitTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.Linq;

    [TestClass]
    public class PhoneTestSourceTests
    {
        private TestSource testSource;

        [TestInitialize]
        public void TestInit()
        {
            this.testSource = new TestSource();
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainXapExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".xap");
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainAppxExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".appx");
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainOnlyTwoExtensionTypes()
        {
            Assert.AreEqual(2, this.testSource.ValidSourceExtensions.Count());
        }
    }
}
