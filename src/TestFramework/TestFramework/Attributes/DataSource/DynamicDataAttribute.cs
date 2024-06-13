// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        => DynamicDataProvider.Instance!.GetData(_dynamicDataDeclaringType, _dynamicDataSourceType, _dynamicDataSourceName, methodInfo);

    /// <inheritdoc />
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => DynamicDataProvider.Instance!.GetDisplayName(DynamicDataDisplayName, DynamicDataDisplayNameDeclaringType, methodInfo, data);
}
