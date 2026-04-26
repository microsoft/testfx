// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class DynamicDataOperations
{
    private const BindingFlags MemberLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    public static IEnumerable<object[]> GetData(Type? dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType, string dynamicDataSourceName, object?[] dynamicDataSourceArguments, MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in. If not, default to test method's class type.
        dynamicDataDeclaringType ??= methodInfo.DeclaringType;
        DebugEx.Assert(dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        object? obj = null;

        switch (dynamicDataSourceType)
        {
            case DynamicDataSourceType.AutoDetect:
#pragma warning disable IDE0045 // Convert to conditional expression - it becomes less readable.
                if (dynamicDataDeclaringType.GetProperty(dynamicDataSourceName, MemberLookup) is { } dynamicDataPropertyInfo)
                {
                    obj = GetDataFromProperty(dynamicDataPropertyInfo);
                }
                else if (dynamicDataDeclaringType.GetMethod(dynamicDataSourceName, MemberLookup) is { } dynamicDataMethodInfo)
                {
                    obj = GetDataFromMethod(dynamicDataMethodInfo, dynamicDataSourceArguments);
                }
                else if (dynamicDataDeclaringType.GetField(dynamicDataSourceName, MemberLookup) is { } dynamicDataFieldInfo)
                {
                    obj = GetDataFromField(dynamicDataFieldInfo);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataSourceShouldExistAndBeValid, dynamicDataSourceName, dynamicDataDeclaringType.FullName));
                }
#pragma warning restore IDE0045 // Convert to conditional expression

                break;
            case DynamicDataSourceType.Property:
                PropertyInfo property = dynamicDataDeclaringType.GetProperty(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Property} {dynamicDataSourceName}");

                obj = GetDataFromProperty(property);
                break;

            case DynamicDataSourceType.Method:
                MethodInfo method = dynamicDataDeclaringType.GetMethod(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Method} {dynamicDataSourceName}");

                obj = GetDataFromMethod(method, dynamicDataSourceArguments);
                break;

            case DynamicDataSourceType.Field:
                FieldInfo field = dynamicDataDeclaringType.GetField(dynamicDataSourceName, MemberLookup)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Field} {dynamicDataSourceName}");

                obj = GetDataFromField(field);
                break;
        }

        if (obj == null)
        {
            throw new ArgumentNullException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataValueNull,
                    dynamicDataSourceName,
                    dynamicDataDeclaringType.FullName));
        }

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
}
