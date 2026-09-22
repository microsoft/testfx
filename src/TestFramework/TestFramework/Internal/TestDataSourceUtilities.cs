// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class TestDataSourceUtilities
{
    private const int MaxCachedBuilderCapacity = 360;

#pragma warning disable IDE0028 // ConditionalWeakTable is not collection-expression-constructible on .NET Framework (CS9174).
    private static readonly ConditionalWeakTable<MethodInfo, MethodData> MethodDataCache = new();
#pragma warning restore IDE0028

    [ThreadStatic]
    private static StringBuilder? s_cachedBuilder;

    [ThreadStatic]
    private static CultureInfo? s_cachedResourceCulture;

    [ThreadStatic]
    private static string? s_cachedDisplayNameFormat;

    public static string? ComputeDefaultDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        if (data is null)
        {
            return null;
        }

        MethodData methodData = MethodDataCache.GetValue(methodInfo, static method => new(method));
        string methodDisplayName = methodInfo is ReflectionTestMethodInfo reflectionTestMethodInfo
            ? reflectionTestMethodInfo.DisplayName
            : methodInfo.Name;
        CultureInfo currentCulture = CultureInfo.CurrentCulture;
        string displayNameFormat = GetDisplayNameFormat();

        StringBuilder argumentsBuilder = AcquireBuilder();
        if (methodData.HasSingleObjectArrayParameter)
        {
            AppendHumanizedArgument(argumentsBuilder, data);
        }
        else
        {
            AppendHumanizedArguments(argumentsBuilder, data);
        }

        string arguments = GetStringAndReleaseBuilder(argumentsBuilder);
        return string.Format(
            currentCulture,
            displayNameFormat,
            methodDisplayName,
            arguments);
    }

    private static string GetDisplayNameFormat()
    {
        CultureInfo resourceCulture = FrameworkMessages.Culture ?? CultureInfo.CurrentUICulture;
        if (!resourceCulture.Equals(s_cachedResourceCulture))
        {
            s_cachedResourceCulture = resourceCulture;
            s_cachedDisplayNameFormat = FrameworkMessages.DataDrivenResultDisplayName;
        }

        return s_cachedDisplayNameFormat!;
    }

    private static StringBuilder AcquireBuilder()
    {
        StringBuilder? builder = s_cachedBuilder;
        if (builder is null)
        {
            return new StringBuilder();
        }

        s_cachedBuilder = null;
        builder.Clear();
        return builder;
    }

    private static string GetStringAndReleaseBuilder(StringBuilder builder)
    {
        string result = builder.ToString();
        if (builder.Capacity <= MaxCachedBuilderCapacity)
        {
            s_cachedBuilder = builder;
        }

        return result;
    }

    /// <summary>
    /// Appends a collection of objects using their display-name representation.
    /// </summary>
    private static void AppendHumanizedArguments(StringBuilder builder, object?[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendHumanizedArgument(builder, data[i]);
        }
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

            case object?[] values:
                builder.Append('[');
                AppendHumanizedArguments(builder, values);
                builder.Append(']');
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
}
