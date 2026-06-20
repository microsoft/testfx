// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.ContainsSingle</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TItem">The type of item in the collection.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly partial struct AssertSingleInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;
        private readonly int _actualCount;
        private readonly TItem? _item;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertSingleInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Count();
            shouldAppend = _actualCount != 1;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
            else
            {
                _item = collection.First();
            }
        }

        internal TItem ComputeAssertion(string collectionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertContainsSingleFailed(_actualCount, _builder.ToString(), collectionExpression);
            }

            return _item!;
        }
    }

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    #region ContainsSingle

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        T item = default!;
        int count = 0;
        foreach (T current in collection)
        {
            if (count == 0)
            {
                item = current;
            }

            count++;
        }

        if (count == 1)
        {
            return item;
        }

        ReportAssertContainsSingleFailed(count, message, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default!;
    }

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static object? ContainsSingle(IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        object? item = null;
        int count = 0;
        foreach (object? current in collection)
        {
            if (count == 0)
            {
                item = current;
            }

            count++;
        }

        if (count == 1)
        {
            return item;
        }

        ReportAssertContainsSingleFailed(count, message, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default;
    }

    /// <summary>
    /// Tests whether the specified collection contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item that matches the predicate.</returns>
    public static T ContainsSingle<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsSingle");
        T firstMatch = default!;
        int matchCount = 0;
        foreach (T item in collection)
        {
            if (!predicate(item))
            {
                continue;
            }

            if (matchCount == 0)
            {
                firstMatch = item;
            }

            matchCount++;
        }

        if (matchCount == 1)
        {
            return firstMatch;
        }

        ReportAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default!;
    }

    /// <summary>
    /// Tests whether the specified collection contains exactly one element that matches the given predicate.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item that matches the predicate.</returns>
    public static object? ContainsSingle(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsSingle");
        object? firstMatch = null;
        int matchCount = 0;
        foreach (object? item in collection)
        {
            if (!predicate(item))
            {
                continue;
            }

            if (matchCount == 0)
            {
                firstMatch = item;
            }

            matchCount++;
        }

        if (matchCount == 1)
        {
            return firstMatch;
        }

        ReportAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default;
    }

    #endregion // ContainsSingle
    [DoesNotReturn]
    private static void ReportAssertContainsSingleFailed(int actualCount, string? userMessage, string collectionExpression)
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected count:", "1")
            .AddLine("actual count:", actualCount.ToString(CultureInfo.CurrentCulture));
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSingleFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("1", actualCount.ToString(CultureInfo.CurrentCulture));
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.ContainsSingle", collectionExpression, "<collection>"));
        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertSingleMatchFailed(int actualCount, string? userMessage, string predicateExpression, string collectionExpression)
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected matches:", "1")
            .AddLine("actual matches:", actualCount.ToString(CultureInfo.CurrentCulture));
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSingleMatchFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("1", actualCount.ToString(CultureInfo.CurrentCulture));
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.ContainsSingle", predicateExpression, collectionExpression, "<predicate>", "<collection>"));
        ReportAssertFailed(structured);
    }
}
