// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.SourceGeneration;

public sealed class SourceGeneratedReflectionMutationTests : TestContainer
{
    public void ProviderReturnsAssemblyAttributesOnlyForItsOwnAssembly()
    {
        object attribute = new();
        var provider = new SourceGeneratedReflectionDataProvider
        {
            Assembly = typeof(Sample).Assembly,
            AssemblyAttributes = [attribute],
        };

        provider.GetAssemblyAttributes(typeof(string).Assembly).Should().BeEmpty();
        provider.GetAssemblyAttributes(typeof(Sample).Assembly).Should().Equal(attribute);
    }

    public void GetNavigationDataReturnsAnEmptyFileNameWhenTheTypeLocationHasNone()
    {
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodLocations = new Dictionary<string, SourceGeneratedReflectionDataProvider.TypeLocation>
            {
                ["Type"] = new() { MethodLocations = new Dictionary<string, int> { ["Method"] = 5 } },
            },
        };

        provider.GetNavigationData("Type", "Method", out int line, out string? file);

        line.Should().Be(5);
        file.Should().BeEmpty();
        new SourceGeneratedReflectionDataProvider.TypeLocation().FileName.Should().BeEmpty();
    }

    public void CompositeKeepsTheStoredAssemblyAttributesWhenAnotherProviderAddsNone()
    {
        Assembly assembly = typeof(Sample).Assembly;
        object first = new();
        object second = new();
        var composite = new CompositeSourceGeneratedReflectionDataProvider();

        composite.Add(new SourceGeneratedReflectionDataProvider { Assembly = assembly, AssemblyName = "first", AssemblyAttributes = [first] });
        object[] initial = composite.GetAssemblyAttributes(assembly);

        composite.Add(new SourceGeneratedReflectionDataProvider { Assembly = assembly, AssemblyName = "second" });
        object[] withoutAdditions = composite.GetAssemblyAttributes(assembly);

        composite.Add(new SourceGeneratedReflectionDataProvider { Assembly = assembly, AssemblyName = "third", AssemblyAttributes = [second] });
        object[] merged = composite.GetAssemblyAttributes(assembly);

        initial.Should().Equal(first);
        withoutAdditions.Should().BeSameAs(initial, "a provider without assembly attributes must not rebuild the stored array");
        merged.Should().Equal(first, second);
        composite.GetAssemblyAttributes(typeof(string).Assembly).Should().BeEmpty();
    }

    public void GetRuntimePropertyKeepsPropertiesWithAnyPublicAccessorWhenNonPublicMembersAreExcluded()
    {
        PropertyInfo publicGetInternalSet = typeof(PropertySample).GetProperty(nameof(PropertySample.PublicGetInternalSet))!;
        PropertyInfo readOnlyPublic = typeof(PropertySample).GetProperty(nameof(PropertySample.ReadOnlyPublic))!;
        PropertyInfo internalGetPublicSet = typeof(PropertySample).GetProperty(nameof(PropertySample.InternalGetPublicSet))!;
        PropertyInfo hidden = typeof(PropertySample).GetProperty(nameof(PropertySample.Hidden), BindingFlags.Instance | BindingFlags.NonPublic)!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            TypePropertiesByName = new Dictionary<Type, Dictionary<string, PropertyInfo>>
            {
                [typeof(PropertySample)] = new()
                {
                    [publicGetInternalSet.Name] = publicGetInternalSet,
                    [readOnlyPublic.Name] = readOnlyPublic,
                    [internalGetPublicSet.Name] = internalGetPublicSet,
                    [hidden.Name] = hidden,
                },
            },
        });

        operations.GetRuntimeProperty(typeof(PropertySample), publicGetInternalSet.Name, includeNonPublic: false).Should().BeSameAs(publicGetInternalSet);
        operations.GetRuntimeProperty(typeof(PropertySample), readOnlyPublic.Name, includeNonPublic: false).Should().BeSameAs(readOnlyPublic);
        operations.GetRuntimeProperty(typeof(PropertySample), internalGetPublicSet.Name, includeNonPublic: false).Should().BeSameAs(internalGetPublicSet);
        operations.GetRuntimeProperty(typeof(PropertySample), hidden.Name, includeNonPublic: false).Should().BeNull();
        operations.GetRuntimeProperty(typeof(PropertySample), hidden.Name, includeNonPublic: true).Should().BeSameAs(hidden);
    }

    public void GetConstructorInvokerReturnsNullWhenNoConstructorInvokerIsRegisteredForTheType()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
            {
                [typeof(Sample)] = [],
            },
        });

        operations.GetConstructorInvoker(typeof(Sample)).Should().BeNull();
    }

    public void GetConstructorInvokerCreatesInstancesThroughTheMatchingConstructorInvoker()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
            {
                [typeof(Sample)] =
                [
                    new() { Parameters = [typeof(int)], Invoker = _ => new Sample("int") },
                    new() { Parameters = [typeof(string)], Invoker = arguments => new Sample((string?)arguments![0]) },
                ],
            },
        });

        Func<object?[]?, object>? invoker = operations.GetConstructorInvoker(typeof(Sample));

        invoker.Should().NotBeNull();
        invoker!(["text"]).Should().BeOfType<Sample>().Which.Value.Should().Be("text");
    }

    public void CreateInstanceStopsInspectingArgumentsAfterTheFirstTypeMismatch()
    {
        var spy = new InstanceCheckCountingType(typeof(string));
        SourceGeneratedReflectionOperations operations = CreateOperationsWithTwoConstructors(spy);

        object? instance = operations.CreateInstance(typeof(Sample), ["text", "value"]);

        instance.Should().BeOfType<Sample>().Which.Value.Should().Be("second");
        spy.InstanceChecks.Should().Be(0, "the first constructor already mismatched on its first parameter");
    }

    public void CreateInstanceStopsInspectingArgumentsAfterANullForANonNullableValueType()
    {
        var spy = new InstanceCheckCountingType(typeof(string));
        SourceGeneratedReflectionOperations operations = CreateOperationsWithTwoConstructors(spy);

        object? instance = operations.CreateInstance(typeof(Sample), [null, "value"]);

        instance.Should().BeOfType<Sample>().Which.Value.Should().Be("second");
        spy.InstanceChecks.Should().Be(0, "the first constructor already mismatched on its first parameter");
    }

    public void IsMethodDeclaredInSameAssemblyAsTypeComparesTheDeclaringAssemblyWithTheAssemblyOfTheType()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());
        MethodInfo declaredInSampleAssembly = typeof(Sample).GetProperty(nameof(Sample.Value))!.GetMethod!;
        MethodInfo declaredInCoreLibrary = typeof(object).GetMethod(nameof(ToString))!;

        operations.IsMethodDeclaredInSameAssemblyAsType(declaredInSampleAssembly, typeof(Sample)).Should().BeTrue();
        operations.IsMethodDeclaredInSameAssemblyAsType(declaredInCoreLibrary, typeof(Sample)).Should().BeFalse();
        operations.IsMethodDeclaredInSameAssemblyAsType(new Mock<MethodInfo>().Object, typeof(Sample)).Should().BeFalse("a method without a declaring type is not declared in the assembly");
    }

    public void RegisterUsesAnEmptyAssemblyNameWhenTheAssemblyHasNoName()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        try
        {
            PlatformServiceProvider.Instance = null!;
            var assembly = new NamelessAssembly();

            ReflectionMetadataHook.Register(
                assembly,
                [],
                new Dictionary<Type, MethodInfo[]>(),
                new Dictionary<Type, Attribute[]>(),
                [],
                new Dictionary<MethodInfo, Attribute[]>(),
                new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
                new Dictionary<Type, ConstructorInvokerInfo[]>(),
                new Dictionary<PropertyInfo, Action<object?, object?>>());

            var fileOperations = (SourceGeneratedFileOperations)PlatformServiceProvider.Instance.FileOperations;

            fileOperations.DataProvider.TryGetAssembly(string.Empty, out Assembly? resolved).Should().BeTrue();
            resolved.Should().BeSameAs(assembly);
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
        }
    }

    public void RegisterIndexesTheTypesOfTheAssemblyByTheirFullName()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        try
        {
            PlatformServiceProvider.Instance = null!;
            var assembly = new NamedAssembly("RegisteredAssemblyWithTypes");

            ReflectionMetadataHook.Register(
                assembly,
                [typeof(Sample)],
                new Dictionary<Type, MethodInfo[]>(),
                new Dictionary<Type, Attribute[]>(),
                [],
                new Dictionary<MethodInfo, Attribute[]>(),
                new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
                new Dictionary<Type, ConstructorInvokerInfo[]>(),
                new Dictionary<PropertyInfo, Action<object?, object?>>());

            var fileOperations = (SourceGeneratedFileOperations)PlatformServiceProvider.Instance.FileOperations;

            fileOperations.DataProvider.TryGetTypeByName(assembly, typeof(Sample).FullName!, out Type? resolved).Should().BeTrue();
            resolved.Should().BeSameAs(typeof(Sample));
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
        }
    }

    private static SourceGeneratedReflectionOperations CreateOperationsWithTwoConstructors(InstanceCheckCountingType spy)
        => new(new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
            {
                [typeof(Sample)] =
                [
                    new() { Parameters = [typeof(int), spy], Invoker = _ => new Sample("first") },
                    new() { Parameters = [typeof(string), typeof(string)], Invoker = _ => new Sample("second") },
                ],
            },
        });

    internal sealed class Sample
    {
        public Sample()
        {
        }

        public Sample(string? value) => Value = value;

        public string? Value { get; }
    }

    internal sealed class PropertySample
    {
        public int PublicGetInternalSet { get; internal set; }

        public int ReadOnlyPublic { get; }

        public int InternalGetPublicSet { internal get; set; }

        internal int Hidden { get; set; }
    }

    private sealed class NamelessAssembly : Assembly
    {
        public override AssemblyName GetName() => new AssemblyName();
    }

    private sealed class NamedAssembly : Assembly
    {
        private readonly string _name;

        public NamedAssembly(string name) => _name = name;

        public override AssemblyName GetName() => new AssemblyName(_name);
    }

    private sealed class InstanceCheckCountingType : TypeDelegator
    {
        private readonly Type _inner;

        public InstanceCheckCountingType(Type inner)
            : base(inner)
            => _inner = inner;

        internal int InstanceChecks { get; private set; }

        public override bool IsInstanceOfType(object? o)
        {
            InstanceChecks++;
            return _inner.IsInstanceOfType(o);
        }
    }
}
