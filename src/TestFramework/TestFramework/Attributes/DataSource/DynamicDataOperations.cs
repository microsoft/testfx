// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

<<<<<<< HEAD:src/Adapter/MSTest.TestAdapter/DynamicDataOperations.cs
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
#if NET471_OR_GREATER || NETCOREAPP
using System.Runtime.CompilerServices;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;
=======
namespace Microsoft.VisualStudio.TestTools.UnitTesting;
>>>>>>> Fix DynamicData.GetData regression preventing it to work without adapter:src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataOperations.cs

internal static class DynamicDataOperations
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<object[]> GetData(Type? _dynamicDataDeclaringType, DynamicDataSourceType _dynamicDataSourceType, string _dynamicDataSourceName, MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in. If not, default to test method's class type.
        _dynamicDataDeclaringType ??= methodInfo.DeclaringType;
        DebugEx.Assert(_dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        object? obj = null;

        switch (_dynamicDataSourceType)
        {
<<<<<<< HEAD:src/Adapter/MSTest.TestAdapter/DynamicDataOperations.cs
=======
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
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.DynamicDataSourceShouldExistAndBeValid, _dynamicDataSourceName, _dynamicDataDeclaringType.FullName));
                }
#pragma warning restore IDE0045 // Convert to conditional expression

                break;
>>>>>>> Fix DynamicData.GetData regression preventing it to work without adapter:src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataOperations.cs
            case DynamicDataSourceType.Property:
                PropertyInfo property = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredProperty(_dynamicDataDeclaringType, _dynamicDataSourceName)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Property} {_dynamicDataSourceName}");
                if (property.GetGetMethod(true) is not { IsStatic: true })
                {
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            FrameworkMessages.DynamicDataInvalidPropertyLayout,
                            property.DeclaringType?.FullName is { } typeFullName ? $"{typeFullName}.{property.Name}" : property.Name));
                }

                obj = property.GetValue(null, null);
                break;

            case DynamicDataSourceType.Method:
                MethodInfo method = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethod(_dynamicDataDeclaringType, _dynamicDataSourceName)
                    ?? throw new ArgumentNullException($"{DynamicDataSourceType.Method} {_dynamicDataSourceName}");
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

                obj = method.Invoke(null, null);
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

<<<<<<< HEAD:src/Adapter/MSTest.TestAdapter/DynamicDataOperations.cs
    /// <inheritdoc />
    public string? GetDisplayName(string? DynamicDataDisplayName, Type? DynamicDataDisplayNameDeclaringType, MethodInfo methodInfo, object?[]? data)
=======
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

    public static string? GetDisplayName(string? DynamicDataDisplayName, Type? DynamicDataDisplayNameDeclaringType, MethodInfo methodInfo, object?[]? data)
>>>>>>> Fix DynamicData.GetData regression preventing it to work without adapter:src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataOperations.cs
    {
        if (DynamicDataDisplayName != null)
        {
            Type? dynamicDisplayNameDeclaringType = DynamicDataDisplayNameDeclaringType ?? methodInfo.DeclaringType;
            DebugEx.Assert(dynamicDisplayNameDeclaringType is not null, "Declaring type of test data cannot be null.");

            MethodInfo method = dynamicDisplayNameDeclaringType.GetMethod(DynamicDataDisplayName, DeclaredOnlyLookup)
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

        if (dataSource is IEnumerable enumerable)
        {
            List<object[]> objects = new();
            foreach (object? entry in enumerable)
            {
#if NET471_OR_GREATER || NETCOREAPP
                if (entry is not ITuple tuple
                    || (objects.Count > 0 && objects[^1].Length != tuple.Length))
                {
                    data = null;
                    return false;
                }

                object[] array = new object[tuple.Length];
                for (int i = 0; i < tuple.Length; i++)
                {
                    array[i] = tuple[i]!;
                }

                objects.Add(array);
#else
                Type type = entry.GetType();
                if (!IsTupleOrValueTuple(entry.GetType(), out int tupleSize)
                    || (objects.Count > 0 && objects[objects.Count - 1].Length != tupleSize))
                {
                    data = null;
                    return false;
                }

                object[] array = new object[tupleSize];
                for (int i = 0; i < tupleSize; i++)
                {
                    array[i] = type.GetField($"Item{i + 1}")?.GetValue(entry)!;
                }

                objects.Add(array);
#endif
            }

            data = objects;
            return true;
        }

        data = null;
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

#if NET462
        // TODO: https://github.com/microsoft/testfx/issues/4624
        if (genericTypeDefinition.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }
#else
        // TODO: https://github.com/microsoft/testfx/issues/4624
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
#endif

        return false;
    }
#endif
<<<<<<< HEAD:src/Adapter/MSTest.TestAdapter/DynamicDataOperations.cs
=======

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
>>>>>>> Fix DynamicData.GetData regression preventing it to work without adapter:src/TestFramework/TestFramework/Attributes/DataSource/DynamicDataOperations.cs
}
