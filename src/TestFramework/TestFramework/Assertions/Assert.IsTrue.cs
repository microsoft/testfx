// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
    // Keep the compiler-facing handler overloads in sync with the related handlers in Assert.IsNull.cs.

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsTrue</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsTrueInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly bool? _condition;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsTrueInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="condition">The condition value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsTrueInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
            _condition = condition;
            shouldAppend = IsTrueFailing(condition);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string conditionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsTrueFailed(_condition, _builder.ToString(), conditionExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsFalse</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsFalseInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly bool? _condition;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsFalseInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="condition">The condition value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsFalseInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
            _condition = condition;
            shouldAppend = IsFalseFailing(condition);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string conditionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsFalseFailed(_condition, _builder.ToString(), conditionExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
    }

    /// <inheritdoc cref="IsTrue(bool?, string, string)"/>
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsTrueInterpolatedStringHandler message, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsTrue");
        message.ComputeAssertion(conditionExpression);
    }

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is false. The message is shown in test results.
    /// </param>
    /// <param name="conditionExpression">
    /// The syntactic expression of condition as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string? message = "", [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsTrue");

        if (IsTrueFailing(condition))
        {
            ReportAssertIsTrueFailed(condition, message, conditionExpression);
        }
    }

    private static bool IsTrueFailing(bool? condition)
        => condition is false or null;

    [DoesNotReturn]
    private static void ReportAssertIsTrueFailed(bool? condition, string? message, string conditionExpression)
    {
        string actualValue = AssertionValueRenderer.RenderValue(condition);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("actual:", actualValue);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsTrueFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(AssertionValueRenderer.RenderValue(true), actualValue);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsTrue", conditionExpression, nameof(condition)));

        ReportAssertFailed(structured);
    }

    /// <inheritdoc cref="IsFalse(bool?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsFalseInterpolatedStringHandler message, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsFalse");
        message.ComputeAssertion(conditionExpression);
    }

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is true. The message is shown in test results.
    /// </param>
    /// <param name="conditionExpression">
    /// The syntactic expression of condition as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string? message = "", [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsFalse");

        if (IsFalseFailing(condition))
        {
            ReportAssertIsFalseFailed(condition, message, conditionExpression);
        }
    }

    private static bool IsFalseFailing(bool? condition)
        => condition is true or null;

    [DoesNotReturn]
    private static void ReportAssertIsFalseFailed(bool? condition, string? message, string conditionExpression)
    {
        string actualValue = AssertionValueRenderer.RenderValue(condition);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("actual:", actualValue);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsFalseFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(AssertionValueRenderer.RenderValue(false), actualValue);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsFalse", conditionExpression, nameof(condition)));

        ReportAssertFailed(structured);
    }
}
