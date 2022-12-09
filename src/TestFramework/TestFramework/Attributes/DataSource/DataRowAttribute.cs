// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        => Data = Array.Empty<object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class.
    /// </summary>
    /// <param name="data"> The data object. </param>
    // Need to have this constructor explicitly to fix a CLS compliance error.
    public DataRowAttribute(object? data)
        => Data = new object?[] { data };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    public DataRowAttribute(object? data1, object? data2)
        => Data = new object?[] { data1, data2 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3)
        => Data = new object?[] { data1, data2, data3 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4)
        => Data = new object?[] { data1, data2, data3, data4 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5)
        => Data = new object?[] { data1, data2, data3, data4, data5 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6)
        => Data = new object?[] { data1, data2, data3, data4, data5, data6 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7)
        => Data = new object?[] { data1, data2, data3, data4, data5, data6, data7 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8)
        => Data = new object?[] { data1, data2, data3, data4, data5, data6, data7, data8 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9)
        => Data = new object?[] { data1, data2, data3, data4, data5, data6, data7, data8, data9 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10)
        => Data = new object?[] { data1, data2, data3, data4, data5, data6, data7, data8, data9, data10 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    /// <param name="data12"> The twelfth data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11, object? data12)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11, data12,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    /// <param name="data12"> The twelfth data object. </param>
    /// <param name="data13"> The thirteen data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11, object? data12, object? data13)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11,
            data12, data13,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    /// <param name="data12"> The twelfth data object. </param>
    /// <param name="data13"> The thirteen data object. </param>
    /// <param name="data14"> The fourteen data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11, object? data12, object? data13,
        object? data14)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11,
            data12, data13, data14,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    /// <param name="data12"> The twelfth data object. </param>
    /// <param name="data13"> The thirteen data object. </param>
    /// <param name="data14"> The fourteen data object. </param>
    /// <param name="data15"> The fifteen data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11, object? data12, object? data13,
        object? data14, object? data15)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11,
            data12, data13, data14, data15,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data1"> The first data object. </param>
    /// <param name="data2"> The second data object. </param>
    /// <param name="data3"> The third data object. </param>
    /// <param name="data4"> The fourth data object. </param>
    /// <param name="data5"> The fifth data object. </param>
    /// <param name="data6"> The sixth data object. </param>
    /// <param name="data7"> The seventh data object. </param>
    /// <param name="data8"> The eight data object. </param>
    /// <param name="data9"> The nineth data object. </param>
    /// <param name="data10"> The tenth data object. </param>
    /// <param name="data11"> The eleventh data object. </param>
    /// <param name="data12"> The twelfth data object. </param>
    /// <param name="data13"> The thirteen data object. </param>
    /// <param name="data14"> The fourteen data object. </param>
    /// <param name="data15"> The fifteen data object. </param>
    /// <param name="data16"> The sixteen data object. </param>
    public DataRowAttribute(object? data1, object? data2, object? data3, object? data4, object? data5, object? data6,
        object? data7, object? data8, object? data9, object? data10, object? data11, object? data12, object? data13,
        object? data14, object? data15, object? data16)
        => Data = new object?[]
        {
            data1, data2, data3, data4, data5, data6, data7, data8, data9, data10, data11,
            data12, data13, data14, data15, data16,
        };

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
}
