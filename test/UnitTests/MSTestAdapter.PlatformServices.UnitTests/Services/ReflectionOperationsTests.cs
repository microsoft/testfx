// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class ReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public ReflectionOperationsTests()
    {
        _reflectionOperations = new ReflectionOperations();
    }

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        var methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : base", "DummySingleA : base" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : derived", "DummySingleA : derived" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, true);

        Verify(attributes is not null);
        Verify(attributes.Length == 3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        var expectedAttributes = new string[] { "DummyA : derived", "DummySingleA : derived", "DummyA : base", };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        var typeInfo = typeof(DummyBaseTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var typeInfo = typeof(DummyTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : a" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : a", "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        var methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : base" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : derived" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : derived", "DummyA : base", };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        var typeInfo = typeof(DummyBaseTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(typeInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var typeInfo = typeof(DummyTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(typeInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        var expectedAttributes = new string[] { "DummyA : a" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = typeof(DummyTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : a", "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        var asm = typeof(DummyTestClass).GetTypeInfo().Assembly;

        var attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        var expectedAttributes = new string[] { "DummyA : a1", "DummyA : a2" };
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    private static string[] GetAttributeValuePairs(object[] attributes)
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

