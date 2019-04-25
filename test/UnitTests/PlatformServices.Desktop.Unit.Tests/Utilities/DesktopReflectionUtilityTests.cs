// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities
{
    extern alias FrameworkV1;

    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
#pragma warning disable SA1649 // File name must match first type name
    public class ReflectionUtilityTests
#pragma warning restore SA1649 // File name must match first type name
    {
        private ReflectionUtility reflectionUtility;

        public ReflectionUtilityTests()
        {
            this.reflectionUtility = new ReflectionUtility();
        }

        [TestMethod]
        public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
        {
            var asm = typeof(DummyTestClass).GetTypeInfo().Assembly;

            var attribs = this.reflectionUtility.GetCustomAttributes(asm, typeof(DummyAAttribute));

            Assert.IsNotNull(attribs);
            Assert.AreEqual(2, attribs.Length);

            var expectedAttribs = new string[] { "DummyA : a1", "DummyA : a2" };
            CollectionAssert.AreEqual(expectedAttribs, GetAttributeValuePairs(attribs));
        }

        internal static string[] GetAttributeValuePairs(object[] attributes)
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

        [DummyA("ba")]
        public class DummyBaseTestClass
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
        public class DummyTestClass : DummyBaseTestClass
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

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
        public sealed class DummyAAttribute : Attribute
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
    }
}
