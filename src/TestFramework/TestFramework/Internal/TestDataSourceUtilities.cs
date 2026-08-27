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
        string methodDisplayName = methodInfo is ReflectionTestMethodInfo reflectionTestMethodInfo
            ? reflectionTestMethodInfo.DisplayName
            : methodInfo.Name;
        CultureInfo currentCulture = CultureInfo.CurrentCulture;
        string displayNameFormat = FrameworkMessages.DataDrivenResultDisplayName;

        var argumentsBuilder = new StringBuilder();
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]))
        {
            AppendHumanizedArgument(argumentsBuilder, data);
        }
        else
        {
            AppendHumanizedArguments(argumentsBuilder, data);
        }

        return string.Format(
            currentCulture,
            displayNameFormat,
            methodDisplayName,
            argumentsBuilder.ToString());
    }

    /// <summary>
    /// Appends a collection of objects using their display-name representation.
    /// </summary>
    private static void AppendHumanizedArguments(StringBuilder builder, IEnumerable data)
    {
        bool appendSeparator = false;
        foreach (object? item in data)
        {
            if (appendSeparator)
            {
                builder.Append(',');
            }

            AppendHumanizedArgument(builder, item);
            appendSeparator = true;
        }
    }

    /// <summary>
    /// Recursively appends collections of objects using their display-name representation.
    /// </summary>
    private static void AppendHumanizedArgument(StringBuilder builder, object? data)
    {
        switch (data)
        {
            case null:
                builder.Append("null");
                break;

            case string value:
                builder.Append('"').Append(value).Append('"');
                break;

            case char value:
                builder.Append('\'').Append(value).Append('\'');
                break;

            case Array:
                builder.Append('[');
                AppendHumanizedArguments(builder, (IEnumerable)data);
                builder.Append(']');
                break;

            default:
                builder.Append(data.ToString());
                break;
        }
    }
}
