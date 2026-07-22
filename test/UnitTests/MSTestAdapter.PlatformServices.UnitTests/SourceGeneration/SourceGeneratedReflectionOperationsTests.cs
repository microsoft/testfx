// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.SourceGeneration;

/// <summary>
/// Verifies that <see cref="SourceGeneratedReflectionOperations"/> serves the delegate-based
/// invokers published by the AOT source generator (constructor factories, method invokers,
/// property setters) so the adapter runs tests without <c>MethodInfo.Invoke</c> /
/// <c>Activator.CreateInstance</c> / <c>PropertyInfo.SetValue</c>.
/// </summary>
public sealed class SourceGeneratedReflectionOperationsTests : TestContainer
{
    public void GetTestMethodInvoker_ReturnsRegisteredDelegate_AndInvokesWithoutReflection()
    {
        MethodInfo add = typeof(Sample).GetMethod(nameof(Sample.Add))!;

        // Mirrors the source-generated contract: the invoker calls the method directly and returns a
        // non-null Task representing completion; the return value is discarded (the method's effect is
        // observed via state instead).
        Func<object?, object?[]?, object?> invoker = static (instance, args) =>
        {
            ((Sample)instance!).Add((int)args![0]!, (int)args![1]!);
            return Task.CompletedTask;
        };

        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodInvokers = new Dictionary<MethodInfo, Func<object?, object?[]?, object?>> { [add] = invoker },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetTestMethodInvoker(add).Should().BeSameAs(invoker);

        var sample = new Sample();
        object? result = operations.GetTestMethodInvoker(add)!(sample, [2, 3]);
        result.Should().BeAssignableTo<Task>();
        ((Task)result!).IsCompleted.Should().BeTrue();
        sample.LastSum.Should().Be(5);
    }

    public void GetTestMethodInvoker_ReturnsNull_WhenMethodNotRegistered()
    {
        MethodInfo unregistered = typeof(Sample).GetMethod(nameof(Sample.Add))!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetTestMethodInvoker(unregistered).Should().BeNull();
    }

    public void GetConstructorInvoker_CreatesInstance_WithoutActivator()
    {
        SourceGeneratedReflectionDataProvider.ConstructorInvoker[] invokers =
        [
            new SourceGeneratedReflectionDataProvider.ConstructorInvoker
            {
                Parameters = [typeof(string)],
                Invoker = static args => new Sample((string)args![0]!),
            },
        ];
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]> { [typeof(Sample)] = invokers },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        Func<object?[]?, object>? constructorInvoker = operations.GetConstructorInvoker(typeof(Sample));
        constructorInvoker.Should().NotBeNull();
        ((Sample)constructorInvoker!(["hello"])).Value.Should().Be("hello");

