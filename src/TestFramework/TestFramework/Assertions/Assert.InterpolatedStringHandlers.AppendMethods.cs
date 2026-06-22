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

// The per-handler AppendLiteral/AppendFormatted overloads that the C# interpolated string handler pattern
// requires on each Assert.*InterpolatedStringHandler struct are emitted by the source generator in
// TestFramework.SourceGeneration (see GenerateAssertInterpolatedStringAppendMethodsAttribute). They all
// forward to the shared AssertInterpolatedStringHandlerAppender above.
