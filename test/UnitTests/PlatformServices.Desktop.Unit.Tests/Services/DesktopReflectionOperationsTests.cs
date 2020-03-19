// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;

    using System;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopReflectionOperationsTests
    {
        private ReflectionOperations reflectionOperations;

        public DesktopReflectionOperationsTests()
        {
            this.reflectionOperations = new ReflectionOperations();
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributes()
        {
            var minfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : base", "DummySingleA : base" };
            CollectionAssert.AreEqual(expectedAttribs, ReflectionUtilityTests.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
        {
            var tinfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(tinfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : ba" };
            CollectionAssert.AreEqual(expectedAttribs, ReflectionUtilityTests.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
        {
            var asm = typeof(ReflectionUtilityTests.DummyTestClass).Assembly;

            var attribs = this.reflectionOperations.GetCustomAttributes(asm, typeof(ReflectionUtilityTests.DummyAAttribute));

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a1", "DummyA : a2" };
            CollectionAssert.AreEqual(expectedAttribs, ReflectionUtilityTests.GetAttributeValuePairs(attribs));
        }
    }
}