        // CreateInstance routes through the same invoker (no Activator.CreateInstance).
        ((Sample)operations.CreateInstance(typeof(Sample), ["world"])!).Value.Should().Be("world");
    }

    public void GetConstructorInvoker_ReturnsNull_WhenTypeNotRegistered()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetConstructorInvoker(typeof(Sample)).Should().BeNull();
    }

    public void GetConstructorInvoker_FallsBackToReflection_WhenArgumentsDoNotMatchRegisteredConstructor()
    {
        // Only the (string) constructor is registered, but Sample also has a parameterless one.
        SourceGeneratedReflectionDataProvider.ConstructorInvoker[] invokers =
        [
            new SourceGeneratedReflectionDataProvider.ConstructorInvoker
            {
                Parameters = [typeof(string)],
                Invoker = static args => new Sample((string)args![0]!),
            },
        ];
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]> { [typeof(Sample)] = invokers },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        // An empty argument list matches no registered constructor; the invoker must fall back to
        // reflection (the parameterless constructor) rather than throwing MissingMethodException.
        Func<object?[]?, object>? constructorInvoker = operations.GetConstructorInvoker(typeof(Sample));
        constructorInvoker.Should().NotBeNull();
        object instance = constructorInvoker!([]);
        instance.Should().BeOfType<Sample>();
        ((Sample)instance).Value.Should().BeNull();
    }

    public void GetPropertySetter_AssignsValue_WithoutSetValue()
    {
        PropertyInfo property = typeof(Sample).GetProperty(nameof(Sample.Value))!;
        Action<object?, object?> setter = static (instance, value) => ((Sample)instance!).Value = (string?)value;

        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypePropertySetters = new Dictionary<PropertyInfo, Action<object?, object?>> { [property] = setter },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetPropertySetter(property).Should().BeSameAs(setter);

        var sample = new Sample();
        operations.GetPropertySetter(property)!(sample, "assigned");
        sample.Value.Should().Be("assigned");
    }

    public void GetPropertySetter_ReturnsNull_WhenPropertyNotRegistered()
    {
        PropertyInfo property = typeof(Sample).GetProperty(nameof(Sample.Value))!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetPropertySetter(property).Should().BeNull();
    }

    public void GetCustomAttributes_ReturnsRegisteredTypeAttributes_IncludingEmptyArray()
    {
        var generated = new MarkerAttribute("generated");
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeAttributes = new Dictionary<Type, Attribute[]>
            {
                [typeof(AttributedSample)] = [generated],
                [typeof(EmptyAuthoritativeSample)] = [],
            },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetCustomAttributes(typeof(AttributedSample)).Should().ContainSingle().Which.Should().BeSameAs(generated);
        operations.GetCustomAttributes(typeof(EmptyAuthoritativeSample)).Should().BeEmpty();
    }

    public void GetCustomAttributes_FallsBackForMissingTypeEntry()
    {
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetCustomAttributes(typeof(AttributedSample))
            .Should().ContainSingle(attribute => attribute.GetType() == typeof(MarkerAttribute) && ((MarkerAttribute)attribute).Value == "reflection");
    }

    public void GetCustomAttributes_ReturnsRegisteredMethodAttributes()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        var generated = new MarkerAttribute("generated");
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]> { [method] = [generated] },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetCustomAttributes(method).Should().ContainSingle().Which.Should().BeSameAs(generated);
    }

    public void GetCustomAttributes_ReturnsEmptyForAuthoritativeEmptyMethodEntry()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]> { [method] = [] },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetCustomAttributes(method).Should().BeEmpty();
    }

    public void GetCustomAttributes_FallsBackForMissingMethodEntry()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider());

        operations.GetCustomAttributes(method)
            .Should().ContainSingle(attribute => attribute.GetType() == typeof(MarkerAttribute) && ((MarkerAttribute)attribute).Value == "reflection");
    }

    public void MethodAttributeHelpers_UseRegisteredAttributes()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        var generated = new DerivedMarkerAttribute("generated");
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]> { [method] = [generated] },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.IsAttributeDefined<MarkerAttribute>(method).Should().BeTrue();
        operations.GetFirstAttributeOrDefault<MarkerAttribute>(method).Should().BeSameAs(generated);
        operations.GetSingleAttributeOrDefault<MarkerAttribute>(method).Should().BeSameAs(generated);
        operations.GetAttributes<MarkerAttribute>(method).Should().ContainSingle().Which.Should().BeSameAs(generated);
    }

    public void CompositeProvider_MergesMethodAttributesFromMultipleProviders()
    {
        MethodInfo firstMethod = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        MethodInfo secondMethod = typeof(AttributedSample).GetMethod(nameof(AttributedSample.SecondMethod))!;
        var firstAttribute = new MarkerAttribute("first");
        var secondAttribute = new MarkerAttribute("second");
        var composite = new CompositeSourceGeneratedReflectionDataProvider();
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]> { [firstMethod] = [firstAttribute] },
        });
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]> { [secondMethod] = [secondAttribute] },
        });
        var operations = new SourceGeneratedReflectionOperations(composite);

        operations.GetCustomAttributes(firstMethod).Should().ContainSingle().Which.Should().BeSameAs(firstAttribute);
        operations.GetCustomAttributes(secondMethod).Should().ContainSingle().Which.Should().BeSameAs(secondAttribute);
    }

    public void ReflectionMetadataHook_RejectsNullMethodAttributes()
    {
        Action action = () => ReflectionMetadataHook.Register(
            typeof(SourceGeneratedReflectionOperationsTests).Assembly,
            [],
            new Dictionary<Type, MethodInfo[]>(),
            new Dictionary<Type, Attribute[]>(),
            [],
            null!,
            new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
            new Dictionary<Type, ConstructorInvokerInfo[]>(),
            new Dictionary<PropertyInfo, Action<object?, object?>>());

        action.Should().Throw<ArgumentNullException>().WithParameterName("methodAttributes");
    }

    public void ReflectionMetadataHook_RegistersMethodAttributesWithRuntimeProvider()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.SecondMethod))!;
        var generated = new MarkerAttribute("hook");

        ReflectionMetadataHook.Register(
            typeof(SourceGeneratedReflectionOperationsTests).Assembly,
            [typeof(AttributedSample)],
            new Dictionary<Type, MethodInfo[]> { [typeof(AttributedSample)] = [method] },
            new Dictionary<Type, Attribute[]> { [typeof(AttributedSample)] = [] },
            [],
            new Dictionary<MethodInfo, Attribute[]> { [method] = [generated] },
            new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
            new Dictionary<Type, ConstructorInvokerInfo[]>(),
            new Dictionary<PropertyInfo, Action<object?, object?>>());

        var operations = (SourceGeneratedReflectionOperations)Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance.ReflectionOperations;
        operations.GetCustomAttributes(method).Should().ContainSingle().Which.Should().BeSameAs(generated);
    }

    private sealed class Sample
    {
        public Sample()
        {
        }

        public Sample(string value) => Value = value;

        public string? Value { get; set; }

        public int LastSum { get; private set; }

        public int Add(int first, int second)
        {
            LastSum = first + second;
            return LastSum;
        }
    }

    [Marker("reflection")]
    private sealed class AttributedSample
    {
        [Marker("reflection")]
        public void AttributedMethod()
        {
        }

        public void SecondMethod()
        {
        }
    }

    [Marker("reflection")]
    private sealed class EmptyAuthoritativeSample
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    private class MarkerAttribute(string value) : Attribute
    {
        public string Value { get; } = value;
    }

    private sealed class DerivedMarkerAttribute(string value) : MarkerAttribute(value);
}
