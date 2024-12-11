// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute to define in-line data for a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataRowAttribute : Attribute, ITestDataSource, ITestDataSourceUnfoldingCapability
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class.
    /// </summary>
    public DataRowAttribute()
        : this(Array.Empty<object>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class with an array of object arguments.
    /// </summary>
    /// <param name="data"> The data. </param>
    /// <remarks>This constructor is only kept for CLS compliant tests.</remarks>
    public DataRowAttribute(object? data) => Data = data is not null ? [data] : [null];

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class with an array of string arguments.
    /// </summary>
    /// <param name="stringArrayData"> The string array data. </param>
    public DataRowAttribute(string?[]? stringArrayData)
        : this([stringArrayData])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class with an array of object arguments.
    /// </summary>
    /// <param name="data"> The data. </param>
    public DataRowAttribute(params object?[]? data) => Data = data ?? [null];

    protected internal static TestIdGenerationStrategy TestIdGenerationStrategy { get; internal set; }

    /// <summary>
    /// Gets data for calling test method.
    /// </summary>
    public object?[] Data { get; }

    /// <summary>
    /// Gets or sets display name in test results for customization.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <inheritdoc />
    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; set; } = TestDataSourceUnfoldingStrategy.Auto;

    /// <inheritdoc />
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo) => [Data];

    /// <inheritdoc />
    public virtual string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName
            : TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data, TestIdGenerationStrategy);
}
