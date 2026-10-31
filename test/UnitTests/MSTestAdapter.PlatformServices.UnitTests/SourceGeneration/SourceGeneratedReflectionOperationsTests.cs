// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

    public void TryGetTestMethodDescriptors_ReturnsRegisteredMethodsAndCompleteness()
    {
        MethodInfo method = typeof(Sample).GetMethod(nameof(Sample.Add))!;
        var provider = new SourceGeneratedReflectionDataProvider
        {
            DescriptorTestMethods = new Dictionary<Type, MethodInfo[]> { [typeof(Sample)] = [method] },
            DescriptorCompleteTypes = new Dictionary<Type, bool> { [typeof(Sample)] = true },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.TryGetTestMethodDescriptors(typeof(Sample), out MethodInfo[]? methods, out bool isComplete)
            .Should().BeTrue();
        methods.Should().ContainSingle().Which.Should().BeSameAs(method);
        isComplete.Should().BeTrue();
    }

    public void TryGetTestMethodDescriptors_RetainsPerMethodFallbackWhenRegistrationIsIncomplete()
    {
        MethodInfo add = typeof(Sample).GetMethod(nameof(Sample.Add))!;
        MethodInfo subtract = typeof(Sample).GetMethod(nameof(Sample.Subtract))!;
        var composite = new CompositeSourceGeneratedReflectionDataProvider();
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            DescriptorTestMethods = new Dictionary<Type, MethodInfo[]> { [typeof(Sample)] = [add] },
            DescriptorCompleteTypes = new Dictionary<Type, bool> { [typeof(Sample)] = true },
        });
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            DescriptorTestMethods = new Dictionary<Type, MethodInfo[]> { [typeof(Sample)] = [subtract] },
            DescriptorCompleteTypes = new Dictionary<Type, bool> { [typeof(Sample)] = false },
        });
        var operations = new SourceGeneratedReflectionOperations(composite);

        operations.TryGetTestMethodDescriptors(typeof(Sample), out MethodInfo[]? methods, out bool isComplete)
            .Should().BeTrue();
        methods.Should().HaveCount(2);
        methods.Should().Contain(add);
        methods.Should().Contain(subtract);
        isComplete.Should().BeFalse();
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

        // GetFirstAttributeOrDefault is contractually restricted to sealed attribute types (a non-sealed
        // type can be matched by several derived attributes), so it is queried with the sealed derived type.
        operations.GetFirstAttributeOrDefault<DerivedMarkerAttribute>(method).Should().BeSameAs(generated);
        operations.GetSingleAttributeOrDefault<MarkerAttribute>(method).Should().BeSameAs(generated);
        operations.GetAttributes<MarkerAttribute>(method).Should().ContainSingle().Which.Should().BeSameAs(generated);
    }

    public void RegisteredAsyncMethodAttributes_PreserveDeclaredAttributesAndRejectAsyncVoid()
    {
        MethodInfo method = typeof(AsyncAttributedSample).GetMethod(nameof(AsyncAttributedSample.AsyncVoidMethod))!;
        var generatedMarker = new MarkerAttribute("generated");
        AsyncStateMachineAttribute asyncStateMachineAttribute = method.GetCustomAttribute<AsyncStateMachineAttribute>()!;
        var debuggerStepThroughAttribute = new DebuggerStepThroughAttribute();
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]>
            {
                [method] = [new TestMethodAttribute(), generatedMarker, asyncStateMachineAttribute, debuggerStepThroughAttribute],
            },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        operations.GetAttributes<MarkerAttribute>(method).Should().ContainSingle().Which.Should().BeSameAs(generatedMarker);
        operations.GetFirstAttributeOrDefault<AsyncStateMachineAttribute>(method).Should().BeSameAs(asyncStateMachineAttribute);
        operations.GetFirstAttributeOrDefault<DebuggerStepThroughAttribute>(method).Should().BeSameAs(debuggerStepThroughAttribute);

        var validator = new TestMethodValidator(new SourceGeneratedReflectHelper(operations), discoverInternals: false);
        var warnings = new List<string>();
        validator.IsValidTestMethod(method, typeof(AsyncAttributedSample), warnings).Should().BeFalse();
        warnings.Should().ContainSingle();
    }

    public void MethodAttributeHelpers_GetSingleAttributeOrDefault_ThrowsLocalizedErrorOnDuplicates()
    {
        MethodInfo method = typeof(AttributedSample).GetMethod(nameof(AttributedSample.AttributedMethod))!;
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]>
            {
                [method] = [new MarkerAttribute("first"), new DerivedMarkerAttribute("second")],
            },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        Action action = () => operations.GetSingleAttributeOrDefault<MarkerAttribute>(method);

        // Regression test for #10302: this path used to throw a hardcoded, non-localizable English message.
        action.Should().Throw<InvalidOperationException>()
            .WithMessage(string.Format(CultureInfo.InvariantCulture, Resource.DuplicateAttributeError, typeof(MarkerAttribute)));
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

        public int Subtract(int first, int second)
        {
            LastSum = first - second;
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

    private sealed class SourceGeneratedReflectHelper(SourceGeneratedReflectionOperations operations) : ReflectHelper
    {
        public override bool IsAttributeDefined<TAttribute>(ICustomAttributeProvider attributeProvider)
            => operations.IsAttributeDefined<TAttribute>(attributeProvider);

        public override TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
            where TAttribute : class
            => operations.GetFirstAttributeOrDefault<TAttribute>(attributeProvider);
    }

    private sealed class AsyncAttributedSample
    {
        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Verifies async-void rejection.")]
        public async void AsyncVoidMethod() => await Task.Yield();
    }

    public void ConstructorRegistrationRejectsNullContractParts()
    {
        Action missingTypes = () => _ = new ConstructorInvokerInfo(null!, _ => new Sample());
        Action missingInvoker = () => _ = new ConstructorInvokerInfo([], null!);

        missingTypes.Should().ThrowExactly<ArgumentNullException>().WithParameterName("parameterTypes");
        missingInvoker.Should().ThrowExactly<ArgumentNullException>().WithParameterName("invoker");
    }

    public void RegistrationRejectsEachNullCollectionBeforePublishing()
    {
        string[] names =
        [
            "assembly", "types", "testMethods", "typeAttributes", "assemblyAttributes", "methodAttributes",
            "methodInvokers", "constructorInvokers", "propertySetters", "descriptorTestMethods", "descriptorCompleteTypes",
        ];
        for (int missing = 0; missing < names.Length; missing++)
        {
            Action register = () => ReflectionMetadataHook.Register(
                missing == 0 ? null! : typeof(Sample).Assembly,
                missing == 1 ? null! : [],
                missing == 2 ? null! : new Dictionary<Type, MethodInfo[]>(),
                missing == 3 ? null! : new Dictionary<Type, Attribute[]>(),
                missing == 4 ? null! : [],
                missing == 5 ? null! : new Dictionary<MethodInfo, Attribute[]>(),
                missing == 6 ? null! : new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
                missing == 7 ? null! : new Dictionary<Type, ConstructorInvokerInfo[]>(),
                missing == 8 ? null! : new Dictionary<PropertyInfo, Action<object?, object?>>(),
                missing == 9 ? null! : new Dictionary<Type, MethodInfo[]>(),
                missing == 10 ? null! : []);

            register.Should().ThrowExactly<ArgumentNullException>().WithParameterName(names[missing]);
        }
    }

    public async Task RichRegistrationExecutesConstructorMethodAndPropertySetter()
    {
        Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.IPlatformServiceProvider previous =
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance;
        try
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = null!;
            MethodInfo method = typeof(RegisteredCoverageSample).GetMethod(nameof(RegisteredCoverageSample.Add))!;
            PropertyInfo property = typeof(RegisteredCoverageSample).GetProperty(nameof(RegisteredCoverageSample.Text))!;
            ReflectionMetadataHook.Register(
                typeof(RegisteredCoverageSample).Assembly,
                [typeof(RegisteredCoverageSample)],
                new Dictionary<Type, MethodInfo[]> { [typeof(RegisteredCoverageSample)] = [method] },
                new Dictionary<Type, Attribute[]> { [typeof(RegisteredCoverageSample)] = [new MarkerAttribute("generated")] },
                [],
                new Dictionary<MethodInfo, Attribute[]> { [method] = [new TestMethodAttribute()] },
                new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>
                {
                    [method] = (instance, args) =>
                    {
                        ((RegisteredCoverageSample)instance!).Add((int)args![0]!);
                        return Task.CompletedTask;
                    },
                },
                new Dictionary<Type, ConstructorInvokerInfo[]>
                {
                    [typeof(RegisteredCoverageSample)] =
                    [
                        new([typeof(string)], args => new RegisteredCoverageSample("generated:" + (string)args![0]!)),
                    ],
                },
                new Dictionary<PropertyInfo, Action<object?, object?>>
                {
                    [property] = (instance, value) => ((RegisteredCoverageSample)instance!).Text = "setter:" + (string?)value,
                },
                new Dictionary<Type, MethodInfo[]> { [typeof(RegisteredCoverageSample)] = [method] },
                [typeof(RegisteredCoverageSample)]);
            var operations = (SourceGeneratedReflectionOperations)Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance.ReflectionOperations;

            var instance = (RegisteredCoverageSample)operations.CreateInstance(typeof(RegisteredCoverageSample), ["input"])!;
            instance.Text.Should().Be("generated:input");
            await (Task)operations.GetTestMethodInvoker(method)!(instance, [7])!;
            instance.Total.Should().Be(17);
            operations.GetPropertySetter(property)!(instance, "value");
            instance.Text.Should().Be("setter:value");
            operations.TryGetTestMethodDescriptors(typeof(RegisteredCoverageSample), out MethodInfo[]? descriptors, out bool complete).Should().BeTrue();
            descriptors.Should().Equal(method);
            complete.Should().BeTrue();
            operations.GetType(typeof(RegisteredCoverageSample).Assembly, typeof(RegisteredCoverageSample).FullName!).Should().Be(typeof(RegisteredCoverageSample));
        }
        finally
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = previous;
        }
    }

    public void CompatibilityRegistrationsRetainRuntimeDiscovery()
    {
        Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.IPlatformServiceProvider previous =
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance;
        try
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = null!;
            MethodInfo method = typeof(CompatibilityCoverageSample).GetMethod(nameof(CompatibilityCoverageSample.Method))!;
            Dictionary<Type, MethodInfo[]> methods = new() { [typeof(CompatibilityCoverageSample)] = [method] };
            ReflectionMetadataHook.Register(typeof(CompatibilityCoverageSample).Assembly, [typeof(CompatibilityCoverageSample)], methods);
            ReflectionMetadataHook.Register(
                typeof(CompatibilityCoverageSample).Assembly, [typeof(CompatibilityCoverageSample)], methods,
                new Dictionary<Type, Attribute[]> { [typeof(CompatibilityCoverageSample)] = [] }, [new MarkerAttribute("assembly")]);
            var operations = (SourceGeneratedReflectionOperations)Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance.ReflectionOperations;

            operations.GetDeclaredMethods(typeof(CompatibilityCoverageSample)).Should().Contain(method);
            operations.GetCustomAttributes(typeof(CompatibilityCoverageSample)).Should().BeEmpty();
            operations.GetTestMethodInvoker(method).Should().BeNull();
            operations.GetCustomAttributes(typeof(RegisteredCoverageSample).Assembly, typeof(MarkerAttribute))
                .Cast<MarkerAttribute>().Select(attribute => attribute.Value).Should().Contain("assembly");
        }
        finally
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = previous;
        }
    }

    public void ProviderRoutesAssemblyTypesAndNavigationWithoutCrossAssemblyLeaks()
    {
        Assembly assembly = typeof(Sample).Assembly;
        string name = assembly.GetName().Name!;
        var provider = new SourceGeneratedReflectionDataProvider
        {
            Assembly = assembly,
            AssemblyName = name,
            TypesByName = new Dictionary<string, Type> { ["generated.Sample"] = typeof(Sample) },
            TypeMethodLocations = new Dictionary<string, SourceGeneratedReflectionDataProvider.TypeLocation>
            {
                ["generated.Sample"] = new() { FileName = "generated.cs", MethodLocations = new() { ["Add"] = 42 } },
                ["null"] = null!,
            },
        };

        provider.TryGetAssembly(name.ToUpperInvariant() + ".dll", out Assembly? found).Should().BeTrue();
        found.Should().BeSameAs(assembly);
        provider.TryGetAssembly("missing.dll", out found).Should().BeFalse();
        found.Should().BeNull();
        provider.TryGetTypeByName(assembly, "generated.Sample", out Type? type).Should().BeTrue();
        type.Should().Be(typeof(Sample));
        provider.TryGetTypeByName(typeof(string).Assembly, "generated.Sample", out type).Should().BeFalse();
        type.Should().BeNull();
        provider.TryGetTypeByName(assembly, "missing", out type).Should().BeFalse();
        type.Should().BeNull();
        foreach ((string Class, string Method) item in new[] { ("missing", "Add"), ("null", "Add"), ("generated.Sample", "Missing") })
        {
            provider.GetNavigationData(item.Class, item.Method, out int line, out string? file);
            line.Should().Be(0);
            file.Should().BeNull();
        }

        provider.GetNavigationData("generated.Sample", "Add", out int actualLine, out string? actualFile);
        actualLine.Should().Be(42);
        actualFile.Should().Be("generated.cs");
    }

    public void CompositeRegistrationRetainsAttributeUnionAndSnapshotIsolation()
    {
        Assembly assembly = typeof(Sample).Assembly;
        var composite = new CompositeSourceGeneratedReflectionDataProvider();
        var first = new MarkerAttribute("first");
        var second = new MarkerAttribute("second");
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            Assembly = assembly,
            AssemblyName = "sample",
            Types = [typeof(Sample)],
            AssemblyAttributes = [first],
        });
        SourceGeneratedReflectionDataProvider oldSnapshot = composite.GetSnapshot();
        composite.Add(new SourceGeneratedReflectionDataProvider
        {
            Assembly = assembly,
            AssemblyName = "sample",
            Types = [typeof(AttributedSample)],
            AssemblyAttributes = [second],
            TypesByName = new Dictionary<string, Type> { ["chosen"] = typeof(AttributedSample) },
            TypeMethodLocations = new Dictionary<string, SourceGeneratedReflectionDataProvider.TypeLocation>
            {
                ["chosen"] = new() { FileName = "chosen.cs", MethodLocations = new() { ["Method"] = 17 } },
            },
        });
        composite.Add(new SourceGeneratedReflectionDataProvider { Assembly = assembly, AssemblyName = "sample" });

        oldSnapshot.Types.Should().Equal(typeof(Sample));
        composite.GetSnapshot().Types.Should().Equal(typeof(Sample), typeof(AttributedSample));
        composite.GetAssemblyAttributes(assembly).Should().Equal(first, second);
        composite.GetAssemblyAttributes(typeof(string).Assembly).Should().BeEmpty();
        composite.TryGetAssembly("SAMPLE.dll", out Assembly? resolved).Should().BeTrue();
        resolved.Should().BeSameAs(assembly);
        composite.TryGetAssembly("missing.dll", out resolved).Should().BeFalse();
        resolved.Should().BeNull();
        composite.TryGetTypeByName(assembly, "chosen", out Type? type).Should().BeFalse();
        type.Should().BeNull();
        composite.TryGetTypeByName(typeof(string).Assembly, "chosen", out type).Should().BeFalse();
        type.Should().BeNull();
        composite.GetNavigationData("chosen", "Method", out int line, out string? file);
        line.Should().Be(17);
        file.Should().Be("chosen.cs");
        composite.GetNavigationData("chosen", "missing", out line, out file);
        line.Should().Be(0);
        file.Should().BeNull();
    }

    public void GeneratedConstructorMatchingRespectsNullabilityAndFallsBack()
    {
        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
            {
                [typeof(Sample)] =
                [
                    new() { Parameters = [typeof(int)], Invoker = _ => new Sample("integer") },
                    new() { Parameters = [typeof(string)], Invoker = _ => new Sample("nullable reference") },
                ],
            },
        };
        var operations = new SourceGeneratedReflectionOperations(provider);

        ((Sample)operations.CreateInstance(typeof(Sample), [7])!).Value.Should().Be("integer");
        ((Sample)operations.CreateInstance(typeof(Sample), [null])!).Value.Should().Be("nullable reference");
        ((Sample)operations.CreateInstance(typeof(Sample), ["value"])!).Value.Should().Be("nullable reference");
        ((Sample)operations.GetConstructorInvoker(typeof(Sample))!(null)).Value.Should().BeNull();
        Action mismatch = () => operations.CreateInstance(typeof(Sample), [new object()]);
        mismatch.Should().ThrowExactly<MissingMethodException>();
    }

    public void RegisteredPropertiesHonorVisibilityAndAttributeProviderValidation()
    {
        PropertyInfo hidden = typeof(RegisteredCoverageSample).GetProperty("Hidden", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var operations = new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
        {
            TypePropertiesByName = new Dictionary<Type, Dictionary<string, PropertyInfo>>
            {
                [typeof(RegisteredCoverageSample)] = new() { ["Hidden"] = hidden },
            },
        });

        operations.GetRuntimeProperty(typeof(RegisteredCoverageSample), "Hidden", includeNonPublic: false).Should().BeNull();
        PropertyInfo selected = operations.GetRuntimeProperty(typeof(RegisteredCoverageSample), "Hidden", includeNonPublic: true)!;
        var sample = new RegisteredCoverageSample("original");
        selected.SetValue(sample, "changed", null);
        sample.ReadHidden().Should().Be("changed");

        Action nullProvider = () => operations.GetCustomAttributesCached(null!);
        nullProvider.Should().ThrowExactly<ArgumentNullException>().WithParameterName("attributeProvider");
        ICustomAttributeProvider unsupported = typeof(Sample).GetMethod(nameof(Sample.Add))!.GetParameters()[0];
        Action invalidProvider = () => operations.GetCustomAttributesCached(unsupported);
        invalidProvider.Should().ThrowExactly<ArgumentException>().WithParameterName("attributeProvider")
            .Which.Message.Should().StartWith("Unsupported attribute provider type:");
    }

    private sealed class RegisteredCoverageSample(string text)
    {
        public string Text { get; set; } = text;

        public int Total { get; private set; } = 10;

        private string Hidden { get; set; } = "initial";

        public void Add(int value) => Total += value;

        public string ReadHidden() => Hidden;
    }

    private sealed class CompatibilityCoverageSample
    {
        public void Method()
        {
        }
    }
}
