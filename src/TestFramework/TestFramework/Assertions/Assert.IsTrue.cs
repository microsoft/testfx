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
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertIsTrueInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsTrueInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
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
                _builder.Insert(0, $"'condition' expression: {conditionExpression}. ");
                ThrowAssertIsTrueFailed(_builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsFalseInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsFalseInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
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
                _builder.Insert(0, $"'condition' expression: {conditionExpression}. ");
                ThrowAssertIsFalseFailed(_builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <inheritdoc cref="IsTrue(bool?, string, string)"/>
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsTrueInterpolatedStringHandler message, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(conditionExpression);

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
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string message = "", [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
    {
        if (IsTrueFailing(condition))
        {
            ThrowAssertIsTrueFailed(BuildUserMessageForConditionExpression(message, conditionExpression));
        }
    }

    private static bool IsTrueFailing(bool? condition)
        => condition is false or null;

    private static void ThrowAssertIsTrueFailed(string message)
        => ThrowAssertFailed("Assert.IsTrue", message);

    /// <inheritdoc cref="IsFalse(bool?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsFalseInterpolatedStringHandler message, [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(conditionExpression);

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
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string message = "", [CallerArgumentExpression(nameof(condition))] string conditionExpression = "")
    {
        if (IsFalseFailing(condition))
        {
            ThrowAssertIsFalseFailed(BuildUserMessageForConditionExpression(message, conditionExpression));
        }
    }

    private static bool IsFalseFailing(bool? condition)
        => condition is true or null;

    [DoesNotReturn]
    private static void ThrowAssertIsFalseFailed(string userMessage)
        => ThrowAssertFailed("Assert.IsFalse", userMessage);
}
