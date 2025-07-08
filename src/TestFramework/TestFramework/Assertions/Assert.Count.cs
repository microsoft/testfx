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
    public readonly struct AssertCountInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;
        private readonly int _expectedCount;
        private readonly int _actualCount;

        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, int count, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Count();
            _expectedCount = count;
            shouldAppend = _actualCount != _expectedCount;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Count();
            _expectedCount = 0;
            shouldAppend = _actualCount != _expectedCount;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string assertionName)
        {
            if (_builder is not null)
            {
                ThrowAssertCountFailed(assertionName, _expectedCount, _actualCount, _builder.ToString());
            }
        }

        public void AppendLiteral(string value)
            => _builder!.Append(value);

        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsNotEmptyInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;

        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            shouldAppend = !collection.Any();
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertIsNotEmptyFailed(_builder.ToString());
            }
        }

        public void AppendLiteral(string value)
            => _builder!.Append(value);

        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the items of the collection.</typeparam>
    /// <param name="collection">The collection.</param>
    public static void IsNotEmpty<T>(IEnumerable<T> collection)
        => IsNotEmpty(collection, string.Empty, null);

    /// <summary>
    /// Tests whether the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void IsNotEmpty<T>(IEnumerable<T> collection, string? message)
        => IsNotEmpty(collection, message, null);

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void IsNotEmpty<T>(IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (collection.Any())
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsNotEmptyFailed(userMessage);
    }

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    public static void HasCount<T>(int expected, IEnumerable<T> collection)
        => HasCount(expected, collection, string.Empty, null);

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void HasCount<T>(int expected, IEnumerable<T> collection, string? message)
        => HasCount(expected, collection, message, null);

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion("HasCount");

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void HasCount<T>(int expected, IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => HasCount("HasCount", expected, collection, message, parameters);

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    public static void IsEmpty<T>(IEnumerable<T> collection)
        => IsEmpty(collection, string.Empty, null);

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void IsEmpty<T>(IEnumerable<T> collection, string? message)
        => IsEmpty(collection, message, null);

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion("IsEmpty");

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void IsEmpty<T>(IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => HasCount("IsEmpty", 0, collection, message, parameters);

    private static void HasCount<T>(string assertionName, int expected, IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        int actualCount = collection.Count();
        if (actualCount == expected)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertCountFailed(assertionName, expected, actualCount, userMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertCountFailed(string assertionName, int expectedCount, int actualCount, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.HasCountFailMsg,
            userMessage,
            expectedCount,
            actualCount);
        ThrowAssertFailed($"Assert.{assertionName}", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsNotEmptyFailed(string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsNotEmptyFailMsg,
            userMessage);
        ThrowAssertFailed("Assert.IsNotEmpty", finalMessage);
    }
}
