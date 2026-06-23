// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Source-generator-backed implementation of <see cref="IReflectionOperations"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope of the source-generated data.</b> The shipping MSTest source generator's
/// <c>[ModuleInitializer]</c> calls <see cref="ReflectionMetadataHook.Register(Assembly, Type[], IReadOnlyDictionary{Type, MethodInfo[]})"/>
/// with only: the assembly, the <c>[TestClass]</c> types, and their <c>[TestMethod]</c>-annotated
/// <see cref="MethodInfo"/>s. The AOT generator additionally publishes pre-materialized type-level
/// and assembly-level attributes via the richer
/// <see cref="ReflectionMetadataHook.Register(Assembly, Type[], IReadOnlyDictionary{Type, MethodInfo[]}, IReadOnlyDictionary{Type, Attribute[]}, object[])"/>
/// overload. Anything still not populated (method attributes, constructors, properties, navigation
/// data) falls back to runtime reflection, so the payload remains "type / test-method rooting +
/// trimmer hints + materialized type/assembly attributes" rather than a full reflection replacement.
/// </para>
/// <para>
/// <b>Why a fallback exists at all.</b> Each fallback in this class falls into one of three
/// categories. Cataloguing them here so that adding a new fallback is a deliberate choice,
/// not an accidental hidden one:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Category A — Generator-gap fallback.</b> The corresponding field on
///     <see cref="SourceGeneratedReflectionDataProvider"/> is not populated by today's
///     emitter, so every call falls back to runtime reflection. These are closable: extend
///     the emitter to populate the field and the fallback stops firing. Today this covers
///     <see cref="GetCustomAttributes(MemberInfo)"/> (for <see cref="Type"/> and
///     <see cref="MethodInfo"/>), <see cref="GetCustomAttributes(Assembly, Type)"/>,
///     <see cref="GetDeclaredConstructors"/>, <see cref="GetDeclaredProperties"/>,
///     <see cref="GetRuntimeProperty"/>, and <see cref="CreateInstance"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Category B — Contract-mismatch fallback.</b> The interface returns "every method"
///     (or similar), but the source generator intentionally models only test methods. Always
///     delegating is correct here; the only way to avoid the fallback would be to either
///     change the contract or to enumerate all methods at compile time (which is exactly
///     what reflection already does at runtime). Today this covers
///     <see cref="GetDeclaredMethods"/>, <see cref="GetRuntimeMethods"/>, and
///     <see cref="GetRuntimeMethod"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Category C — Cross-assembly fallback.</b> The lookup targets an assembly that did
///     not participate in source generation (test framework, adapter, extensions, or assets
///     packed without the generator). No amount of generator work eliminates this — assemblies
///     we do not compile cannot register source-gen data. Today this covers
///     <see cref="GetType(string)"/>, the no-match branch of <see cref="GetType(Assembly, string)"/>,
///     <see cref="GetDefinedTypes"/>, and the non-<see cref="Type"/>/non-<see cref="MethodInfo"/>
///     branch of <see cref="GetCustomAttributes(MemberInfo)"/>.
///     </description>
///   </item>
/// </list>
/// <para>
/// <b>Trim/AOT safety.</b> Because most reads still fall through to reflection at runtime,
/// trim safety relies on the <c>[DynamicDependency(All, typeof(T))]</c> attributes emitted
/// by the source generator for each <c>[TestClass]</c> and each of its accessible base
/// types. Those preserve the members reflection then enumerates.
/// </para>
/// <para>
/// <b>Adding a new fallback?</b> Mark the method with a <c>// Category A/B/C</c> comment and
/// explain which one and why. The blind-corner risk is fallbacks that look intentional but
/// were really just oversights — labelling each call site makes the design choice visible.
/// </para>
/// </remarks>
internal sealed class SourceGeneratedReflectionOperations : IReflectionOperations
{
    private readonly ConcurrentDictionary<ICustomAttributeProvider, Attribute[]> _attributeCache = [];
    private readonly ReflectionOperations _fallback = new();

