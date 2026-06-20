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
    public readonly partial struct AssertCountInterpolatedStringHandler<TItem>
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
    }
}
