// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    [TestClass]
    public class TestSourceTests
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
        public void IsAssemblyReferencedShouldReturnTrueIfSourceOrAssemblyNameIsNull()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(null, null));
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(null, string.Empty));
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(new AssemblyName(), null));
        }

        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly"));
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
