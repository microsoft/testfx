// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertAreSameInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly TArgument? _expected;
        private readonly TArgument? _actual;

        public AssertAreSameInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, out bool shouldAppend)
        {
            _expected = expected;
            _actual = actual;
            shouldAppend = IsAreSameFailing(expected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertAreSameFailed(_expected, _actual, _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertAreNotSameInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;

        public AssertAreNotSameInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
        {
            shouldAppend = IsAreNotSameFailing(notExpected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertAreNotSameFailed(_builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Tests whether the specified objects both refer to the same object and
    /// throws an exception if the two inputs do not refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first object to compare. This is the value the test expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual)
        => AreSame(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the specified objects both refer to the same object and
    /// throws an exception if the two inputs do not refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first object to compare. This is the value the test expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not the same as <paramref name="expected"/>. The message is shown
    /// in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual, string? message)
        => AreSame(expected, actual, message, null);

    /// <inheritdoc cref="AreSame{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreSame<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreSameInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified objects both refer to the same object and
    /// throws an exception if the two inputs do not refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first object to compare. This is the value the test expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not the same as <paramref name="expected"/>. The message is shown
    /// in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!IsAreSameFailing(expected, actual))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreSameFailed(expected, actual, userMessage);
    }

    private static bool IsAreSameFailing<T>(T? expected, T? actual)
        => !object.ReferenceEquals(expected, actual);

    [DoesNotReturn]
    private static void ThrowAssertAreSameFailed<T>(T? expected, T? actual, string userMessage)
    {
        string finalMessage = userMessage;
        if (expected is ValueType && actual is ValueType)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreSameGivenValues,
                userMessage);
        }

        ThrowAssertFailed("Assert.AreSame", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified objects refer to different objects and
    /// throws an exception if the two inputs refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual)
        => AreNotSame(notExpected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the specified objects refer to different objects and
    /// throws an exception if the two inputs refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is the same as <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual, string? message)
        => AreNotSame(notExpected, actual, message, null);

    /// <inheritdoc cref="AreNotSame{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotSame<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotSameInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified objects refer to different objects and
    /// throws an exception if the two inputs refer to the same object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is the same as <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (IsAreNotSameFailing(notExpected, actual))
        {
            ThrowAssertAreNotSameFailed(BuildUserMessage(message, parameters));
        }
    }

    private static bool IsAreNotSameFailing<T>(T? notExpected, T? actual)
        => object.ReferenceEquals(notExpected, actual);

    [DoesNotReturn]
    private static void ThrowAssertAreNotSameFailed(string userMessage)
        => ThrowAssertFailed("Assert.AreNotSame", userMessage);
}
