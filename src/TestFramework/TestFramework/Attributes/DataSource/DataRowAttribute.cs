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
    /// <param name="arg1"> The first argument. </param>
    // Need to have this constructor explicitly to fix a CLS compliance error.
    public DataRowAttribute(object? arg1)
        => Data = new object?[] { arg1 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    public DataRowAttribute(object? arg1, object? arg2)
        => Data = new object?[] { arg1, arg2 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3)
        => Data = new object?[] { arg1, arg2, arg3 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4)
        => Data = new object?[] { arg1, arg2, arg3, arg4 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5, arg6 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10)
        => Data = new object?[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10 };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    /// <param name="arg12"> The twelfth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11, object? arg12)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    /// <param name="arg12"> The twelfth argument. </param>
    /// <param name="arg13"> The thirteen argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11, object? arg12, object? arg13)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11,
            arg12, arg13,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    /// <param name="arg12"> The twelfth argument. </param>
    /// <param name="arg13"> The thirteen argument. </param>
    /// <param name="arg14"> The fourteenth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11, object? arg12, object? arg13,
        object? arg14)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11,
            arg12, arg13, arg14,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    /// <param name="arg12"> The twelfth argument. </param>
    /// <param name="arg13"> The thirteen argument. </param>
    /// <param name="arg14"> The fourteenth argument. </param>
    /// <param name="arg15"> The fifteenth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11, object? arg12, object? arg13,
        object? arg14, object? arg15)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11,
            arg12, arg13, arg14, arg15,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
    /// </summary>
    /// <param name="arg1"> The first argument. </param>
    /// <param name="arg2"> The second argument. </param>
    /// <param name="arg3"> The third argument. </param>
    /// <param name="arg4"> The fourth argument. </param>
    /// <param name="arg5"> The fifth argument. </param>
    /// <param name="arg6"> The sixth argument. </param>
    /// <param name="arg7"> The seventh argument. </param>
    /// <param name="arg8"> The eight argument. </param>
    /// <param name="arg9"> The nineth argument. </param>
    /// <param name="arg10"> The tenth argument. </param>
    /// <param name="arg11"> The eleventh argument. </param>
    /// <param name="arg12"> The twelfth argument. </param>
    /// <param name="arg13"> The thirteen argument. </param>
    /// <param name="arg14"> The fourteenth argument. </param>
    /// <param name="arg15"> The fifteenth argument. </param>
    /// <param name="arg16"> The sixteenth argument. </param>
    public DataRowAttribute(object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6,
        object? arg7, object? arg8, object? arg9, object? arg10, object? arg11, object? arg12, object? arg13,
        object? arg14, object? arg15, object? arg16)
        => Data = new object?[]
        {
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11,
            arg12, arg13, arg14, arg15, arg16,
        };

    /// <summary>
    /// Gets data for calling test method.
    /// </summary>
    public object?[] Data { get; }

    /// <summary>
    /// Gets or sets display name in test results for customization.
    /// </summary>
    public string? DisplayName { get; set; }

    public string? DisplayNameMethod { get; set; }

    public Type? DisplayNameMethodDeclaringType { get; set; }

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

        if (DisplayNameMethod != null)
        {
            var displayNameMethodDeclaringType = DisplayNameMethodDeclaringType ?? methodInfo.DeclaringType;
            DebugEx.Assert(displayNameMethodDeclaringType is not null, "Declaring type of test data cannot be null.");

            var method = displayNameMethodDeclaringType.GetTypeInfo().GetDeclaredMethod(DisplayNameMethod);
            if (method == null)
            {
                throw new ArgumentNullException(string.Format("{0}", DisplayNameMethod));
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(MethodInfo) ||
                parameters[1].ParameterType != typeof(object[]) ||
                method.ReturnType != typeof(string) ||
                !method.IsStatic ||
                !method.IsPublic)
            {
                throw new ArgumentNullException(
                    string.Format(
                        FrameworkMessages.DataRowDisplayNameMethod,
                        DisplayNameMethod,
                        typeof(string).Name,
                        string.Join(", ", typeof(MethodInfo).Name, typeof(object[]).Name)));
            }

            return method.Invoke(null, new object?[] { methodInfo, data }) as string;
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
