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
    /// <summary>
    /// Provides an interpolated string handler used by <see cref="Assert.HasCount{T}(int, IEnumerable{T}, ref AssertCountInterpolatedStringHandler{T}, string)"/>
    /// and <see cref="Assert.IsEmpty{T}(IEnumerable{T}, ref AssertCountInterpolatedStringHandler{T}, string)"/>
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TItem">The type of item in the collection.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertCountInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;
        private readonly int _expectedCount;
        private readonly int _actualCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="count">The expected item count.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
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

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="count">The expected item count.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, int count, ReadOnlySpan<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Length;
            _expectedCount = count;
            shouldAppend = _actualCount != _expectedCount;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="count">The expected item count.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, int count, Span<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, count, (ReadOnlySpan<TItem>)collection, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="count">The expected item count.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, int count, ReadOnlyMemory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, count, collection.Span, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertCountInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="count">The expected item count.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertCountInterpolatedStringHandler(int literalLength, int formattedCount, int count, Memory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, count, (ReadOnlyMemory<TItem>)collection, out shouldAppend)
        {
        }

#endif

        internal void ComputeAssertion(string assertionName, string collectionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertCountFailed(assertionName, _expectedCount, _actualCount, _builder.ToString(), collectionExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value)
            => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
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
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsNotEmpty</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TItem">The type of item in the collection.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsNotEmptyInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotEmptyInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            shouldAppend = !collection.Any();
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string collectionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotEmptyFailed(_builder.ToString(), collectionExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value)
            => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
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
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    #region IsNotEmpty

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");
        message.ComputeAssertion(collectionExpression);
    }

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty<T>(IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Any())
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }

    /// <summary>
    /// Tests that the collection is not empty.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty(IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Cast<object>().Any())
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }
    #endregion // IsNotEmpty

    #region HasCount

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount(int expected, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, ReadOnlySpan<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, Span<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, ReadOnlyMemory<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, Memory<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection.Span, message, collectionExpression);

#endif

    #endregion // HasCount

    #region IsEmpty

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty(IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

    #endregion // IsEmpty

    private static void HasCount<T>(string assertionName, int expected, IEnumerable<T> collection, string? message, string collectionExpression)
    {
        // assertionName is one of a small fixed set ("HasCount", "IsEmpty"); use a cached prefixed
        // string instead of allocating "Assert." + assertionName on every call.
        TelemetryCollector.TrackAssertionCall(GetTrackedAssertionName(assertionName));

        int actualCount = collection.Count();
        if (actualCount == expected)
        {
            return;
        }

        ReportAssertCountFailed(assertionName, expected, actualCount, message, collectionExpression);
    }

    private static void HasCount(string assertionName, int expected, IEnumerable collection, string? message, string collectionExpression)
        => HasCount(assertionName, expected, collection.Cast<object>(), message, collectionExpression);

#if NETCOREAPP3_1_OR_GREATER
    private static void HasCount<T>(string assertionName, int expected, ReadOnlySpan<T> collection, string? message, string collectionExpression)
    {
        // assertionName is always "HasCount" here (there are no span/memory IsEmpty overloads that call this);
        // use a cached prefixed string instead of allocating "Assert." + assertionName on every call.
        TelemetryCollector.TrackAssertionCall(GetTrackedAssertionName(assertionName));

        int actualCount = collection.Length;
        if (actualCount == expected)
        {
            return;
        }

        ReportAssertCountFailed(assertionName, expected, actualCount, message, collectionExpression);
    }
#endif

    private static string GetTrackedAssertionName(string assertionName)
        => assertionName switch
        {
            "HasCount" => "Assert.HasCount",
            "IsEmpty" => "Assert.IsEmpty",
            _ => string.Concat("Assert.", assertionName),
        };

    [DoesNotReturn]
    private static void ReportAssertCountFailed(string assertionName, int expectedCount, int actualCount, string? userMessage, string collectionExpression)
    {
        bool isEmptyAssertion = string.Equals(assertionName, nameof(IsEmpty), StringComparison.Ordinal);
        string summary = isEmptyAssertion
            ? FrameworkMessages.IsEmptyFailedSummary
            : FrameworkMessages.HasCountFailedSummary;

        string expectedEvidenceText = expectedCount.ToString(CultureInfo.CurrentCulture);
        string expectedCallSiteText = expectedCount.ToString(CultureInfo.InvariantCulture);
        string actualText = actualCount.ToString(CultureInfo.CurrentCulture);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected count:", expectedEvidenceText)
            .AddLine("actual count:", actualText);

        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedEvidenceText, actualText);
        structured.WithCallSiteExpression(isEmptyAssertion
            ? FormatCallSiteExpression($"Assert.{assertionName}", collectionExpression, "<collection>")
            : FormatCallSiteExpression($"Assert.{assertionName}", expectedCallSiteText, collectionExpression, "<expected>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertIsNotEmptyFailed(string? userMessage, string collectionExpression)
    {
        string actualText = 0.ToString(CultureInfo.CurrentCulture);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("actual count:", actualText);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsNotEmptyFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("> 0", actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsNotEmpty", collectionExpression, "<collection>"));

        ReportAssertFailed(structured);
    }
}
