// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal class DynamicDataOperations : IDynamicDataOperations
{
    public IEnumerable<object[]> GetData(Type? _dynamicDataDeclaringType, DynamicDataSourceType _dynamicDataSourceType, string _dynamicDataSourceName, MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in. If not, default to test method's class type.
        _dynamicDataDeclaringType ??= methodInfo.DeclaringType;
        DebugEx.Assert(_dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        object? obj = null;

        switch (_dynamicDataSourceType)
        {
            case DynamicDataSourceType.AutoDetect:
#pragma warning disable IDE0045 // Convert to conditional expression - it becomes less readable.
                if (GetPropertyConsideringInheritance(_dynamicDataDeclaringType, _dynamicDataSourceName) is { } dynamicDataPropertyInfo)
                {
                    obj = GetDataFromProperty(dynamicDataPropertyInfo);
                }
                else if (GetMethodConsideringInheritance(_dynamicDataDeclaringType, _dynamicDataSourceName) is { } dynamicDataMethodInfo)
                {
                    obj = GetDataFromMethod(dynamicDataMethodInfo);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.DynamicDataSourceShouldExistAndBeValid, _dynamicDataSourceName, _dynamicDataDeclaringType.FullName));
                }
#pragma warning restore IDE0045 // Convert to conditional expression

                break;
            case DynamicDataSourceType.Property:
                PropertyInfo property = GetPropertyConsideringInheritance(_dynamicDataDeclaringType, _dynamicDataSourceName)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Property} {_dynamicDataSourceName}");

                obj = GetDataFromProperty(property);
                break;

            case DynamicDataSourceType.Method:
                MethodInfo method = GetMethodConsideringInheritance(_dynamicDataDeclaringType, _dynamicDataSourceName)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Method} {_dynamicDataSourceName}");

                obj = GetDataFromMethod(method);
                break;
        }

        if (obj == null)
        {
            throw new ArgumentNullException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataValueNull,
                    _dynamicDataSourceName,
                    _dynamicDataDeclaringType.FullName));
        }

        if (!TryGetData(obj, out IEnumerable<object[]>? data))
        {
            throw new ArgumentNullException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataIEnumerableNull,
                    _dynamicDataSourceName,
                    _dynamicDataDeclaringType.FullName));
        }

        // Data is valid, return it.
        return data;
    }

    private static object? GetDataFromMethod(MethodInfo method)
    {
        if (!method.IsStatic
            || method.ContainsGenericParameters
            || method.GetParameters().Length > 0)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataInvalidPropertyLayout,
                    method.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{method.Name}" : method.Name));
        }

        // Note: the method is static and takes no parameters.
        return method.Invoke(null, null);
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

    /// <inheritdoc />
    public string? GetDisplayName(string? DynamicDataDisplayName, Type? DynamicDataDisplayNameDeclaringType, MethodInfo methodInfo, object?[]? data)
    {
        if (DynamicDataDisplayName != null)
        {
            Type? dynamicDisplayNameDeclaringType = DynamicDataDisplayNameDeclaringType ?? methodInfo.DeclaringType;
            DebugEx.Assert(dynamicDisplayNameDeclaringType is not null, "Declaring type of test data cannot be null.");

            MethodInfo method = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethod(dynamicDisplayNameDeclaringType, DynamicDataDisplayName)
                ?? throw new ArgumentNullException($"{DynamicDataSourceType.Method} {DynamicDataDisplayName}");
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(MethodInfo) ||
                parameters[1].ParameterType != typeof(object[]) ||
                method.ReturnType != typeof(string) ||
                !method.IsStatic ||
                !method.IsPublic
                ? throw new ArgumentNullException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        FrameworkMessages.DynamicDataDisplayName,
                        DynamicDataDisplayName,
                        nameof(String),
                        string.Join(", ", nameof(MethodInfo), typeof(object[]).Name)))
                : method.Invoke(null, [methodInfo, data]) as string;
        }

        if (data != null)
        {
            // We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
            // so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
            // you will get empty string while with the call you will get "null,a".
            return string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, methodInfo.Name,
                string.Join(",", data.AsEnumerable()));
        }

        return null;
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
            List<object[]> objects = new();
            foreach (object? entry in enumerable)
            {
                if (entry is null)
                {
                    data = null;
                    return false;
                }

                if (!TryHandleTupleDataSource(entry, objects))
                {
                    objects.Add(new[] { entry });
                }
            }

            data = objects;
            return true;
        }

        data = null;
        return false;
    }

    private static bool TryHandleTupleDataSource(object data, List<object[]> objects)
    {
#if NET471_OR_GREATER || NETCOREAPP
        if (data is ITuple tuple
            && (objects.Count == 0 || objects[^1].Length == tuple.Length))
        {
            object[] array = new object[tuple.Length];
            for (int i = 0; i < tuple.Length; i++)
            {
                array[i] = tuple[i]!;
            }

            objects.Add(array);
            return true;
        }
#else
        Type type = data.GetType();
        if (IsTupleOrValueTuple(data.GetType(), out int tupleSize)
            && (objects.Count == 0 || objects[objects.Count - 1].Length == tupleSize))
        {
            object[] array = new object[tupleSize];
            for (int i = 0; i < tupleSize; i++)
            {
                array[i] = type.GetField($"Item{i + 1}")?.GetValue(data)!;
            }

            objects.Add(array);
            return true;
        }
#endif

        return false;
    }

#if !NET471_OR_GREATER && !NETCOREAPP
    private static bool IsTupleOrValueTuple(Type type, out int tupleSize)
    {
        tupleSize = 0;
        if (!type.IsGenericType)
        {
            return false;
        }

        Type genericTypeDefinition = type.GetGenericTypeDefinition();

        if (genericTypeDefinition == typeof(Tuple<>) ||
            genericTypeDefinition == typeof(Tuple<,>) ||
            genericTypeDefinition == typeof(Tuple<,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,,,>) ||
            genericTypeDefinition == typeof(Tuple<,,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        if (genericTypeDefinition == typeof(ValueTuple<>) ||
            genericTypeDefinition == typeof(ValueTuple<,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        return false;
    }
#endif

    private static PropertyInfo? GetPropertyConsideringInheritance(Type type, string propertyName)
    {
        // NOTE: Don't use GetRuntimeProperty. It considers inheritance only for instance properties.
        Type? currentType = type;
        while (currentType is not null)
        {
            PropertyInfo? property = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredProperty(currentType, propertyName);
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
            MethodInfo? method = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethod(currentType, methodName);
            if (method is not null)
            {
                return method;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }
}