    public SourceGeneratedReflectionOperations(SourceGeneratedReflectionDataProvider dataProvider)
        => DataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));

    internal SourceGeneratedReflectionDataProvider DataProvider { get; }

    // Category A for Type / MethodInfo (TypeAttributes / TypeMethodAttributes are not emitted).
    // Category C for every other MemberInfo subtype — the source generator never models
    // FieldInfo, EventInfo, ParameterInfo, etc. and we have no plans to.
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
        => memberInfo switch
        {
            null => null,
            Type type => GetTypeAttributes(type),
            MethodInfo method => GetMethodAttributes(method),
            _ => _fallback.GetCustomAttributes(memberInfo),
        };

    // Category A: AssemblyAttributes is not populated by today's emitter, so this always
    // falls through unless a future emitter snapshots assembly-level attributes.
    public object[] GetCustomAttributes(Assembly assembly, Type type)
    {
        object[] sourceGenAttributes = DataProvider.GetAssemblyAttributes(assembly);
        return sourceGenAttributes.Length == 0
            ? _fallback.GetCustomAttributes(assembly, type)
            : [.. sourceGenAttributes.Where(type.IsInstanceOfType)];
    }

    // Category A: TypeConstructors is not populated by today's emitter.
    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeConstructors.TryGetValue(classType, out ConstructorInfo[]? constructors)
            ? constructors
            : _fallback.GetDeclaredConstructors(classType);
    }

    // Category B: the source-generated TypeMethods dictionary is partial — today it only
    // contains methods annotated with [TestMethod] (and inherited [TestMethod]s), so it
    // cannot satisfy the GetDeclaredMethods contract which is expected to return every
    // method declared on the type. Always delegate to the runtime fallback to preserve
    // correctness; the source-generated data is still used elsewhere (e.g. attribute
    // lookup) to avoid reflection at runtime.
    public MethodInfo[] GetDeclaredMethods(Type classType)
        => _fallback.GetDeclaredMethods(classType);

    // Category A: TypeProperties is not populated by today's emitter.
    public PropertyInfo[] GetDeclaredProperties(Type type)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeProperties.TryGetValue(type, out PropertyInfo[]? properties)
            ? properties
            : _fallback.GetDeclaredProperties(type);
    }

    // Category C: assemblies that did not opt into source generation (typically the test
    // framework, the adapter, MSTest extensions, or test assets packed without the
    // generator) have no entries in `data.Types`. Fall back so those assemblies behave the
    // same as in non-source-gen mode.
    public Type[] GetDefinedTypes(Assembly assembly)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        Type[] filtered = [.. data.Types.Where(t => t.Assembly.Equals(assembly))];
        return filtered.Length > 0 ? filtered : _fallback.GetDefinedTypes(assembly);
    }

    // Category B: the source-generated TypeMethods dictionary is partial (only
    // [TestMethod]-annotated methods, no generics or by-ref) and the runtime contract
    // requires returning every runtime method. Always delegate to the fallback.
    public MethodInfo[] GetRuntimeMethods(Type type) => _fallback.GetRuntimeMethods(type);

    // Category B: the source-generator does not pre-resolve arbitrary methods (the
    // partial TypeMethods dictionary only carries [TestMethod]-annotated entries). Doing
    // our own GetRuntimeMethods + parameter-match here would (a) duplicate the scan that
    // the fallback already performs on miss and (b) diverge from Type.GetMethod's binder
    // semantics (overload resolution, generic / by-ref handling). Delegate so callers get
    // the same selection rules as reflection mode.
    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
        => _fallback.GetRuntimeMethod(declaringType, methodName, parameters, includeNonPublic);

    // Category A: TypePropertiesByName is not populated by today's emitter.
    public PropertyInfo? GetRuntimeProperty(Type classType, string propertyName, bool includeNonPublic)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        if (data.TypePropertiesByName.TryGetValue(classType, out Dictionary<string, PropertyInfo>? properties)
            && properties.TryGetValue(propertyName, out PropertyInfo? property))
        {
            if (!includeNonPublic)
            {
                bool isPublic = property.GetMethod?.IsPublic is true || property.SetMethod?.IsPublic is true;
                if (!isPublic)
                {
                    return null;
                }
            }

            return property;
        }

        return _fallback.GetRuntimeProperty(classType, propertyName, includeNonPublic);
    }

    // Category C: `Type.GetType(string)` only resolves assembly-qualified names (or types
    // in the calling assembly / mscorlib). The source-generated `TypesByName` dictionary is
    // keyed by full type name without assembly qualification and aggregates entries across
    // every registered test assembly, so consulting it here could silently bind to a
    // same-named type from the wrong assembly. Match the runtime contract by always
    // delegating; callers that have an assembly in hand use the `GetType(Assembly, string)`
    // overload below for source-generated lookups.
    public Type? GetType(string typeName)
        => _fallback.GetType(typeName);

    // Category C: assemblies that did not opt into source generation (and types in
    // opted-in assemblies that the generator skipped, such as open generics) are not in
    // TypesByName; fall back so cross-assembly lookups still resolve. The composite routes
    // this through the per-provider TryGetTypeByName override so two assemblies with the
    // same fully-qualified type name do not shadow each other.
    public Type? GetType(Assembly assembly, string typeName)
        => DataProvider.TryGetTypeByName(assembly, typeName, out Type? type)
            ? type
            : _fallback.GetType(assembly, typeName);

    // Category A: TypeConstructorsInvoker is now populated by the AOT emitter (via the
    // MSTestReflectionMetadata registry). When present, the invoker path avoids both
    // Activator.CreateInstance and the trim-unfriendly constructor reflection. Falls back
    // to reflection for types the generator did not register (open generics, cross-assembly).
    public object? CreateInstance(Type type, object?[] parameters)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeConstructorsInvoker.TryGetValue(type, out SourceGeneratedReflectionDataProvider.ConstructorInvoker[]? invokers)
            && TryInvokeMatchingConstructor(invokers, parameters, out object? instance)
                ? instance
                : _fallback.CreateInstance(type, parameters);
    }

    // Returns a delegate that constructs `type` via the source-generated constructor invoker,
    // or null when the generator did not register the type (caller falls back to reflection).
    // The delegate routes through CreateInstance so a no-match still falls back to reflection
    // (Activator.CreateInstance) instead of throwing.
    public Func<object?[]?, object>? GetConstructorInvoker(Type type)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeConstructorsInvoker.TryGetValue(type, out SourceGeneratedReflectionDataProvider.ConstructorInvoker[]? invokers) && invokers.Length > 0
            ? args => CreateInstance(type, args ?? [])!
            : null;
    }

    public Func<object?, object?[]?, object?>? GetTestMethodInvoker(MethodInfo method)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeMethodInvokers.TryGetValue(method, out Func<object?, object?[]?, object?>? invoker)
            ? invoker
            : null;
    }

    public Action<object?, object?>? GetPropertySetter(PropertyInfo property)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypePropertySetters.TryGetValue(property, out Action<object?, object?>? setter)
            ? setter
            : null;
    }

    private static bool TryInvokeMatchingConstructor(
        SourceGeneratedReflectionDataProvider.ConstructorInvoker[] invokers,
        object?[] parameters,
        out object? instance)
    {
        foreach (SourceGeneratedReflectionDataProvider.ConstructorInvoker invoker in invokers)
        {
            if (invoker.Parameters.Length != parameters.Length)
            {
                continue;
            }

            bool matches = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                object? argument = parameters[i];
                Type expected = invoker.Parameters[i];

                if (argument is null)
                {
                    if (expected.IsValueType && Nullable.GetUnderlyingType(expected) is null)
                    {
                        matches = false;
                        break;
                    }
                }
                else if (!expected.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                instance = invoker.Invoker(parameters);
                return true;
            }
        }

        instance = null;
        return false;
    }

    public bool IsAttributeDefined<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
        => attributeProvider is null
            ? throw new ArgumentNullException(nameof(attributeProvider))
            : GetCustomAttributesCached(attributeProvider).OfType<TAttribute>().Any();

    public TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
        => GetCustomAttributesCached(attributeProvider).OfType<TAttribute>().FirstOrDefault();

    public TAttribute? GetSingleAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
    {
        TAttribute[] matches = [.. GetCustomAttributesCached(attributeProvider).OfType<TAttribute>().Take(2)];
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Found multiple attributes of type '{0}' when only one was expected.", typeof(TAttribute))),
        };
    }

    public IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider)
        where TAttributeType : Attribute
        => GetCustomAttributesCached(attributeProvider).OfType<TAttributeType>();

    public Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider)
        => attributeProvider is null
            ? throw new ArgumentNullException(nameof(attributeProvider))
            : attributeProvider is MemberInfo or Assembly
                ? _attributeCache.GetOrAdd(attributeProvider, GetAttributesForProvider)
                : throw new ArgumentException(
                    $"Unsupported attribute provider type: {attributeProvider.GetType()}. Only MemberInfo and Assembly are supported.",
                    nameof(attributeProvider));

    private Attribute[] GetAttributesForProvider(ICustomAttributeProvider provider)
        => provider switch
        {
            Type type => GetTypeAttributes(type),
            MethodInfo method => GetMethodAttributes(method),
            Assembly assembly => GetAssemblyAttributesForProvider(assembly),
            MemberInfo memberInfo => GetMemberAttributesFromReflection(memberInfo),
            _ => [],
        };

    private Attribute[] GetAssemblyAttributesForProvider(Assembly assembly)
    {
        object[] sourceGen = DataProvider.GetAssemblyAttributes(assembly);
        return sourceGen.Length > 0
            ? [.. sourceGen.OfType<Attribute>()]
            : GetAssemblyAttributesFromReflection(assembly);
    }

    public bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => method.DeclaringType?.Assembly.Equals(type.Assembly) ?? false;

    internal void ClearCache() => _attributeCache.Clear();

    private Attribute[] GetTypeAttributes(Type type)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeAttributes.TryGetValue(type, out Attribute[]? attributes)
            ? attributes
            : GetMemberAttributesFromReflection(type);
    }

    private Attribute[] GetMethodAttributes(MethodInfo method)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeMethodAttributes.TryGetValue(method, out Attribute[]? attributes)
            ? attributes
            : GetMemberAttributesFromReflection(method);
    }

    // Bypass _fallback.GetCustomAttributesCached because its internal NotCachedReflectionAccessor
    // routes through PlatformServiceProvider.Instance.ReflectionOperations, which after SetMetadata
    // resolves back to this SourceGeneratedReflectionOperations instance — causing infinite mutual recursion.
    // Use direct reflection (_fallback.GetCustomAttributes does not go through that indirection).
    private Attribute[] GetMemberAttributesFromReflection(MemberInfo memberInfo)
    {
        object[]? attributes = _fallback.GetCustomAttributes(memberInfo);
        return attributes switch
        {
            null => [],
            Attribute[] attributeArray => attributeArray,
            _ => [.. attributes.OfType<Attribute>()],
        };
    }

    private Attribute[] GetAssemblyAttributesFromReflection(Assembly assembly)
    {
        object[] attributes = _fallback.GetCustomAttributes(assembly, typeof(Attribute));
        return attributes is Attribute[] attributeArray
            ? attributeArray
            : [.. attributes.OfType<Attribute>()];
    }
}
