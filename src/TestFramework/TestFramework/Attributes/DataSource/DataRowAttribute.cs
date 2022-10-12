// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    /// <param name="data"> The data object. </param>
    public DataRowAttribute(object data)
    {
        // Need to have this constructor explicitly to fix a CLS compliance error.
        Data = new object[] { data };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="data"> More data. </param>
    public DataRowAttribute(params object[] data)
    {
        // This actually means that the user wants to pass in a 'null' value to the test method.
        Data = data ?? new object[] { null };
    }

    /// <summary>
    /// Gets data for calling test method.
    /// </summary>
    public object[] Data { get; }

    /// <summary>
    /// Gets or sets display name in test results for customization.
    /// </summary>
    public string DisplayName { get; set; }

    /// <inheritdoc />
    public IEnumerable<object[]> GetData(MethodInfo methodInfo)
    {
        return new[] { Data };
    }

    /// <inheritdoc />
    public string GetDisplayName(MethodInfo methodInfo, object[] data)
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return DisplayName;
        }

        if (data != null)
        {
            return string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, methodInfo.Name, string.Join(",", data));
        }

        return null;
    }
}
