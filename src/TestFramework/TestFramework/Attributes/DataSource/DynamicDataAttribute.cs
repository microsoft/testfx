// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET471_OR_GREATER || NETCOREAPP
using System.Collections;
using System.Runtime.CompilerServices;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Enum to specify whether the data is stored as property or in method.
/// </summary>
public enum DynamicDataSourceType
{
    /// <summary>
    /// Data is declared as property.
    /// </summary>
    Property = 0,

    /// <summary>
    /// Data is declared in method.
    /// </summary>
    Method = 1,
}

/// <summary>
/// Attribute to define dynamic data for a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DynamicDataAttribute : Attribute, ITestDataSource
{
    private readonly string _dynamicDataSourceName;
    private readonly DynamicDataSourceType _dynamicDataSourceType;

    private Type? _dynamicDataDeclaringType;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class.
    /// </summary>
    /// <param name="dynamicDataSourceName">
    /// The name of method or property having test data.
    /// </param>
    /// <param name="dynamicDataSourceType">
    /// Specifies whether the data is stored as property or in method.
    /// </param>
    public DynamicDataAttribute(string dynamicDataSourceName, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
    {
        _dynamicDataSourceName = dynamicDataSourceName;
        _dynamicDataSourceType = dynamicDataSourceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class when the test data is present in a class different
    /// from test method's class.
    /// </summary>
    /// <param name="dynamicDataSourceName">
    /// The name of method or property having test data.
    /// </param>
    /// <param name="dynamicDataDeclaringType">
    /// The declaring type of property or method having data. Useful in cases when declaring type is present in a class different from
    /// test method's class. If null, declaring type defaults to test method's class type.
    /// </param>
    /// <param name="dynamicDataSourceType">
    /// Specifies whether the data is stored as property or in method.
    /// </param>
    public DynamicDataAttribute(string dynamicDataSourceName, Type dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
        : this(dynamicDataSourceName, dynamicDataSourceType)
    {
        _dynamicDataDeclaringType = dynamicDataDeclaringType;
    }

    /// <summary>
    /// Gets or sets the name of method used to customize the display name in test results.
    /// </summary>
    public string? DynamicDataDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the declaring type used to customize the display name in test results.
    /// </summary>
    public Type? DynamicDataDisplayNameDeclaringType { get; set; }

    /// <inheritdoc />
    public IEnumerable<object[]> GetData(MethodInfo methodInfo)
    {
        // Check if the declaring type of test data is passed in constructor. If not, default to test method's class type.
        _dynamicDataDeclaringType ??= methodInfo.DeclaringType;
        DebugEx.Assert(_dynamicDataDeclaringType is not null, "Declaring type of test data cannot be null.");

        object? obj = null;

        switch (_dynamicDataSourceType)
        {
            case DynamicDataSourceType.Property:
                PropertyInfo property = _dynamicDataDeclaringType.GetTypeInfo().GetDeclaredProperty(_dynamicDataSourceName)
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
                MethodInfo method = _dynamicDataDeclaringType.GetTypeInfo().GetDeclaredMethod(_dynamicDataSourceName)
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
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        if (DynamicDataDisplayName != null)
        {
            Type? dynamicDisplayNameDeclaringType = DynamicDataDisplayNameDeclaringType ?? methodInfo.DeclaringType;
            DebugEx.Assert(dynamicDisplayNameDeclaringType is not null, "Declaring type of test data cannot be null.");

            MethodInfo method = dynamicDisplayNameDeclaringType.GetTypeInfo().GetDeclaredMethod(DynamicDataDisplayName)
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
