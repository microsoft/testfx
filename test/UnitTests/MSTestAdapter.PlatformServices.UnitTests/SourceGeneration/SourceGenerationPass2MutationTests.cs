// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.SourceGeneration;

public sealed class SourceGenerationPass2MutationTests : TestContainer
{
    public void CompositeMergesTheTypePropertiesByNameOfEveryProvider()
    {
        PropertyInfo firstProperty = typeof(FirstPropertyHolder).GetProperty(nameof(FirstPropertyHolder.Name))!;
        PropertyInfo secondProperty = typeof(SecondPropertyHolder).GetProperty(nameof(SecondPropertyHolder.Count))!;
        PropertyInfo thirdProperty = typeof(ThirdPropertyHolder).GetProperty(nameof(ThirdPropertyHolder.Enabled))!;
        var composite = new CompositeSourceGeneratedReflectionDataProvider();

        composite.Add(CreateProvider("first", typeof(FirstPropertyHolder), firstProperty));
        composite.GetSnapshot().TypePropertiesByName.Should().HaveCount(1, "the first provider is the only contributor so far");

        composite.Add(CreateProvider("second", typeof(SecondPropertyHolder), secondProperty));
        composite.Add(CreateProvider("third", typeof(ThirdPropertyHolder), thirdProperty));

        IReadOnlyDictionary<Type, Dictionary<string, PropertyInfo>> merged = composite.GetSnapshot().TypePropertiesByName;
        merged.Should().HaveCount(3);
        (Type Type, PropertyInfo Property)[] expected =
        [
            (typeof(FirstPropertyHolder), firstProperty),
            (typeof(SecondPropertyHolder), secondProperty),
            (typeof(ThirdPropertyHolder), thirdProperty),
        ];
        foreach ((Type type, PropertyInfo property) in expected)
        {
            merged.Should().ContainKey(type, "every provider contributes its own types");
            merged[type].Should().ContainSingle().Which.Value.Should().BeSameAs(property);
            merged[type].Should().ContainKey(property.Name);
        }
    }

    public void GetCustomAttributesCachedFallsBackToReflectionWhenNoGeneratedAssemblyAttributesMatch()
    {
        Assembly assembly = typeof(SourceGenerationPass2MutationTests).Assembly;
        string[] expected = GetTypeNames(assembly.GetCustomAttributes(typeof(Attribute), inherit: true));
        expected.Should().NotBeEmpty("the test assembly carries assembly-level attributes for the fallback to return");
        var withoutAnyAttributes = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());
        var withAnotherAssemblyOnly = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            Assembly = typeof(string).Assembly,
            AssemblyAttributes = [new MarkerAttribute()],
        });

        GetTypeNames(withoutAnyAttributes.GetCustomAttributesCached(assembly))
            .Should().BeEquivalentTo(expected, "a provider that publishes nothing must defer to runtime reflection");
        GetTypeNames(withAnotherAssemblyOnly.GetCustomAttributesCached(assembly))
            .Should().BeEquivalentTo(expected, "attributes generated for another assembly must not hide the reflected ones");
    }

    public void GetCustomAttributesCachedReturnsTheGeneratedAssemblyAttributesWithoutQueryingReflection()
    {
        Assembly assembly = typeof(SourceGenerationPass2MutationTests).Assembly;
        var generated = new MarkerAttribute();
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            Assembly = assembly,
            AssemblyAttributes = [generated, new object()],
        });

        Attribute[] attributes = operations.GetCustomAttributesCached(assembly);

        attributes.Should().ContainSingle().Which.Should().BeSameAs(generated);
    }

    private static SourceGeneratedReflectionDataProvider CreateProvider(string assemblyName, Type type, PropertyInfo property)
        => new()
        {
            AssemblyName = assemblyName,
            TypePropertiesByName = new Dictionary<Type, Dictionary<string, PropertyInfo>>
            {
                [type] = new() { [property.Name] = property },
            },
        };

    private static string[] GetTypeNames(IEnumerable<object> attributes)
        => [.. attributes.Select(attribute => attribute.GetType().FullName!)];

    internal sealed class FirstPropertyHolder
    {
        public string? Name { get; set; }
    }

    internal sealed class SecondPropertyHolder
    {
        public int Count { get; set; }
    }

    internal sealed class ThirdPropertyHolder
    {
        public bool Enabled { get; set; }
    }

    [AttributeUsage(AttributeTargets.All)]
    private sealed class MarkerAttribute : Attribute;
}
