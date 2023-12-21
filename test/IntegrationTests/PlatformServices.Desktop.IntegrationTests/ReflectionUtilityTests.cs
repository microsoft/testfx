// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SampleFrameworkExtensions;

using TestFramework.ForTestingMSTest;

namespace PlatformServices.Desktop.ComponentTests;

public class ReflectionUtilityTests : TestContainer
{
    private readonly Assembly _testAsset;

    /// <summary>
    /// Dictionary of Assemblies discovered to date. Must be locked as it may
    /// be accessed in a multi-threaded context.
    /// </summary>
    private readonly Dictionary<string, Assembly> _resolvedAssemblies = [];

    public ReflectionUtilityTests()
    {
        var currentAssemblyDirectory = new FileInfo(typeof(ReflectionUtilityTests).Assembly.Location).Directory;
        var testAssetPath =
            Path.Combine(
                currentAssemblyDirectory.Parent.Parent.Parent.FullName,
                "TestProjectForDiscovery",
#if DEBUG
                "Debug",
#else
                "Release",
#endif
                currentAssemblyDirectory.Name /* TFM (e.g. net462) */,
                "TestProjectForDiscovery.dll");
        _testAsset = Assembly.ReflectionOnlyLoadFrom(testAssetPath);

        // This is needed for System assemblies.
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(ReflectionOnlyOnResolve);
    }

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : base", "Owner : base" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : derived", "Owner : derived" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        // Notice that the Owner on the base method does not show up since it can only be defined once.
        var expectedAttributes = new string[] { "TestCategory : derived", "TestCategory : base", "Owner : derived" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        var typeInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(typeInfo, false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : ba" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var typeInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(typeInfo, false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : a" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : a", "TestCategory : ba" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributes()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(TestCategoryAttribute), false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : base" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(TestCategoryAttribute), false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : derived" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo =
            _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(TestCategoryAttribute), true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : derived", "TestCategory : base", };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesIncludingUserDefinedAttributes()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, null, true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        var expectedAttributes = new string[] { "Duration : superfast", "TestCategory : base", "Owner : base" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnAllAttributesIncludingUserDefinedAttributes()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyVTestMethod1");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(TestPropertyAttribute), true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "Duration : superfast" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesShouldReturnArrayAttributesAsWell()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyTestMethod2");

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(CategoryArrayAttribute), true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "CategoryAttribute : foo,foo2" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        var typeInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(typeInfo, typeof(TestCategoryAttribute), false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : ba" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesIgnoringBaseInheritance()
    {
        var typeInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(typeInfo, typeof(TestCategoryAttribute), false);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        var expectedAttributes = new string[] { "TestCategory : a" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        var methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetTypeInfo();

        var attributes = ReflectionUtility.GetCustomAttributes(methodInfo, typeof(TestCategoryAttribute), true);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : a", "TestCategory : ba" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        var asm = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").Assembly;

        var attributes = ReflectionUtility.GetCustomAttributes(asm, typeof(TestCategoryAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        var expectedAttributes = new string[] { "TestCategory : a1", "TestCategory : a2" };
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    private Assembly ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
    {
        string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

        // Put it in the resolved assembly cache so that if the Load call below
        // triggers another assembly resolution, then we don't end up in stack overflow.
        _resolvedAssemblies[assemblyNameToLoad] = null;

        var assembly = Assembly.ReflectionOnlyLoad(assemblyNameToLoad);

        if (assembly != null)
        {
            _resolvedAssemblies[assemblyNameToLoad] = assembly;
            return assembly;
        }

        return null;
    }

    private static string[] GetAttributeValuePairs(object[] attributes)
    {
        var attributeValuePairs = new List<string>();
        foreach (var attribute in attributes)
        {
            if (attribute is OwnerAttribute)
            {
                var a = attribute as OwnerAttribute;
                attributeValuePairs.Add("Owner : " + a.Owner);
            }
            else if (attribute is TestCategoryAttribute)
            {
                var a = attribute as TestCategoryAttribute;
                attributeValuePairs.Add("TestCategory : " + a.TestCategories.Aggregate((i, j) => { return i + "," + j; }));
            }
            else if (attribute is DurationAttribute)
            {
                var a = attribute as DurationAttribute;
                attributeValuePairs.Add("Duration : " + a.Duration);
            }
            else if (attribute is CategoryArrayAttribute)
            {
                var a = attribute as CategoryArrayAttribute;
                attributeValuePairs.Add("CategoryAttribute : " + a.Value.Aggregate((i, j) => { return i + "," + j; }));
            }
        }

        return attributeValuePairs.ToArray();
    }
}
