// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Entry point used by MSTest's source generator to register pre-computed reflection metadata
/// for a test assembly. After a successful <see cref="Register(Assembly, Type[], IReadOnlyDictionary{Type, MethodInfo[]})"/>
/// call, MSTest's discovery and execution paths read metadata from the source-generated data
/// instead of doing reflection at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This API is intended to be invoked from a <c>[ModuleInitializer]</c> in the test assembly
/// emitted by the MSTest source generator. Hand-written code should not depend on it; the
/// shape of the registered metadata is an implementation detail that may evolve with the
/// generator.
/// </para>
/// <para>
/// <b>Discovery limitation.</b> The MSTest source generator only enumerates types that carry
/// <c>[TestClass]</c> declared directly on the type. Test classes that inherit
/// <c>[TestClass]</c> from a base class are <i>not</i> registered through this hook and will
/// not be discovered when the source-generated provider is the active reflection backend.
/// Apply <c>[TestClass]</c> directly to the derived class to opt it back into discovery.
/// Analyzer <c>MSTEST0069</c> (shipped in the MSTest.SourceGeneration package) flags classes
/// that hit this limitation.
/// </para>
/// </remarks>
public static class ReflectionMetadataHook
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif
    private static readonly CompositeSourceGeneratedReflectionDataProvider Composite = new();

    /// <summary>
    /// Publishes source-generated metadata for <paramref name="assembly"/> to the MSTest adapter.
    /// Safe to call from multiple module initializers; later registrations are merged with
    /// earlier ones.
    /// </summary>
    /// <param name="assembly">The test assembly the metadata describes.</param>
    /// <param name="types">All types directly annotated with <c>[TestClass]</c> in the assembly.</param>
    /// <param name="testMethods">
    /// A map from each test class to its <c>[TestMethod]</c>-annotated <see cref="MethodInfo"/>
    /// set. The dictionary and arrays are copied defensively; the caller may mutate the inputs
    /// after the call.
    /// </param>
    public static void Register(Assembly assembly, Type[] types, IReadOnlyDictionary<Type, MethodInfo[]> testMethods)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        if (types is null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        if (testMethods is null)
        {
            throw new ArgumentNullException(nameof(testMethods));
        }

        var typesCopy = (Type[])types.Clone();

        var testMethodsCopy = new Dictionary<Type, MethodInfo[]>(testMethods.Count);
        foreach (KeyValuePair<Type, MethodInfo[]> kvp in testMethods)
        {
            testMethodsCopy[kvp.Key] = (MethodInfo[])kvp.Value.Clone();
        }

        // TypesByName must always match Type.FullName at runtime (see comment in the source
        // generator emitter): compute it on the runtime side from typeof(T).FullName so the
        // generator emits less code and the same FullName conventions are honored for nested
        // and generic types.
        var typesByName = new Dictionary<string, Type>(typesCopy.Length, StringComparer.Ordinal);
        foreach (Type type in typesCopy)
        {
            if (type.FullName is { } fullName)
            {
                typesByName[fullName] = type;
            }
        }

        var provider = new SourceGeneratedReflectionDataProvider
        {
            Assembly = assembly,
            AssemblyName = assembly.GetName().Name ?? string.Empty,
            Types = typesCopy,
            TypesByName = typesByName,
            TypeMethods = testMethodsCopy,
        };

        lock (Lock)
        {
            Composite.Add(provider);

            SourceGeneratorToggle.Enable();

            var reflectionOperations = new SourceGeneratedReflectionOperations(Composite);
            var fileOperations = new SourceGeneratedFileOperations(Composite);

            if (PlatformServiceProvider.Instance is PlatformServiceProvider concreteProvider)
            {
                concreteProvider.SetSourceGeneratedOperations(reflectionOperations, fileOperations);
            }
        }
    }
}
