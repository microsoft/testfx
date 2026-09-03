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
                builder.Append('"').Append(value).Append('"');
                break;

            case char value:
                builder.Append('\'').Append(value).Append('\'');
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
