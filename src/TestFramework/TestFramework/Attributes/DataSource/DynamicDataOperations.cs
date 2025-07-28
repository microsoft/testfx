// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class DynamicDataOperations
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<object[]> GetData(Type? dynamicDataDeclaringType, string dynamicDataSourceName, object?[] dynamicDataSourceArguments, MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in. If not, default to test method's class type.
        dynamicDataDeclaringType ??= methodInfo.DeclaringType;
        DebugEx.Assert(dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        object? obj =
            (GetPropertyConsideringInheritance(dynamicDataDeclaringType, dynamicDataSourceName) is { } dynamicDataPropertyInfo
                ? GetDataFromProperty(dynamicDataPropertyInfo)
                : GetMethodConsideringInheritance(dynamicDataDeclaringType, dynamicDataSourceName) is { } dynamicDataMethodInfo
                    ? GetDataFromMethod(dynamicDataMethodInfo, dynamicDataSourceArguments)
                    : throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataSourceShouldExistAndBeValid, dynamicDataSourceName, dynamicDataDeclaringType.FullName)))
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
        if (lastParameter is not null &&
            (lastParameter.GetCustomAttribute<ParamArrayAttribute>() is not null ||
                lastParameter.GetCustomAttribute<ParamCollectionAttribute>() is not null))
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
                objects.Add([entry!]);
            }

            data = objects;
            return true;
        }

        data = null;
        return false;
    }

    private static PropertyInfo? GetPropertyConsideringInheritance(Type type, string propertyName)
    {
        // NOTE: Don't use GetRuntimeProperty. It considers inheritance only for instance properties.
        Type? currentType = type;
        while (currentType is not null)
        {
            PropertyInfo? property = currentType.GetProperty(propertyName, DeclaredOnlyLookup);
            if (property is not null)
            {
                return property;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static MethodInfo? GetMethodConsideringInheritance(Type type, string methodName)
    {
        // NOTE: Don't use GetRuntimeMethod. It considers inheritance only for instance methods.
        Type? currentType = type;
        while (currentType is not null)
        {
            MethodInfo? method = currentType.GetMethod(methodName, DeclaredOnlyLookup);
            if (method is not null)
            {
                return method;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }
}
