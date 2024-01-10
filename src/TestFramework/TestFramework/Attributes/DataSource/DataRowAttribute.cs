// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        : this(new object?[] { stringArrayData })
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

    /// <summary>
    /// Gets data for calling test method.
    /// </summary>
    public object?[] Data { get; }

    /// <summary>
    /// Gets or sets display name in test results for customization.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <inheritdoc />
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
    {
        return new[] { Data };
    }

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

        var parameters = methodInfo.GetParameters();

        // We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
        // so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
        // you will get empty string while with the call you will get "null,a".
        IEnumerable<object?> displayData = parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])
            ? new object[] { data.AsEnumerable() }
            : data.AsEnumerable();

        // On the most basic level, if we only used TestMethodAttribute the problem could be fixed here,
        // but user can inherit from the attribute type (not a big problem we could construct it), or (worse) they can provide
        // an override for every attribute on the class that is determined by GetTestMethodAttribute
        // (as shown in https://github.com/microsoft/testfx/issues/1635#issuecomment-1532714683)
        // This means that we cannot naively pick up DisplayName from the attribute and use it,
        // because we don't hold that final instance.
        //
        // This whole replacement is implemented on the Adapter level (incorrectly?), and so we don't have access to the
        // TestMethodInfo type, which holds all of the information of how the adapter resolved the type. It probably is also not
        // our job to do some difficult logic here.
        //
        // So maybe the simplest solution would be to replace the return type with a result type, and return a multi part name,
        // which can describe DisplayNameOverride and parameters. That way the upper logic knows if it should pick up the name,
        // or use the method name, and also has the parameters.
        //
        // Problem with this is that it is not universal, and that this interface is public.
        //
        // We could also hot patch based on something else, like returning just name starting with ( from the data source,
        // but that will cause issues in the future.
        //
        // We could also replace object?[]? data with the same data and custom type (so we can detect it above the interface)
        // and that way communicate the multi part name out. The data are not used for anything else now,
        // so this is probably the safest way to fix this for 3.2.
        string formattedParameters = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, string.Empty,
            string.Join(",", displayData));

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = null;
        }

        data[0] = formattedParameters;

        return "MSTestReservedSeeData";
    }
}
