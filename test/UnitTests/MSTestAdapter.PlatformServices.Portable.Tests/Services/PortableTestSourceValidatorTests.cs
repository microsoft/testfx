// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Portable.Tests
{
    extern alias FrameworkV1;

    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class PortableTestSourceValidatorTests
    {
        private TestSource testSourceValidator;

        [TestInitialize]
        public void TestInit()
        {
            this.testSourceValidator = new TestSource();
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainDllExtensions()
        {
            CollectionAssert.Contains(this.testSourceValidator.ValidSourceExtensions.ToList(), ".dll");
        }

        [TestMethod]
        public void ValidSourceExtensionsShouldContainExeExtensions()
        {
            CollectionAssert.Contains(this.testSourceValidator.ValidSourceExtensions.ToList(), ".exe");
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfSourceOrAssemblyNameIsNull()
        {
            Assert.IsTrue(this.testSourceValidator.IsAssemblyReferenced(null, null));
            Assert.IsTrue(this.testSourceValidator.IsAssemblyReferenced(null, ""));
            Assert.IsTrue(this.testSourceValidator.IsAssemblyReferenced(new AssemblyName(), null));
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
        {
            Assert.IsTrue(this.testSourceValidator.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly"));
        }
    }
}
