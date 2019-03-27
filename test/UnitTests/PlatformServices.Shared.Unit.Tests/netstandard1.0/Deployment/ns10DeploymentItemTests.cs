// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Deployment
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

    [TestClass]
    public class DeploymentItemTests
    {
        [TestMethod]
        public void EqualsShouldReturnFalseIfOtherItemIsNull()
        {
            DeploymentItem item = new DeploymentItem("e:\\temp\\temp1.dll");

            Assert.IsFalse(item.Equals(null));
        }

        [TestMethod]
        public void EqualsShouldReturnFalseIfOtherItemIsNotDeploymentItem()
        {
            DeploymentItem item = new DeploymentItem("e:\\temp\\temp1.dll");

            Assert.IsFalse(item.Equals(new DeploymentItemTests()));
        }

        [TestMethod]
        public void EqualsShouldReturnFalseIfSourcePathIsDifferent()
        {
            DeploymentItem item1 = new DeploymentItem("e:\\temp\\temp1.dll");
            DeploymentItem item2 = new DeploymentItem("e:\\temp\\temp2.dll");

            Assert.IsFalse(item1.Equals(item2));
        }

        [TestMethod]
        public void EqualsShouldReturnFalseIfRelativeOutputDirectoryIsDifferent()
        {
            DeploymentItem item1 = new DeploymentItem("e:\\temp\\temp1.dll", "foo1");
            DeploymentItem item2 = new DeploymentItem("e:\\temp\\temp1.dll", "foo2");

            Assert.IsFalse(item1.Equals(item2));
        }

        [TestMethod]
        public void EqualsShouldReturnTrueIfSourcePathDiffersByCase()
        {
            DeploymentItem item1 = new DeploymentItem("e:\\temp\\temp1.dll");
            DeploymentItem item2 = new DeploymentItem("e:\\temp\\Temp1.dll");

            Assert.IsTrue(item1.Equals(item2));
        }

        [TestMethod]
        public void EqualsShouldReturnTrueIfRelativeOutputDirectoryDiffersByCase()
        {
            DeploymentItem item1 = new DeploymentItem("e:\\temp\\temp1.dll", "foo1");
            DeploymentItem item2 = new DeploymentItem("e:\\temp\\temp1.dll", "Foo1");

            Assert.IsTrue(item1.Equals(item2));
        }

        [TestMethod]
        public void EqualsShouldReturnTrueIfSourceAndRelativeOutputDirectoryAreSame()
        {
            DeploymentItem item1 = new DeploymentItem("e:\\temp\\temp1.dll", "foo1");
            DeploymentItem item2 = new DeploymentItem("e:\\temp\\temp1.dll", "foo1");

            Assert.IsTrue(item1.Equals(item2));
        }

        [TestMethod]
        public void GetHashCodeShouldConsiderSourcePathAndRelativeOutputDirectory()
        {
            var sourcePath = "e:\\temp\\temp1.dll";
            var relativeOutputDirectory = "foo1";
            DeploymentItem item = new DeploymentItem(sourcePath, relativeOutputDirectory);

            Assert.AreEqual(sourcePath.GetHashCode() + relativeOutputDirectory.GetHashCode(), item.GetHashCode());
        }

        [TestMethod]
        public void ToStringShouldReturnDeploymentItemIfRelativeOutputDirectoryIsNotSpecified()
        {
            var sourcePath = "e:\\temp\\temp1.dll";
            DeploymentItem item = new DeploymentItem(sourcePath);

            Assert.AreEqual(string.Format(Resource.DeploymentItem, sourcePath), item.ToString());
        }

        [TestMethod]
        public void ToStringShouldReturnDeploymentItemAndRelativeOutputDirectory()
        {
            var sourcePath = "e:\\temp\\temp1.dll";
            var relativeOutputDirectory = "foo1";
            DeploymentItem item = new DeploymentItem(sourcePath, relativeOutputDirectory);

            Assert.AreEqual(string.Format(Resource.DeploymentItemWithOutputDirectory, sourcePath, relativeOutputDirectory), item.ToString());
        }
    }
}
