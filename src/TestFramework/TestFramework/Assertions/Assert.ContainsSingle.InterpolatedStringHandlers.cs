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
    [GenerateAssertInterpolatedStringAppendMethods]
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

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertSingleInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, ReadOnlySpan<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Length;
            shouldAppend = _actualCount != 1;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
            else
            {
                _item = collection[0];
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertSingleInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, Span<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, (ReadOnlySpan<TItem>)collection, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertSingleInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, ReadOnlyMemory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, collection.Span, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertSingleInterpolatedStringHandler{TItem}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="collection">The collection being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, Memory<TItem> collection, out bool shouldAppend)
            : this(literalLength, formattedCount, (ReadOnlyMemory<TItem>)collection, out shouldAppend)
        {
        }

#endif

        internal TItem ComputeAssertion(string collectionExpression)
        {
            if (_builder is not null)
            {
                ReportAssertContainsSingleFailed(_actualCount, _builder.ToString(), collectionExpression);
            }

            return _item!;
        }
    }
}
