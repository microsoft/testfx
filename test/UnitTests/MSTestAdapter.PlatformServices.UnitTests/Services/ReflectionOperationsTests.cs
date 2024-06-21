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
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, true);

        Verify(attributes is not null);
        Verify(attributes.Length == 3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived", "DummyA : base"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        TypeInfo typeInfo = typeof(DummyBaseTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        TypeInfo typeInfo = typeof(DummyTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : a"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        TypeInfo methodInfo = typeof(DummyTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : base"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : derived"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : derived", "DummyA : base"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        TypeInfo typeInfo = typeof(DummyBaseTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(typeInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        TypeInfo typeInfo = typeof(DummyTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(typeInfo, typeof(DummyAAttribute), false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : a"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        TypeInfo methodInfo = typeof(DummyTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute), true);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        Verify(expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)));
    }

    private static string[] GetAttributeValuePairs(object[] attributes)
    {
        var attribValuePairs = new List<string>();
        foreach (object attrib in attributes)
        {
            if (attrib is DummySingleAAttribute dummySingleAAttribute)
            {
                attribValuePairs.Add("DummySingleA : " + dummySingleAAttribute.Value);
            }
            else if (attrib is DummyAAttribute dummyAAttribute)
            {
                attribValuePairs.Add("DummyA : " + dummyAAttribute.Value);
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

