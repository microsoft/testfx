// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using SampleFrameworkExtensions;

using TestFramework.ForTestingMSTest;

namespace PlatformServices.Desktop.ComponentTests;

public class ReflectionUtilityTests : TestContainer
{
    private readonly Assembly _testAsset;

    public ReflectionUtilityTests()
    {
        DirectoryInfo currentAssemblyDirectory = new FileInfo(typeof(ReflectionUtilityTests).Assembly.Location).Directory;
        string testAssetPath =
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
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ReflectionOnlyOnResolve;
    }

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass").GetMethod("DummyVTestMethod1");

        IReadOnlyList<object> attributes = new ReflectionOperations().GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["TestCategory : base", "Owner : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesWithBaseInheritance()
    {
        MethodInfo methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").GetMethod("DummyVTestMethod1");

        IReadOnlyList<object> attributes = new ReflectionOperations().GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        // Notice that the Owner on the base method does not show up since it can only be defined once.
        string[] expectedAttributes = ["TestCategory : derived", "TestCategory : base", "Owner : derived"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = _testAsset.GetType("TestProjectForDiscovery.AttributeTestBaseClass");

        IReadOnlyList<object> attributes = new ReflectionOperations().GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(1);

        string[] expectedAttributes = ["TestCategory : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributesWithBaseInheritance()
    {
        Type type = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass");

        IReadOnlyList<object> attributes = new ReflectionOperations().GetCustomAttributes(type);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["TestCategory : a", "TestCategory : ba"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetCustomAttributesShouldReturnAllAttributesIncludingUserDefinedAttributes()
    {
        MethodInfo methodInfo = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClassWithCustomAttributes").GetMethod("DummyVTestMethod1");

        IReadOnlyList<object> attributes = new ReflectionOperations().GetCustomAttributes(methodInfo);

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(3);

        string[] expectedAttributes = ["Duration : superfast", "TestCategory : base", "Owner : base"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = _testAsset.GetType("TestProjectForDiscovery.AttributeTestClass").Assembly;

        object[] attributes = new ReflectionOperations().GetCustomAttributes(asm, typeof(TestCategoryAttribute));

        attributes.Should().NotBeNull();
        attributes.Should().HaveCount(2);

        string[] expectedAttributes = ["TestCategory : a1", "TestCategory : a2"];
        GetAttributeValuePairs(attributes).Should().Equal(expectedAttributes);
    }

    private static Assembly ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
    {
        string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

        return Assembly.ReflectionOnlyLoad(assemblyNameToLoad);
    }

    private static string[] GetAttributeValuePairs(IEnumerable attributes)
    {
        var attributeValuePairs = new List<string>();
        foreach (object attribute in attributes)
        {
            if (attribute is OwnerAttribute ownerAttribute)
            {
                attributeValuePairs.Add("Owner : " + ownerAttribute.Owner);
            }
            else if (attribute is TestCategoryAttribute categoryAttribute)
            {
                attributeValuePairs.Add("TestCategory : " + categoryAttribute.TestCategories.Aggregate((i, j) => i + ',' + j));
            }
            else if (attribute is DurationAttribute durationAttribute)
            {
                attributeValuePairs.Add("Duration : " + durationAttribute.Duration);
            }
            else if (attribute is CategoryArrayAttribute arrayAttribute)
            {
                attributeValuePairs.Add("CategoryAttribute : " + arrayAttribute.Value.Aggregate((i, j) => i + ',' + j));
            }
        }

        return [.. attributeValuePairs];
    }
}
