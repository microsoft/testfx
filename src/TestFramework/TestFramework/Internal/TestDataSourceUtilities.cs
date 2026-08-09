// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class TestDataSourceUtilities
{
    public static string? ComputeDefaultDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        if (data is null)
        {
            return null;
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();

        // We want to force treating `data` as a single array element to ensure that objects are casted to strings
        // (using ToString()) so that null do appear as "null". If you remove this special-casing, and do
        // string.Join(",", new object[] { null, "a" }), you will get empty string while with the special-casing
        // you will get "null,a".
        bool wrapAsSingleElement = parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]);

        string methodDisplayName = methodInfo is ReflectionTestMethodInfo reflectionTestMethodInfo
            ? reflectionTestMethodInfo.DisplayName
            : methodInfo.Name;

        var argumentsBuilder = new StringBuilder();
        if (wrapAsSingleElement)
        {
            AppendHumanizedArguments(argumentsBuilder, data);
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    argumentsBuilder.Append(',');
                }

                AppendHumanizedArguments(argumentsBuilder, data[i]);
            }
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.DataDrivenResultDisplayName,
            methodDisplayName,
            argumentsBuilder.ToString());
    }

    /// <summary>
    /// Recursively resolve collections of objects to a proper string representation.
    /// </summary>
    /// <param name="builder">The builder to append the humanized representation to.</param>
    /// <param name="data">The method arguments.</param>
    private static void AppendHumanizedArguments(StringBuilder builder, object? data)
    {
        if (data is null)
        {
            builder.Append("null");
            return;
        }

        if (data is string s)
        {
            builder.Append('"').Append(s).Append('"');
            return;
        }

        if (data is char c)
        {
            builder.Append('\'').Append(c).Append('\'');
            return;
        }

        if (data is not IEnumerable enumerable || data is not System.Array)
        {
            builder.Append(data.ToString());
            return;
        }

        builder.Append('[');
        bool first = true;
        foreach (object? element in enumerable)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            AppendHumanizedArguments(builder, element);
        }

        builder.Append(']');
    }
}
