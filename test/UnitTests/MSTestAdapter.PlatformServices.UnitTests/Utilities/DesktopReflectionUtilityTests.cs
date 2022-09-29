﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

#pragma warning disable SA1649 // File name must match first type name
public class ReflectionUtilityTests : TestContainer
#pragma warning restore SA1649 // File name must match first type name
{
    private readonly ReflectionUtility _reflectionUtility;

    public ReflectionUtilityTests()
    {
        _reflectionUtility = new ReflectionUtility();
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        var asm = typeof(DummyTestClass).GetTypeInfo().Assembly;

        var attributes = ReflectionUtility.GetCustomAttributes(asm, typeof(DummyAAttribute));

        Verify(attributes is not null);
        Verify(2 == attributes.Length);

        var expectedAttributes = new string[] { "DummyA : a1", "DummyA : a2" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
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
            Value = foo;
        }

        public string Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false)]
    public class DummySingleAAttribute : Attribute
    {
        public DummySingleAAttribute(string foo)
        {
            Value = foo;
        }

        public string Value { get; set; }
    }
}
#endif
