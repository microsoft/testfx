// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

/// <summary>
/// Tests for ReflectionOperations which provides platform-specific reflection operations.
/// </summary>
public class ReflectionUtilityTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations = new();

#if NETFRAMEWORK
    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        expectedAttributes.SequenceEqual(GetAttributeValuePairs(attributes)).Should().BeTrue();
    }
#endif

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        object[]? attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        GetAttributeValuePairs(attributes!).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        object[]? attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        // Notice that the DummySingleA on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["DummyA : derived", "DummySingleA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes!).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        object[]? attributes = _reflectionOperations.GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        // Filter to only our test attributes (excludes compiler-generated attributes like NullableContextAttribute)
        List<string> testAttributes = GetAttributeValuePairs(attributes!);
        testAttributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : ba"];
        testAttributes.Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        object[]? attributes = _reflectionOperations.GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        // Filter to only our test attributes (excludes compiler-generated attributes like NullableContextAttribute)
        List<string> testAttributes = GetAttributeValuePairs(attributes!);
        testAttributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        testAttributes.Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(DummyBaseTestClass).GetMethod("DummyVTestMethod1")!;

        DummyAAttribute[] attributes = _reflectionOperations.GetAttributes<DummyAAttribute>(methodInfo).ToArray();

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("DummyVTestMethod1")!;

        DummyAAttribute[] attributes = _reflectionOperations.GetAttributes<DummyAAttribute>(methodInfo).ToArray();

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : derived", "DummyA : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(DummyBaseTestClass);

        DummyAAttribute[] attributes = _reflectionOperations.GetAttributes<DummyAAttribute>(type).ToArray();

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = typeof(DummyTestClass);

        DummyAAttribute[] attributes = _reflectionOperations.GetAttributes<DummyAAttribute>(type).ToArray();

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["DummyA : a", "DummyA : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnMSTestMethodShouldReturnAllAttributes()
    {
        MethodInfo method = typeof(MSTestAttributeBaseClass).GetMethod(nameof(MSTestAttributeBaseClass.AttributeMethod))!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(method);

        GetMSTestAttributeValuePairs(attributes).Should().Equal("Owner : base", "TestCategory : base");
    }

    public void GetCustomAttributesOnMSTestOverrideShouldHonorAttributeUsage()
    {
        MethodInfo method = typeof(MSTestAttributeDerivedClass).GetMethod(nameof(MSTestAttributeDerivedClass.AttributeMethod))!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(method);

        // Owner allows only one value; categories from both the override and base method are retained.
        GetMSTestAttributeValuePairs(attributes).Should().Equal("Owner : derived", "TestCategory : derived", "TestCategory : base");
    }

    public void GetCustomAttributesOnMSTestTypeShouldReturnAllAttributes()
    {
        object[] attributes = _reflectionOperations.GetCustomAttributes(typeof(MSTestAttributeBaseClass));

        GetMSTestAttributeValuePairs(attributes).Should().Equal("TestCategory : ba");
    }

    public void GetCustomAttributesOnMSTestDerivedTypeShouldIncludeInheritedAttributes()
    {
        object[] attributes = _reflectionOperations.GetCustomAttributes(typeof(MSTestAttributeDerivedClass));

        GetMSTestAttributeValuePairs(attributes).Should().Equal("TestCategory : a", "TestCategory : ba");
    }

    public void GetMSTestCategoriesOnMethodShouldReturnDeclaredAttribute()
    {
        MethodInfo method = typeof(MSTestAttributeBaseClass).GetMethod(nameof(MSTestAttributeBaseClass.AttributeMethod))!;

        TestCategoryAttribute[] attributes = _reflectionOperations.GetAttributes<TestCategoryAttribute>(method).ToArray();

        attributes.SelectMany(attribute => attribute.TestCategories).Should().Equal("base");
    }

    public void GetMSTestCategoriesOnOverrideShouldIncludeInheritedAttributes()
    {
        MethodInfo method = typeof(MSTestAttributeDerivedClass).GetMethod(nameof(MSTestAttributeDerivedClass.AttributeMethod))!;

        TestCategoryAttribute[] attributes = _reflectionOperations.GetAttributes<TestCategoryAttribute>(method).ToArray();

        attributes.SelectMany(attribute => attribute.TestCategories).Should().Equal("derived", "base");
    }

    public void GetCustomAttributesShouldIncludeUserDefinedMSTestAttributes()
    {
        MethodInfo method = typeof(MSTestCustomAttributeClass).GetMethod(nameof(MSTestCustomAttributeClass.AttributeMethod))!;

        object[] attributes = _reflectionOperations.GetCustomAttributes(method);

        GetMSTestAttributeValuePairs(attributes).Should().Equal("Duration : superfast", "Owner : base", "TestCategory : base");
    }

    public void GetMSTestPropertiesShouldIncludeUserDefinedAndInheritedAttributes()
    {
        MethodInfo method = typeof(MSTestCustomAttributeClass).GetMethod(nameof(MSTestCustomAttributeClass.AttributeMethod))!;

        TestPropertyAttribute[] attributes = _reflectionOperations.GetAttributes<TestPropertyAttribute>(method).ToArray();

        attributes.Select(attribute => (attribute.GetType(), attribute.Name, attribute.Value)).Should().Equal(
            (typeof(DurationAttribute), "Duration", "superfast"),
            (typeof(OwnerAttribute), "Owner", "base"));
        attributes[0].Should().BeOfType<DurationAttribute>().Which.Duration.Should().Be("superfast");
    }

    public void GetCustomArrayAttributesShouldPreserveAllValues()
    {
        MethodInfo method = typeof(MSTestCustomAttributeClass).GetMethod(nameof(MSTestCustomAttributeClass.ArrayAttributeMethod))!;

        CategoryArrayAttribute[] attributes = _reflectionOperations.GetAttributes<CategoryArrayAttribute>(method).ToArray();

        attributes.Should().ContainSingle().Which.Value.Should().Equal("foo", "foo2");
    }

    public void GetMSTestCategoriesOnTypeShouldReturnDeclaredAttribute()
    {
        TestCategoryAttribute[] attributes = _reflectionOperations.GetAttributes<TestCategoryAttribute>(typeof(MSTestAttributeBaseClass)).ToArray();

        attributes.SelectMany(attribute => attribute.TestCategories).Should().Equal("ba");
    }

    public void GetMSTestCategoriesOnDerivedTypeShouldIncludeInheritedAttributes()
    {
        TestCategoryAttribute[] attributes = _reflectionOperations.GetAttributes<TestCategoryAttribute>(typeof(MSTestAttributeDerivedClass)).ToArray();

        attributes.SelectMany(attribute => attribute.TestCategories).Should().Equal("a", "ba");
    }

    public void GetMSTestAssemblyCategoriesShouldPreserveValuesAndTypeIdentity()
    {
        // Keep these attributes off the test suite itself: they belong only to the assembly under inspection.
        AssemblyName name = new($"ReflectionUtilityTests_{Guid.NewGuid():N}");
#if NETFRAMEWORK
        AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
        ConstructorInfo constructor = typeof(TestCategoryAttribute).GetConstructor([typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["a1"]));
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["a2"]));

        object[] attributes = _reflectionOperations.GetCustomAttributes(assembly, typeof(TestCategoryAttribute));

        attributes.Should().AllBeOfType<TestCategoryAttribute>();
        attributes.Cast<TestCategoryAttribute>().SelectMany(attribute => attribute.TestCategories).Should().Equal("a1", "a2");
    }

    private static IEnumerable<string> GetMSTestAttributeValuePairs(IEnumerable<object> attributes)
    {
        foreach (object attribute in attributes)
        {
            // Ignore unrelated compiler-generated metadata, but retain the order and concrete runtime types.
            switch (attribute)
            {
                case OwnerAttribute owner:
                    yield return "Owner : " + owner.Owner;
                    break;
                case TestCategoryAttribute category:
                    yield return "TestCategory : " + string.Join(",", category.TestCategories);
                    break;
                case DurationAttribute duration:
                    yield return "Duration : " + duration.Duration;
                    break;
            }
        }
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

    [TestCategory("ba")]
    public class MSTestAttributeBaseClass
    {
        [Owner("base")]
        [TestCategory("base")]
        public virtual void AttributeMethod()
        {
        }
    }

    [TestCategory("a")]
    public class MSTestAttributeDerivedClass : MSTestAttributeBaseClass
    {
        [Owner("derived")]
        [TestCategory("derived")]
        public override void AttributeMethod()
        {
        }
    }

    public class MSTestCustomAttributeClass : MSTestAttributeBaseClass
    {
        [Duration("superfast")]
        public override void AttributeMethod()
        {
        }

        [CategoryArray("foo", "foo2")]
        public void ArrayAttributeMethod()
        {
        }
    }

    public sealed class DurationAttribute(string duration) : TestPropertyAttribute("Duration", duration)
    {
        public string Duration { get; } = duration;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CategoryArrayAttribute(params string[] value) : Attribute
    {
        public string[] Value { get; } = value;
    }
}
