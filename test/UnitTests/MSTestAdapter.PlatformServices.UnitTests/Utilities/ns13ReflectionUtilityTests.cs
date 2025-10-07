﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

#pragma warning disable SA1649 // File name must match first type name
public class ReflectionUtilityTests : TestContainer
#pragma warning restore SA1649 // File name must match first type name
{
    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributesCore(methodInfo, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributesCore(methodInfo, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributesCore(type, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        IReadOnlyList<object> attributes = ReflectionUtility.GetCustomAttributesCore(type, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    internal static List<string> GetAttributeValuePairs(IEnumerable attributes)
    {
        var attribValuePairs = new List<string>();
        foreach (object? attrib in attributes)
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

        return attribValuePairs;
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
        public DummyAAttribute(string foo) => Value = foo;

        public string Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class DummySingleAAttribute : Attribute
    {
        public DummySingleAAttribute(string foo) => Value = foo;

        public string Value { get; set; }
    }
}
