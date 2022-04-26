// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PlatformServices.Desktop.ComponentTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using SampleFrameworkExtensions;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using OwnerV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.OwnerAttribute;
    using TestCategoryV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestPropertyV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute;

    [TestClass]
    public class ReflectionUtilityTests
    {
        private ReflectionUtility reflectionUtility;

        private Assembly testAsset;

        /// <summary>
        /// Dictionary of Assemblies discovered to date. Must be locked as it may
        /// be accessed in a multi-threaded context.
        /// </summary>
        private Dictionary<string, Assembly> resolvedAssemblies = new Dictionary<string, Assembly>();

        public ReflectionUtilityTests()
        {
            this.reflectionUtility = new ReflectionUtility();

            var currentAssemblyPath = Path.GetDirectoryName(typeof(ReflectionUtilityTests).Assembly.Location);
            var testAssetPath =
                Path.Combine(
                    Directory.GetParent(Directory.GetParent(Directory.GetParent(currentAssemblyPath).FullName).FullName).FullName,
                    "artifacts",
                    "TestAssets");
            this.testAsset = Assembly.ReflectionOnlyLoadFrom(Path.Combine(testAssetPath, "TestProjectForDiscovery.dll"));

            // Also pre-load the Test Framework assembly.
            Assembly.ReflectionOnlyLoadFrom(
                Path.Combine(testAssetPath, "Microsoft.VisualStudio.TestPlatform.TestFramework.dll"));

            // This is needed for System assemblies.
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(this.ReflectionOnlyOnResolve);
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributes()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : base", "Owner : base" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : derived", "Owner : derived" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(3, attribs.Length);

            // Notice that the Owner on the base method does not show up since it can only be defined once.
            var expectedAttribs = new string[] { "TestCategory : derived", "TestCategory : base", "Owner : derived" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
        {
            var tinfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(tinfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var tinfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(tinfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : a" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : a", "TestCategory : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributes()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(TestCategoryV2), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : base" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(TestCategoryV2), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : derived" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo =
                this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(TestCategoryV2), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : derived", "TestCategory : base", };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributesIncludingUserDefinedAttributes()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, null, true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(3, attribs.Length);

            var expectedAttribs = new string[] { "Duration : superfast", "TestCategory : base", "Owner : base" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributesIncludingUserDefinedAttributes()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(TestPropertyV2), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "Duration : superfast" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnArrayAttributesAsWell()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyTestMethod2");

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(CategoryArrayAttribute), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "CategoryAttribute : foo,foo2" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
        {
            var tinfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(tinfo, typeof(TestCategoryV2), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var tinfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(tinfo, typeof(TestCategoryV2), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : a" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

            var attribs = this.reflectionUtility.GetCustomAttributes(minfo, typeof(TestCategoryV2), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : a", "TestCategory : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
        {
            var asm = this.testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").Assembly;

            var attribs = this.reflectionUtility.GetCustomAttributes(asm, typeof(TestCategoryV2));

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "TestCategory : a1", "TestCategory : a2" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        private Assembly ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
        {
            string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

            // Put it in the resolved assembly cache so that if the Load call below
            // triggers another assembly resolution, then we don't end up in stack overflow.
            this.resolvedAssemblies[assemblyNameToLoad] = null;

            var assembly = Assembly.ReflectionOnlyLoad(assemblyNameToLoad);

            if (assembly != null)
            {
                this.resolvedAssemblies[assemblyNameToLoad] = assembly;
                return assembly;
            }

            return null;
        }

        private string[] GetAttributeValuePairs(object[] attributes)
        {
            var attribValuePairs = new List<string>();
            foreach (var attrib in attributes)
            {
                if (attrib is OwnerV2)
                {
                    var a = attrib as OwnerV2;
                    attribValuePairs.Add("Owner : " + a.Owner);
                }
                else if (attrib is TestCategoryV2)
                {
                    var a = attrib as TestCategoryV2;
                    attribValuePairs.Add("TestCategory : " + a.TestCategories.Aggregate((i, j) => { return i + "," + j; }));
                }
                else if (attrib is DurationAttribute)
                {
                    var a = attrib as DurationAttribute;
                    attribValuePairs.Add("Duration : " + a.Duration);
                }
                else if (attrib is CategoryArrayAttribute)
                {
                    var a = attrib as CategoryArrayAttribute;
                    attribValuePairs.Add("CategoryAttribute : " + a.Value.Aggregate((i, j) => { return i + "," + j; }));
                }
            }

            return attribValuePairs.ToArray();
        }
    }
}
