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
    /// Provides an interpolated string handler used by <c>Assert.AreEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods(NullableLiteralParameter = true)]
    public readonly partial struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _expected;
        private readonly object? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, out bool shouldAppend)
        {
            _expected = expected!;
            shouldAppend = AreEqualFailing(expected, actual, null);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="comparer">The equality comparer used to compare values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreEqualFailed(_expected, _actual, _builder.ToString(), expectedExpression, actualExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreNotEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _notExpected;
        private readonly object? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreNotEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, null, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreNotEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="comparer">The equality comparer used to compare values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _notExpected = notExpected;
                _actual = actual;
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreNotEqualFailed(_notExpected, _actual, _builder.ToString(), notExpectedExpression, actualExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string, string, string>? _failAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, CultureInfo.InvariantCulture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="culture">The culture used for the string comparison.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, ignoreCase, culture, cultureExplicit: true, userMessage, expectedExpression, actualExpression);
            }
        }

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
            => _failAction?.Invoke(_builder!.ToString(), expectedExpression, actualExpression);
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreNotEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string, string, string>? _failAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, float notExpected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal notExpected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, long notExpected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, double notExpected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="culture">The culture used for the string comparison.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, culture, cultureExplicit: true, userMessage, notExpectedExpr, actualExpr);
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
            => _failAction?.Invoke(_builder!.ToString(), notExpectedExpression, actualExpression);
    }
}
