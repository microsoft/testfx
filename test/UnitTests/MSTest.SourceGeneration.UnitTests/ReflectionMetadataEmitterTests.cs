// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Emitters;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class ReflectionMetadataEmitterTests
{
    [TestMethod]
    public void Emit_WithoutTestClasses_RegistersEmptyTypesAndMethods()
    {
        var metadata = new TestAssemblyMetadata("TestSample", EquatableArray<TestClassMetadata>.Empty);

        string generated = ReflectionMetadataEmitter.Emit(metadata);

        generated.Should().Contain(
            """
                    [ModuleInitializer]
                    internal static void Initialize()
                    {
                        var assembly = typeof(MSTestSourceGeneratedReflectionMetadata).Assembly;
                        var types = Array.Empty<Type>();
                        var testMethods = new Dictionary<Type, MethodInfo[]>();
                        global::Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.ReflectionMetadataHook.Register(assembly, types, testMethods);
                    }
            """.NormalizeNewlines());
    }

    [TestMethod]
    public void Emit_WithSeveralClasses_RegistersEachClassAndEachDistinctBaseTypeOnce()
    {
        var metadata = new TestAssemblyMetadata(
            "TestSample",
            new[]
            {
                CreateClass(
                    "global::Sample.Derived1",
                    ["global::Sample.BaseTests"],
                    CreateMethod("Test1"),
                    CreateMethod("Test2", "global::System.Int32", "global::System.String")),
                CreateClass(
                    "global::Sample.Derived2",
                    ["global::Sample.BaseTests", "global::Sample.OtherBase"],
                    CreateMethod("Odd\\\"Name")),
            }.ToEquatableArray());

        string generated = ReflectionMetadataEmitter.Emit(metadata);

        // The method name carries both a backslash and a double quote: each must be escaped in the emitted literal.
        generated.Should().Contain(
            """
                    [ModuleInitializer]
                    [DynamicDependency(TestClassMemberTypes, typeof(global::Sample.Derived1))]
                    [DynamicDependency(TestClassMemberTypes, typeof(global::Sample.Derived2))]
                    [DynamicDependency(TestClassMemberTypes, typeof(global::Sample.BaseTests))]
                    [DynamicDependency(TestClassMemberTypes, typeof(global::Sample.OtherBase))]
                    internal static void Initialize()
                    {
                        var assembly = typeof(MSTestSourceGeneratedReflectionMetadata).Assembly;
                        var types = new Type[]
                        {
                            typeof(global::Sample.Derived1),
                            typeof(global::Sample.Derived2),
                        };
                        var testMethods = new Dictionary<Type, MethodInfo[]>
                        {
                            [typeof(global::Sample.Derived1)] = new MethodInfo[]
                            {
                                ResolveMethod(typeof(global::Sample.Derived1), "Test1", Type.EmptyTypes),
                                ResolveMethod(typeof(global::Sample.Derived1), "Test2", new Type[] { typeof(global::System.Int32), typeof(global::System.String) }),
                            },
                            [typeof(global::Sample.Derived2)] = new MethodInfo[]
                            {
                                ResolveMethod(typeof(global::Sample.Derived2), "Odd\\\"Name", Type.EmptyTypes),
                            },
                        };
                        global::Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.ReflectionMetadataHook.Register(assembly, types, testMethods);
                    }
            """.NormalizeNewlines());
    }

    private static TestClassMetadata CreateClass(string fullyQualifiedName, string[] baseTypes, params TestMethodMetadata[] methods)
        => new(
            fullyQualifiedName,
            fullyQualifiedName,
            "Sample",
            methods.ToEquatableArray(),
            baseTypes.ToEquatableArray());

    private static TestMethodMetadata CreateMethod(string name, params string[] parameterTypes)
        => new(name, parameterTypes.ToEquatableArray());
}
