// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Source-generator-backed implementation of <see cref="IReflectionOperations"/>. All metadata is
/// read from a <see cref="SourceGeneratedReflectionDataProvider"/> populated at compile time, which
/// avoids the runtime reflection calls that the regular <see cref="ReflectionOperations"/> performs.
/// When the source-generated data does not contain an entry for a given lookup (e.g. an attribute
/// added by an unaware extension, or a type defined in an assembly that does not participate in
/// source generation), the operation transparently falls back to runtime reflection so that
/// mixed scenarios keep working.
/// </summary>
internal sealed class SourceGeneratedReflectionOperations : IReflectionOperations
{
    private readonly ConcurrentDictionary<ICustomAttributeProvider, Attribute[]> _attributeCache = [];
    private readonly ReflectionOperations _fallback = new();

    public SourceGeneratedReflectionOperations(SourceGeneratedReflectionDataProvider dataProvider)
        => DataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));

    internal SourceGeneratedReflectionDataProvider DataProvider { get; }

    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
        => memberInfo switch
        {
            null => null,
            Type type => GetTypeAttributes(type),
            MethodInfo method => GetMethodAttributes(method),
            _ => _fallback.GetCustomAttributes(memberInfo),
        };

    public object[] GetCustomAttributes(Assembly assembly, Type type)
    {
        object[] sourceGenAttributes = DataProvider.GetAssemblyAttributes(assembly);
        return sourceGenAttributes.Length == 0
            ? _fallback.GetCustomAttributes(assembly, type)
            : [.. sourceGenAttributes.Where(type.IsInstanceOfType)];
    }

    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeConstructors.TryGetValue(classType, out ConstructorInfo[]? constructors)
            ? constructors
            : _fallback.GetDeclaredConstructors(classType);
    }

    // The source-generated TypeMethods dictionary is partial — today it only contains methods
    // annotated with [TestMethod] (and inherited [TestMethod]s), so it cannot satisfy the
    // GetDeclaredMethods contract which is expected to return every method declared on the
    // type. Always delegate to the runtime fallback to preserve correctness; the source-generated
    // data is still used elsewhere (e.g. attribute lookup) to avoid reflection at runtime.
    public MethodInfo[] GetDeclaredMethods(Type classType)
        => _fallback.GetDeclaredMethods(classType);

    public PropertyInfo[] GetDeclaredProperties(Type type)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypeProperties.TryGetValue(type, out PropertyInfo[]? properties)
            ? properties
            : _fallback.GetDeclaredProperties(type);
    }

    public Type[] GetDefinedTypes(Assembly assembly)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        Type[] filtered = [.. data.Types.Where(t => t.Assembly.Equals(assembly))];
        return filtered.Length > 0 ? filtered : _fallback.GetDefinedTypes(assembly);
    }

    // The source-generated TypeMethods dictionary is partial (only [TestMethod]-annotated
    // methods, no generics or by-ref) and the runtime contract requires returning every
    // runtime method. Always delegate to the fallback.
    public MethodInfo[] GetRuntimeMethods(Type type) => _fallback.GetRuntimeMethods(type);

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
    {
        foreach (MethodInfo method in GetRuntimeMethods(declaringType))
        {
            if (method.Name != methodName)
            {
                continue;
            }

            if (!includeNonPublic && !method.IsPublic)
            {
                continue;
            }

            ParameterInfo[] candidateParameters = method.GetParameters();
            if (candidateParameters.Length != parameters.Length)
            {
                continue;
            }

            bool matches = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (candidateParameters[i].ParameterType != parameters[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return _fallback.GetRuntimeMethod(declaringType, methodName, parameters, includeNonPublic);
    }

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

    // `Type.GetType(string)` only resolves assembly-qualified names (or types in the calling
    // assembly / mscorlib). The source-generated `TypesByName` dictionary is keyed by full type
    // name without assembly qualification and aggregates entries across every registered test
    // assembly, so consulting it here could silently bind to a same-named type from the wrong
    // assembly. Match the runtime contract by always delegating; callers that have an assembly
    // in hand use the `GetType(Assembly, string)` overload below for source-generated lookups.
    public Type? GetType(string typeName)
        => _fallback.GetType(typeName);

    public Type? GetType(Assembly assembly, string typeName)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        return data.TypesByName.TryGetValue(typeName, out Type? type) && type.Assembly.Equals(assembly)
            ? type
            : _fallback.GetType(assembly, typeName);
    }

    public object? CreateInstance(Type type, object?[] parameters)
    {
        SourceGeneratedReflectionDataProvider data = DataProvider.GetSnapshot();
        if (!data.TypeConstructorsInvoker.TryGetValue(type, out SourceGeneratedReflectionDataProvider.ConstructorInvoker[]? invokers))
        {
            return _fallback.CreateInstance(type, parameters);
        }

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
                return invoker.Invoker(parameters);
            }
        }

        return _fallback.CreateInstance(type, parameters);
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
