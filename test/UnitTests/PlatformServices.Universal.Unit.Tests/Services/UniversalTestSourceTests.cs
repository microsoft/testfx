// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UWP.UnitTests
{
    extern alias FrameworkV1;

    using System.Linq;
    using System.Reflection;
    using FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The universal test source validator tests.
    /// </summary>
    [TestClass]
    public class UniversalTestSourceTests
    {
        private TestSource testSource;

        /// <summary>
        /// The test initialization.
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            this.testSource = new TestSource();
        }

        /// <summary>
        /// The valid source extensions should contain .dll as extension.
        /// </summary>
        [TestMethod]
        public void ValidSourceExtensionsShouldContainDllExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".dll");
        }

        /// <summary>
        /// The valid source extensions should contain .exe as extension.
        /// </summary>
        [TestMethod]
        public void ValidSourceExtensionsShouldContainExeExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".exe");
        }

        /// <summary>
        /// The valid source extensions should contain .appx as extension.
        /// </summary>
        [TestMethod]
        public void ValidSourceExtensionsShouldContainAppxExtensions()
        {
            CollectionAssert.Contains(this.testSource.ValidSourceExtensions.ToList(), ".appx");
        }

        /// <summary>
        /// The is assembly referenced should return true if assembly name is null.
        /// </summary>
        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(null, "DummySource"));
        }

        /// <summary>
        /// The is assembly referenced should return true if source is null.
        /// </summary>
        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null));
        }

        /// <summary>
        /// The is assembly referenced should return true if an assembly is referenced in source.
        /// </summary>
        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(typeof(TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location));
        }

        /// <summary>
        /// The is assembly referenced should return false if an assembly is not referenced in source.
        /// </summary>
        [TestMethod]
        public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
        {
            Assert.IsTrue(this.testSource.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly"));
        }
    }
}
