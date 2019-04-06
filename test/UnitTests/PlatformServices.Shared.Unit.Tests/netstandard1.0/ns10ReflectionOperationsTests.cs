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
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    [TestClass]
    public class ReflectionOperationsTests
    {
        private ReflectionOperations reflectionOperations;

        public ReflectionOperationsTests()
        {
            this.reflectionOperations = new ReflectionOperations();
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributes()
        {
            var minfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : base", "DummySingleA : base" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : derived", "DummySingleA : derived" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(3, attribs.Length);

            // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
            var expectedAttribs = new string[] { "DummyA : derived", "DummySingleA : derived", "DummyA : base", };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
        {
            var tinfo = typeof(DummyBaseTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(tinfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var tinfo = typeof(DummyTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(tinfo, false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a", "DummyA : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributes()
        {
            var minfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, typeof(DummyAAttribute), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : base" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, typeof(DummyAAttribute), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : derived" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, typeof(DummyAAttribute), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : derived", "DummyA : base", };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
        {
            var tinfo = typeof(DummyBaseTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(tinfo, typeof(DummyAAttribute), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
        {
            var tinfo = typeof(DummyTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(tinfo, typeof(DummyAAttribute), false);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(1, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
        {
            var minfo = typeof(DummyTestClass).GetTypeInfo();

            var attribs = this.reflectionOperations.GetCustomAttributes(minfo, typeof(DummyAAttribute), true);

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a", "DummyA : ba" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
        {
            var asm = typeof(DummyTestClass).GetTypeInfo().Assembly;

            var attribs = this.reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a1", "DummyA : a2" };
            CollectionAssert.AreEqual(expectedAttribs, this.GetAttributeValuePairs(attribs));
        }

        private string[] GetAttributeValuePairs(object[] attributes)
        {
            var attribValuePairs = new List<string>();
            foreach (var attrib in attributes)
            {
                if (attrib is DummySingleAAttribute)
                {
                    var a = attrib as DummySingleAAttribute;
                    attribValuePairs.Add("DummySingleA : " + a.Value);
                }
                else if (attrib is DummyAAttribute)
                {
                    var a = attrib as DummyAAttribute;
                    attribValuePairs.Add("DummyA : " + a.Value);
                }
            }

            return attribValuePairs.ToArray();
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
        public class DummyAAttribute : Attribute
        {
            public DummyAAttribute(string foo)
            {
                this.Value = foo;
            }

            public string Value { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false)]
        public class DummySingleAAttribute : Attribute
        {
            public DummySingleAAttribute(string foo)
            {
                this.Value = foo;
            }

            public string Value { get; set; }
        }

        [DummyA("ba")]
        private class DummyBaseTestClass
        {
            [DummyA("base")]
            [DummySingleA("base")]
            public virtual void DummyVTestMethod1()
            {
            }

            public void DummyBTestMethod2()
            {
            }
        }

        [DummyA("a")]
        private class DummyTestClass : DummyBaseTestClass
        {
            [DummyA("derived")]
            [DummySingleA("derived")]
            public override void DummyVTestMethod1()
            {
            }

            [DummySingleA("derived")]
            public void DummyTestMethod2()
            {
            }
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
