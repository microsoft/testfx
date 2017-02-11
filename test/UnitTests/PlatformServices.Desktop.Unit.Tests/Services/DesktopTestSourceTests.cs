// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopTestSourceTests
    {
        private TestSource testSource;

        [TestInitialize]
        public void TestInit()
        {
            this.testSource = new TestSource();
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainDllExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".dll");
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainExeExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".exe");
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainAppxExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".appx");
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(null, "DummySource"));
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null));
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(typeof(FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location));
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnFalseIfAnAssemblyIsNotReferencedInSource()
        {
            Assert.IsFalse(this.testSource.IsAssemblyReferenced(new AssemblyName("foobar"), Assembly.GetExecutingAssembly().Location));
        }
    }
}
