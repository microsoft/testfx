// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute to define in-line data for a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataRowAttribute : Attribute, ITestDataSource
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
    public DataRowAttribute(object? data)
    {
        Data = data is not null ? [data] : [null];
    }

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
    public DataRowAttribute(params object?[]? data)
    {
        Data = data ?? [null];
    }

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
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo) => new[] { Data };

    /// <inheritdoc />
    public virtual string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return DisplayName;
        }

        if (data == null)
        {
            return null;
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();

        // We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
        // so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
        // you will get empty string while with the call you will get "null,a".
        IEnumerable<object?> displayData = parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])
            ? new object[] { data.AsEnumerable() }
            : data.AsEnumerable();

        return string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, methodInfo.Name,
            string.Join(",", displayData.Select(GetObjectString)));
    }

    /// <summary>
    /// Recursively resolve collections of objects to a proper string representation.
    /// </summary>
    private static string? GetObjectString(object? obj)
    {
        if (obj == null)
        {
            return null;
        }

        if (TestIdGenerationStrategy != TestIdGenerationStrategy.FullyQualified
            || !obj.GetType().IsArray)
        {
            return obj.ToString();
        }

        // We need to box the object here so that we can support value types
        IEnumerable<object> boxedObjectEnumerable = ((IEnumerable)obj).Cast<object>();
        IEnumerable<string?> elementStrings = boxedObjectEnumerable.Select(GetObjectString);
        return $"[{string.Join(",", elementStrings)}]";
    }
}
