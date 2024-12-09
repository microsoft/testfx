// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP || NET471_OR_GREATER
using System.Collections;
using System.Runtime.CompilerServices;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

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
public sealed class DynamicDataAttribute : Attribute, ITestDataSource, ITestDataSourceEmptyDataSourceExceptionInfo, ITestDataSourceUnfoldingCapability
{
    private readonly string _dynamicDataSourceName;
    private readonly DynamicDataSourceType _dynamicDataSourceType;

    private readonly Type? _dynamicDataDeclaringType;

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
        : this(dynamicDataSourceName, dynamicDataSourceType) => _dynamicDataDeclaringType = dynamicDataDeclaringType;

    internal static TestIdGenerationStrategy TestIdGenerationStrategy { get; set; }

    /// <summary>
    /// Gets or sets the name of method used to customize the display name in test results.
    /// </summary>
    public string? DynamicDataDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the declaring type used to customize the display name in test results.
    /// </summary>
    public Type? DynamicDataDisplayNameDeclaringType { get; set; }

    /// <inheritdoc />
    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; set; } = TestDataSourceUnfoldingStrategy.Auto;

    /// <inheritdoc />
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) => DynamicDataProvider.Instance.GetData(_dynamicDataDeclaringType, _dynamicDataSourceType, _dynamicDataSourceName, methodInfo);

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

        return TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data, TestIdGenerationStrategy);
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

    string? ITestDataSourceEmptyDataSourceExceptionInfo.GetPropertyOrMethodNameForEmptyDataSourceException()
        => _dynamicDataSourceName;

    string? ITestDataSourceEmptyDataSourceExceptionInfo.GetPropertyOrMethodContainerTypeNameForEmptyDataSourceException(MethodInfo testMethodInfo)
    {
        Type? type = _dynamicDataDeclaringType ?? testMethodInfo.DeclaringType;
        DebugEx.Assert(type is not null, "Declaring type of test data cannot be null.");
        return type.FullName;
    }
}
