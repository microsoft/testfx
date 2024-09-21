// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

#if NET471_OR_GREATER || NETCOREAPP
using System.Collections;
using System.Runtime.CompilerServices;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

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

        if (!data.Any())
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataIEnumerableEmpty,
                    _dynamicDataSourceName,
                    _dynamicDataDeclaringType.FullName));
        }

        // Data is valid, return it.
        return data;
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

#if NETCOREAPP || NET471_OR_GREATER
        if (dataSource is IEnumerable enumerable)
        {
            List<object[]> objects = new();
            foreach (object? entry in enumerable)
            {
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
            }

            data = objects;
            return true;
        }
#endif

        data = null;
        return false;
    }
}
