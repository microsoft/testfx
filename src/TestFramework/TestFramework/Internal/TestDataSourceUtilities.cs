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
                AppendEscapedString(builder, value);
                break;

            case char value:
                AppendEscapedChar(builder, value);
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
<<<<<<< HEAD
=======

    private static void AppendEscapedString(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                default:
                    AppendEscapedCharacter(builder, character, IsUnpairedSurrogate(value, i));
                    break;
            }
        }

        builder.Append('"');
    }

    private static void AppendEscapedChar(StringBuilder builder, char value)
    {
        builder.Append('\'');
        if (value == '\'')
        {
            builder.Append("\\'");
        }
        else if (value == '\\')
        {
            builder.Append("\\\\");
        }
        else
        {
            AppendEscapedCharacter(builder, value, char.IsSurrogate(value));
        }

        builder.Append('\'');
    }

    private static void AppendEscapedCharacter(StringBuilder builder, char character, bool forceUnicodeEscape)
    {
        switch (character)
        {
            case '\0':
                builder.Append("\\0");
                break;
            case '\a':
                builder.Append("\\a");
                break;
            case '\b':
                builder.Append("\\b");
                break;
            case '\f':
                builder.Append("\\f");
                break;
            case '\n':
                builder.Append("\\n");
                break;
            case '\r':
                builder.Append("\\r");
                break;
            case '\t':
                builder.Append("\\t");
                break;
            case '\v':
                builder.Append("\\v");
                break;
            default:
                if (forceUnicodeEscape || char.IsControl(character) || character is '\u2028' or '\u2029')
                {
                    builder.Append("\\u");
                    builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(character);
                }

                break;
        }
    }

    private static bool IsUnpairedSurrogate(string value, int index)
    {
        char character = value[index];
        return char.IsHighSurrogate(character)
            ? index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])
            : char.IsLowSurrogate(character)
                && (index == 0 || !char.IsHighSurrogate(value[index - 1]));
    }

    private sealed class MethodData
    {
        public MethodData(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            HasSingleObjectArrayParameter = parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]);
        }

        public bool HasSingleObjectArrayParameter { get; }
    }
>>>>>>> Escape control characters in data row display names
}
