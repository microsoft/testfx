// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
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
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertIsNotEmptyInterpolatedStringHandler<TItem>
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

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotEmptyInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, ReadOnlySpan<TItem> collection, out bool shouldAppend)
        {
            shouldAppend = collection.Length == 0;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotEmptyInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, Span<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, (ReadOnlySpan<TItem>)collection, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotEmptyInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, ReadOnlyMemory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, collection.Span, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotEmptyInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotEmptyInterpolatedStringHandler(int literalLength, int formattedCount, Memory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, (ReadOnlyMemory<TItem>)collection, out shouldAppend)
        {
        }

#endif

        internal void ComputeAssertion(string collectionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotEmptyFailed(_builder.ToString(), collectionExpression);
            }
        }
    }

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

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

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests that the span is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(ReadOnlySpan<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");
        message.ComputeAssertion(collectionExpression);
    }

    /// <summary>
    /// Tests that the span is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty<T>(ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Length != 0)
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }

    /// <summary>
    /// Tests that the span is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(Span<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");
        message.ComputeAssertion(collectionExpression);
    }

    /// <summary>
    /// Tests that the span is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty<T>(Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Length != 0)
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(ReadOnlyMemory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");
        message.ComputeAssertion(collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty<T>(ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Length != 0)
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsNotEmpty<T>(Memory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertIsNotEmptyInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");
        message.ComputeAssertion(collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsNotEmpty<T>(Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotEmpty");

        if (collection.Length != 0)
        {
            return;
        }

        ReportAssertIsNotEmptyFailed(message, collectionExpression);
    }

#endif

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    [DoesNotReturn]
    private static void ReportAssertIsNotEmptyFailed(string? userMessage, string collectionExpression)
    {
        string actualText = "0";
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
