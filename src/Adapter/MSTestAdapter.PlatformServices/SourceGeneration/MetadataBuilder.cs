// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Fluent builder used by the MSTest source generator to construct and register a
/// source-generated metadata snapshot for a test assembly. Returned by
/// <see cref="ReflectionMetadataHook.ForAssembly(Assembly)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written code should not consume this type — its purpose is to keep the API surface
/// emitted by the source generator narrow and additive. New metadata categories (attributes,
/// constructors, properties, ...) are added via new <c>With*</c> methods rather than by
/// extending a public data-bag.
/// </para>
/// <para>
/// All <c>With*</c> methods copy their inputs defensively so that the caller mutating the
/// supplied arrays or dictionaries after the call cannot tear the registered snapshot.
/// </para>
/// </remarks>
public sealed class MetadataBuilder
{
    private readonly Assembly _assembly;
    private Type[] _types = [];
    private Dictionary<Type, MethodInfo[]> _testMethods = [];

    internal MetadataBuilder(Assembly assembly)
        => _assembly = assembly;

    /// <summary>
    /// Sets the list of <c>[TestClass]</c>-discovered types in the assembly.
    /// </summary>
    /// <param name="types">All types directly annotated with <c>[TestClass]</c>.</param>
    /// <returns>The same builder for chaining.</returns>
    public MetadataBuilder WithTypes(Type[] types)
    {
        if (types is null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        _types = (Type[])types.Clone();
        return this;
    }

    /// <summary>
    /// Sets the test methods (and inherited test methods) the source generator was able to
    /// resolve for each test class.
    /// </summary>
    /// <param name="testMethods">
    /// A map from test class to its <c>[TestMethod]</c>-annotated <see cref="MethodInfo"/> set.
    /// The dictionary contents are copied; the caller may mutate the input after the call.
    /// </param>
    /// <returns>The same builder for chaining.</returns>
    /// <remarks>
    /// The supplied data is informational — it does not replace the reflection-backed
    /// <c>GetDeclaredMethods</c> / <c>GetRuntimeMethods</c> contracts. Generic methods and
    /// methods with by-ref parameters are intentionally excluded by the source generator.
    /// </remarks>
    public MetadataBuilder WithTestMethods(IReadOnlyDictionary<Type, MethodInfo[]> testMethods)
    {
        if (testMethods is null)
        {
            throw new ArgumentNullException(nameof(testMethods));
        }

        var copy = new Dictionary<Type, MethodInfo[]>(testMethods.Count);
        foreach (KeyValuePair<Type, MethodInfo[]> kvp in testMethods)
        {
            copy[kvp.Key] = (MethodInfo[])kvp.Value.Clone();
        }

        _testMethods = copy;
        return this;
    }

    /// <summary>
    /// Publishes the built metadata to the MSTest adapter. After this call, MSTest discovery
    /// and execution see the registered test classes through the source-generated provider.
    /// Safe to call from multiple module initializers.
    /// </summary>
    public void Register()
    {
        // TypesByName must always match Type.FullName at runtime (see comment in the source
        // generator emitter): compute it on the runtime side from typeof(T).FullName so the
        // generator emits less code and the same FullName conventions are honored for nested
        // and generic types.
        var typesByName = new Dictionary<string, Type>(_types.Length, StringComparer.Ordinal);
        foreach (Type type in _types)
        {
            if (type.FullName is { } fullName)
            {
                typesByName[fullName] = type;
            }
        }

        var provider = new SourceGeneratedReflectionDataProvider
        {
            Assembly = _assembly,
            AssemblyName = _assembly.GetName().Name ?? string.Empty,
            Types = _types,
            TypesByName = typesByName,
            TypeMethods = _testMethods,
        };

        ReflectionMetadataHook.Register(provider);
    }
}
