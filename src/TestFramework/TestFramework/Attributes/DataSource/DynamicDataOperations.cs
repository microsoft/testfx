// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
#if NET471_OR_GREATER || NETCOREAPP
using System.Runtime.CompilerServices;
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

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
            case DynamicDataSourceType.Property:
                PropertyInfo property = _dynamicDataDeclaringType.GetProperty(_dynamicDataSourceName, DeclaredOnlyLookup)
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
                MethodInfo method = _dynamicDataDeclaringType.GetMethod(_dynamicDataSourceName, DeclaredOnlyLookup)
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

    public static string? GetDisplayName(string? DynamicDataDisplayName, Type? DynamicDataDisplayNameDeclaringType, MethodInfo methodInfo, object?[]? data)
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
<<<<<<< HEAD
                if (!IsTupleOrValueTuple(entry, out int tupleSize)
                    || (objects.Count > 0 && objects[objects.Count - 1].Length != tupleSize))
                {
                    data = null;
                    return false;
                }

                object[] array = new object[tupleSize];
                ProcessTuple(entry, array, 0);

                objects.Add(array);

                static void ProcessTuple(object data, object[] array, int startingIndex)
=======
        if (IsTupleOrValueTuple(data, out int tupleSize)
            && (objects.Count == 0 || objects[objects.Count - 1].Length == tupleSize))
        {
            object[] array = new object[tupleSize];
            ProcessTuple(data, array, 0);

            objects.Add(array);
            return true;
        }

        static object GetFieldOrProperty(Type type, object data, string fieldOrPropertyName)
            // ValueTuple is a value type, and uses fields for Items.
            // Tuple is a reference type, and uses properties for Items.
            => type.IsValueType
                ? type.GetField(fieldOrPropertyName).GetValue(data)
                : type.GetProperty(fieldOrPropertyName).GetValue(data);

        static void ProcessTuple(object data, object[] array, int startingIndex)
        {
            Type type = data.GetType();
            int tupleSize = type.GenericTypeArguments.Length;
            for (int i = 0; i < tupleSize; i++)
            {
                if (i != 7)
                {
                    // Note: ItemN are properties on Tuple, but are fields on ValueTuple
                    array[startingIndex + i] = GetFieldOrProperty(type, data, $"Item{i + 1}");
                    continue;
                }

                object rest = GetFieldOrProperty(type, data, "Rest");
                if (IsTupleOrValueTuple(rest, out _))
                {
                    ProcessTuple(rest, array, startingIndex + 7);
                }
                else
>>>>>>> Fix tuple regression before it's shipped :)
                {
                    Type type = data.GetType();
                    int tupleSize = type.GenericTypeArguments.Length;
                    for (int i = 0; i < tupleSize; i++)
                    {
                        if (i != 7)
                        {
                            // Note: ItemN are properties on Tuple, but are fields on ValueTuple
                            array[startingIndex + i] = type.GetField($"Item{i + 1}")?.GetValue(data)
                                ?? type.GetProperty($"Item{i + 1}").GetValue(data);
                            continue;
                        }

                        object rest = type.GetProperty("Rest")?.GetValue(data) ??
                            type.GetField("Rest").GetValue(data)!;
                        if (IsTupleOrValueTuple(rest, out _))
                        {
                            ProcessTuple(rest, array, startingIndex + 7);
                        }
                        else
                        {
                            array[startingIndex + i] = rest;
                        }

                        return;
                    }
                }
#endif
            }

            data = objects;
            return true;
        }

        data = null;
        return false;
    }

#if !NET471_OR_GREATER && !NETCOREAPP
    private static bool IsTupleOrValueTuple(object? data, out int tupleSize)
    {
        tupleSize = 0;

        if (data is null)
        {
            return false;
        }

        Type type = data.GetType();
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
            genericTypeDefinition == typeof(Tuple<,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        if (genericTypeDefinition == typeof(Tuple<,,,,,,,>))
        {
            object? last = type.GetProperty("Rest").GetValue(data);
            if (IsTupleOrValueTuple(last, out int restSize))
            {
                tupleSize = 7 + restSize;
                return true;
            }
            else
            {
                tupleSize = 8;
                return true;
            }
        }

#if NET462
        if (genericTypeDefinition.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            tupleSize = type.GetGenericArguments().Length;
            if (tupleSize == 8)
            {
                object? last = type.GetField("Rest").GetValue(data);
                if (IsTupleOrValueTuple(last, out int restSize))
                {
                    tupleSize = 7 + restSize;
                }
            }

            return true;
        }
#else
        if (genericTypeDefinition == typeof(ValueTuple<>) ||
            genericTypeDefinition == typeof(ValueTuple<,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,>) ||
            genericTypeDefinition == typeof(ValueTuple<,,,,,,>))
        {
            tupleSize = type.GetGenericArguments().Length;
            return true;
        }

        if (genericTypeDefinition == typeof(ValueTuple<,,,,,,,>))
        {
            object? last = type.GetField("Rest").GetValue(data);
            if (IsTupleOrValueTuple(last, out int restSize))
            {
                tupleSize = 7 + restSize;
                return true;
            }
            else
            {
                tupleSize = 8;
                return true;
            }
        }
#endif

        return false;
    }
#endif
}
