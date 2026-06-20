// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS1591
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

internal static class AssertInterpolatedStringHandlerAppender
{
    public static void AppendLiteral(StringBuilder builder, string? value) => builder.Append(value);

#if NETCOREAPP3_1_OR_GREATER
    public static void AppendFormatted(StringBuilder builder, ReadOnlySpan<char> value) => builder.Append(value);
#endif

    public static void AppendFormatted<T>(StringBuilder builder, T value, string? format) => builder.AppendFormat(null, $"{{0:{format}}}", value);

    public static void AppendFormatted<T>(StringBuilder builder, T value, int alignment) => builder.AppendFormat(null, $"{{0,{alignment}}}", value);

    public static void AppendFormatted<T>(StringBuilder builder, T value, int alignment, string? format) => builder.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

    public static void AppendFormatted(StringBuilder builder, string? value) => builder.Append(value);

    public static void AppendFormatted(StringBuilder builder, string? value, int alignment = 0, string? format = null) => builder.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

    public static void AppendFormatted(StringBuilder builder, object? value, int alignment = 0, string? format = null) => builder.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
}

public sealed partial class Assert
{
    public readonly partial struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        public void AppendLiteral(string? value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertAreSameInterpolatedStringHandler<TArgument>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertAreNotSameInterpolatedStringHandler<TArgument>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertSingleInterpolatedStringHandler<TItem>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertCountInterpolatedStringHandler<TItem>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsNotEmptyInterpolatedStringHandler<TItem>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsExactInstanceOfTypeInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsNotExactInstanceOfTypeInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsInstanceOfTypeInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertGenericIsInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsNotInstanceOfTypeInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsNullInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsNotNullInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsTrueInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertIsFalseInterpolatedStringHandler
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertNonStrictThrowsInterpolatedStringHandler<TException>
        where TException : Exception
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }

    public readonly partial struct AssertThrowsExactlyInterpolatedStringHandler<TException>
        where TException : Exception
    {
        public void AppendLiteral(string value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

        public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
    }
}
