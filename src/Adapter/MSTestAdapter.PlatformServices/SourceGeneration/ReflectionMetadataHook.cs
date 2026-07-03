// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// <b>Infrastructure.</b> Entry point used by the MSTest source generator to register pre-computed
/// reflection metadata for a test assembly. After a successful
/// <see cref="Register(Assembly, Type[], IReadOnlyDictionary{Type, MethodInfo[]})"/> call, MSTest's
/// discovery and execution paths read metadata from the source-generated data instead of doing
/// reflection at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is not intended to be used directly from application code.</b> It is public only
/// because the MSTest source generator emits a <c>[ModuleInitializer]</c> in the test assembly
/// that needs to call it across the assembly boundary, and module initializers cannot use
/// <c>internal</c> APIs from a different assembly. The signature and behaviour of this hook are
/// implementation details that may evolve with the generator without a major version bump; do
/// not hand-roll a call to <see cref="Register(Assembly, Type[], IReadOnlyDictionary{Type, MethodInfo[]})"/> from your own code.
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
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ReflectionMetadataHook
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif
    private static readonly CompositeSourceGeneratedReflectionDataProvider Composite = new();

    /// <summary>
    /// <b>Infrastructure.</b> Publishes source-generated metadata for <paramref name="assembly"/>
    /// to the MSTest adapter. Safe to call from multiple module initializers; later registrations
    /// are merged with earlier ones.
    /// </summary>
    /// <param name="assembly">The test assembly the metadata describes.</param>
    /// <param name="types">All types directly annotated with <c>[TestClass]</c> in the assembly.</param>
    /// <param name="testMethods">
    /// A map from each test class to its <c>[TestMethod]</c>-annotated <see cref="MethodInfo"/>
    /// set. Ownership transfers to the adapter: the source generator hands over freshly-built
    /// collections and must not mutate them after the call (see the remarks on the full overload).
    /// </param>
    /// <remarks>
    /// Do not call this method from hand-written code; it is meant to be invoked exclusively from
    /// the <c>[ModuleInitializer]</c> emitted by the MSTest source generator. The signature is
    /// not covered by API-stability guarantees.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(Assembly assembly, Type[] types, IReadOnlyDictionary<Type, MethodInfo[]> testMethods)
        => Register(assembly, types, testMethods, EmptyTypeAttributes, []);

    /// <summary>
    /// <b>Infrastructure.</b> Publishes source-generated metadata for <paramref name="assembly"/>
    /// to the MSTest adapter, including pre-materialized type-level and assembly-level attributes
    /// so the adapter serves them without runtime reflection. Safe to call from multiple module
    /// initializers; later registrations are merged with earlier ones.
    /// </summary>
    /// <param name="assembly">The test assembly the metadata describes.</param>
    /// <param name="types">All types directly annotated with <c>[TestClass]</c> in the assembly.</param>
    /// <param name="testMethods">A map from each test class to its <c>[TestMethod]</c> set.</param>
    /// <param name="typeAttributes">
    /// A map from each test class to its pre-inflated <see cref="Attribute"/> instances. The adapter
    /// returns these from <c>GetCustomAttributes(Type)</c> instead of reflecting at runtime.
    /// </param>
    /// <param name="assemblyAttributes">Pre-inflated assembly-level attribute instances.</param>
    /// <remarks>
    /// Do not call this method from hand-written code; it is meant to be invoked exclusively from
    /// the <c>[ModuleInitializer]</c> emitted by the MSTest source generator.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(
        Assembly assembly,
        Type[] types,
        IReadOnlyDictionary<Type, MethodInfo[]> testMethods,
        IReadOnlyDictionary<Type, Attribute[]> typeAttributes,
        object[] assemblyAttributes)
        => Register(assembly, types, testMethods, typeAttributes, assemblyAttributes, EmptyMethodInvokers, EmptyConstructorInvokers, EmptyPropertySetters);

    /// <summary>
    /// <b>Infrastructure.</b> Publishes source-generated metadata for <paramref name="assembly"/>
    /// to the MSTest adapter, including the delegate-based invokers that let the adapter run tests
    /// without runtime reflection: per-method invokers (replacing <c>MethodInfo.Invoke</c>),
    /// per-type constructor invokers (replacing <c>Activator.CreateInstance</c>), and per-property
    /// setters (replacing <c>PropertyInfo.SetValue</c>). Safe to call from multiple module
    /// initializers; later registrations are merged with earlier ones.
    /// </summary>
    /// <param name="assembly">The test assembly the metadata describes.</param>
    /// <param name="types">All types directly annotated with <c>[TestClass]</c> in the assembly.</param>
    /// <param name="testMethods">A map from each test class to its <c>[TestMethod]</c> set.</param>
    /// <param name="typeAttributes">A map from each test class to its pre-inflated <see cref="Attribute"/> instances.</param>
    /// <param name="assemblyAttributes">Pre-inflated assembly-level attribute instances.</param>
    /// <param name="methodInvokers">
    /// A map from each test/fixture <see cref="MethodInfo"/> to a delegate that invokes it directly.
    /// The delegate returns a non-null <see cref="Task"/> representing the method's completion: the
    /// source generator normalizes every shape to a <see cref="Task"/> (<c>void</c>/synchronous yield
    /// <see cref="Task.CompletedTask"/>, a <c>ValueTask</c> is converted to a <see cref="Task"/>, and
    /// any return value is discarded).
    /// </param>
    /// <param name="constructorInvokers">
    /// A map from each test class to its constructor invokers. Each entry pairs the constructor's
    /// parameter types with a delegate that constructs the instance from an argument array.
    /// </param>
    /// <param name="propertySetters">
    /// A map from each settable property (today: the <c>TestContext</c> property) to a delegate that
    /// assigns it directly.
    /// </param>
    /// <remarks>
    /// <para>
    /// Do not call this method from hand-written code; it is meant to be invoked exclusively from
    /// the <c>[ModuleInitializer]</c> emitted by the MSTest source generator.
    /// </para>
    /// <para>
    /// <b>Ownership transfer.</b> The adapter takes ownership of every collection passed in
    /// (the <paramref name="types"/> array, the <paramref name="assemblyAttributes"/> array, and
    /// each dictionary with its value arrays) and stores them without cloning. Callers MUST hand
    /// over freshly-built collections and MUST NOT mutate them after the call returns; the source
    /// generator (the only intended caller) already emits fresh, throwaway collections that satisfy
    /// this. This is a contract about ownership and mutation, not caller identity: it trades the
    /// previous defensive copies for zero-copy startup on the understanding that the inputs are the
    /// adapter's to keep.
    /// </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(
        Assembly assembly,
        Type[] types,
        IReadOnlyDictionary<Type, MethodInfo[]> testMethods,
        IReadOnlyDictionary<Type, Attribute[]> typeAttributes,
        object[] assemblyAttributes,
        IReadOnlyDictionary<MethodInfo, Func<object?, object?[]?, object?>> methodInvokers,
        IReadOnlyDictionary<Type, ConstructorInvokerInfo[]> constructorInvokers,
        IReadOnlyDictionary<PropertyInfo, Action<object?, object?>> propertySetters)
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

        if (typeAttributes is null)
        {
            throw new ArgumentNullException(nameof(typeAttributes));
        }

        if (assemblyAttributes is null)
        {
            throw new ArgumentNullException(nameof(assemblyAttributes));
        }

        if (methodInvokers is null)
        {
            throw new ArgumentNullException(nameof(methodInvokers));
        }

        if (constructorInvokers is null)
        {
            throw new ArgumentNullException(nameof(constructorInvokers));
        }

        if (propertySetters is null)
        {
            throw new ArgumentNullException(nameof(propertySetters));
        }

        // Ownership transfer (see the remarks on this method): the source generator hands over
        // freshly-built, throwaway collections and never mutates them after the call, so we store
        // them directly instead of cloning. Arrays are kept by reference; dictionaries are reused
        // as-is when already concrete and only materialized when the caller passed some other
        // IReadOnlyDictionary implementation.
        Dictionary<Type, MethodInfo[]> testMethodsMap = AsOwnedDictionary(testMethods);
        Dictionary<Type, Attribute[]> typeAttributesMap = AsOwnedDictionary(typeAttributes);
        Dictionary<MethodInfo, Func<object?, object?[]?, object?>> methodInvokersMap = AsOwnedDictionary(methodInvokers);
        Dictionary<PropertyInfo, Action<object?, object?>> propertySettersMap = AsOwnedDictionary(propertySetters);

        // ConstructorInvokerInfo (public struct) has to be projected onto the adapter's internal
        // ConstructorInvoker type; this is a representation change, not a defensive copy, and the
        // parameter-type arrays are taken by reference.
        var constructorInvokersMap = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>(constructorInvokers.Count);
        foreach (KeyValuePair<Type, ConstructorInvokerInfo[]> kvp in constructorInvokers)
        {
            var invokers = new SourceGeneratedReflectionDataProvider.ConstructorInvoker[kvp.Value.Length];
            for (int i = 0; i < kvp.Value.Length; i++)
            {
                ConstructorInvokerInfo info = kvp.Value[i];
                invokers[i] = new SourceGeneratedReflectionDataProvider.ConstructorInvoker
                {
                    Parameters = info.ParameterTypes,
                    Invoker = info.Invoker,
                };
            }

            constructorInvokersMap[kvp.Key] = invokers;
        }

        // TypesByName must always match Type.FullName at runtime (see comment in the source
        // generator emitter): compute it on the runtime side from typeof(T).FullName so the
        // generator emits less code and the same FullName conventions are honored for nested
        // and generic types.
        var typesByName = new Dictionary<string, Type>(types.Length, StringComparer.Ordinal);
        foreach (Type type in types)
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
            Types = types,
            TypesByName = typesByName,
            TypeMethods = testMethodsMap,
            TypeAttributes = typeAttributesMap,
            AssemblyAttributes = assemblyAttributes,
            TypeMethodInvokers = methodInvokersMap,
            TypeConstructorsInvoker = constructorInvokersMap,
            TypePropertySetters = propertySettersMap,
        };

        lock (Lock)
        {
            Composite.Add(provider);

            var reflectionOperations = new SourceGeneratedReflectionOperations(Composite);
            var fileOperations = new SourceGeneratedFileOperations(Composite);

            if (PlatformServiceProvider.Instance is PlatformServiceProvider concreteProvider)
            {
                concreteProvider.SetSourceGeneratedOperations(reflectionOperations, fileOperations);
            }
        }
    }

    // Reuses the caller-provided dictionary when it is already a concrete Dictionary<,> (the shape
    // the source generator always emits), honoring the ownership-transfer contract with zero
    // copying. Any other IReadOnlyDictionary implementation is materialized once so the provider
    // still owns a concrete instance. Values are always taken by reference.
    private static Dictionary<TKey, TValue> AsOwnedDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
        where TKey : notnull
    {
        if (source is Dictionary<TKey, TValue> concrete)
        {
            return concrete;
        }

        var copy = new Dictionary<TKey, TValue>(source.Count);
        foreach (KeyValuePair<TKey, TValue> kvp in source)
        {
            copy[kvp.Key] = kvp.Value;
        }

        return copy;
    }

    private static readonly Dictionary<Type, Attribute[]> EmptyTypeAttributes = [];

    private static readonly Dictionary<MethodInfo, Func<object?, object?[]?, object?>> EmptyMethodInvokers = [];

    private static readonly Dictionary<Type, ConstructorInvokerInfo[]> EmptyConstructorInvokers = [];

    private static readonly Dictionary<PropertyInfo, Action<object?, object?>> EmptyPropertySetters = [];
}
