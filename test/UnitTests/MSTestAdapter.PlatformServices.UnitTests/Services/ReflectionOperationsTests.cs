// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class ReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public ReflectionOperationsTests() => _reflectionOperations = new ReflectionOperations();

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        object[] attributes = GetMemberAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    private object[] GetMemberAttributes(Type type)
        => [.. _reflectionOperations.GetCustomAttributes(type).Where(x => x.GetType().FullName != "System.Runtime.CompilerServices.NullableContextAttribute")];

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        object[] attributes = GetMemberAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(1);

        string[] expectedAttributes = ["DummyA : base"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        object[] attributes = _reflectionOperations.GetCustomAttributes(type, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        object[] attributes = _reflectionOperations.GetCustomAttributes(type, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        GetAttributeValuePairs(attributes).SequenceEqual(expectedAttributes).Should().BeTrue();
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

        return [.. attribValuePairs];
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    public class DummyAAttribute : Attribute
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

