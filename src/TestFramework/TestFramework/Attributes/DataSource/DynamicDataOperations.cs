// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class DynamicDataOperations
{
    private const BindingFlags MemberLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// The members that a dynamic data source type may expose. <see cref="MemberLookup"/> resolves the source member with
    /// <see cref="BindingFlags.NonPublic"/> and <see cref="BindingFlags.FlattenHierarchy"/>, so the source can be an
    /// inherited (for example <see langword="protected"/> <see langword="static"/>) member declared on a base type. Only
    /// <see cref="DynamicallyAccessedMemberTypes.All"/> preserves inherited non-public members across the whole base chain
    /// (the granular <c>NonPublic*</c> flags preserve only members declared directly on the annotated type, and the
    /// <c>NonPublic*WithInherited</c> flags are not available on every target framework's base class library). Annotating a
    /// <see cref="Type"/> with this value keeps the whole member surface alive under trimming and NativeAOT, so
    /// <see cref="DynamicDataAttribute"/> can resolve the source by name at runtime.
    /// </summary>
    /// <remarks>
    /// This mirrors the <c>[DynamicDependency(DynamicallyAccessedMemberTypes.All, ...)]</c> that
    /// <c>MSTest.SourceGeneration</c> already emits for every discovered test class and its base types. It over-preserves
    /// (constructors, events, instance members, and so on) because there is no static-only or member-kind-scoped variant
    /// that also walks the base hierarchy; this is intentional and unavoidable to keep the inherited-source scenario
    /// trim-safe.
    /// </remarks>
    internal const DynamicallyAccessedMemberTypes RequiredMemberTypes = DynamicallyAccessedMemberTypes.All;

    public static IEnumerable<object[]> GetData([DynamicallyAccessedMembers(RequiredMemberTypes)] Type? dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType, string dynamicDataSourceName, object?[] dynamicDataSourceArguments, MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in. If not, default to test method's class type.
        // In the supported trimming/NativeAOT configuration the test class (and its base types) are rooted by the
        // [DynamicDependency(All)] that MSTest.SourceGeneration emits, so MethodInfo.DeclaringType stays trim-safe even
        // though it is not statically annotated with DynamicallyAccessedMembersAttribute (see GetTestMethodDeclaringType).
        dynamicDataDeclaringType ??= GetTestMethodDeclaringType(methodInfo);
        DebugEx.Assert(dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        // Prefer the source-generated accessor when available: it returns the data object without reflecting
        // over the declaring type, which is what makes DynamicData trim/Native-AOT safe. When no accessor was
        // registered (reflection mode) we fall back to reflection below.
        object? obj = (DynamicDataSourceResolver.TryGetData(dynamicDataDeclaringType, dynamicDataSourceName, dynamicDataSourceArguments, out object? resolvedData)
            ? resolvedData
            : GetDataFromMemberByReflection(dynamicDataDeclaringType, dynamicDataSourceType, dynamicDataSourceName, dynamicDataSourceArguments))
            ?? throw new ArgumentNullException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataValueNull,
                    dynamicDataSourceName,
                    dynamicDataDeclaringType.FullName));

        if (!TryGetData(obj, out IEnumerable<object[]>? data))
        {
            throw new ArgumentNullException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataIEnumerableNull,
                    dynamicDataSourceName,
                    dynamicDataDeclaringType.FullName));
        }

        // Data is valid, return it.
        return data;
    }

    private static object? GetDataFromMemberByReflection([DynamicallyAccessedMembers(RequiredMemberTypes)] Type dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType, string dynamicDataSourceName, object?[] dynamicDataSourceArguments)
    {
        switch (dynamicDataSourceType)
        {
            case DynamicDataSourceType.AutoDetect:
#pragma warning disable IDE0045 // Convert to conditional expression - it becomes less readable.
#pragma warning disable IDE0046 // Convert to conditional expression - it becomes less readable.
                if (dynamicDataDeclaringType.GetProperty(dynamicDataSourceName, MemberLookup) is { } dynamicDataPropertyInfo)
                {
                    return GetDataFromProperty(dynamicDataPropertyInfo);
                }
                else if (dynamicDataDeclaringType.GetMethod(dynamicDataSourceName, MemberLookup) is { } dynamicDataMethodInfo)
                {
                    return GetDataFromMethod(dynamicDataMethodInfo, dynamicDataSourceArguments);
                }
                else if (dynamicDataDeclaringType.GetField(dynamicDataSourceName, MemberLookup) is { } dynamicDataFieldInfo)
                {
                    return GetDataFromField(dynamicDataFieldInfo);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataSourceShouldExistAndBeValid, dynamicDataSourceName, dynamicDataDeclaringType.FullName));
                }
#pragma warning restore IDE0046 // Convert to conditional expression
#pragma warning restore IDE0045 // Convert to conditional expression

            case DynamicDataSourceType.Property:
                PropertyInfo property = dynamicDataDeclaringType.GetProperty(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Property} {dynamicDataSourceName}");

                return GetDataFromProperty(property);

            case DynamicDataSourceType.Method:
                MethodInfo method = dynamicDataDeclaringType.GetMethod(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Method} {dynamicDataSourceName}");

                return GetDataFromMethod(method, dynamicDataSourceArguments);

            case DynamicDataSourceType.Field:
                FieldInfo field = dynamicDataDeclaringType.GetField(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Field} {dynamicDataSourceName}");

                return GetDataFromField(field);

            default:
                return null;
        }
    }

    private static object? GetDataFromMethod(MethodInfo method, object?[] arguments)
    {
        if (!method.IsStatic || method.ContainsGenericParameters)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataInvalidMethodLayout,
                    method.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{method.Name}" : method.Name));
        }

        ParameterInfo[] methodParameters = method.GetParameters();
        ParameterInfo? lastParameter = methodParameters.Length > 0 ? methodParameters[methodParameters.Length - 1] : null;

#if NET9_0_OR_GREATER
        if (lastParameter is not null &&
            (lastParameter.GetCustomAttribute<ParamArrayAttribute>() is not null ||
                lastParameter.GetCustomAttribute<ParamCollectionAttribute>() is not null))
#else
        if (lastParameter is not null && lastParameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
#endif
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataInvalidMethodLayout,
                    method.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{method.Name}" : method.Name));
        }

        // Note: the method is static.
        return method.Invoke(null, arguments.Length == 0 ? null : arguments);
    }

    private static object? GetDataFromField(FieldInfo field)
    {
        if (!field.IsStatic)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataInvalidFieldLayout,
                    field.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{field.Name}" : field.Name));
        }

        // Note: the field is static.
        return field.GetValue(null);
    }

    private static object? GetDataFromProperty(PropertyInfo property)
    {
        if (property.GetGetMethod(true) is not { IsStatic: true })
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataInvalidPropertyLayout,
                    property.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{property.Name}" : property.Name));
        }

        // Note: the property getter is static.
        return property.GetValue(null, null);
    }

    private static bool TryGetData(object dataSource, [NotNullWhen(true)] out IEnumerable<object[]>? data)
    {
        if (dataSource is IEnumerable<object[]> enumerableObjectArray)
        {
            data = enumerableObjectArray;
            return true;
        }

        if (dataSource is IEnumerable enumerable and not string)
        {
            List<object[]> objects = [];
            foreach (object? entry in enumerable)
            {
                objects.Add([entry]);
            }

            data = objects;
            return true;
        }

        data = null;
        return false;
    }

    [return: DynamicallyAccessedMembers(RequiredMemberTypes)]
    [UnconditionalSuppressMessage("Trimming", "IL2073:Value returned does not have matching annotations", Justification = "In the supported trimming/NativeAOT configuration, test classes are source-generated: MSTest.SourceGeneration emits [DynamicDependency(DynamicallyAccessedMemberTypes.All, ...)] for every discovered test class and its base types, so MethodInfo.DeclaringType and its members are already rooted. This fallback only runs for the test method's own declaring type; it does not claim safety for unsupported reflection-only trimmed callers that discover tests without the source generator.")]
    internal static Type? GetTestMethodDeclaringType(MethodInfo methodInfo)
        => methodInfo.DeclaringType;
}
