// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.Throws</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertNonStrictThrowsInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonStrictThrowsInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonStrictThrowsInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: false);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonStrictThrowsInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonStrictThrowsInterpolatedStringHandler(int literalLength, int formattedCount, Func<object?> action, out bool shouldAppend)
            : this(literalLength, formattedCount, (Action)(() => _ = action()), out shouldAppend)
        {
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: false, _state, _builder!.ToString(), actionExpression, nameof(Throws));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.ThrowsExactly</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertThrowsExactlyInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertThrowsExactlyInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: true);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertThrowsExactlyInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Func<object?> action, out bool shouldAppend)
            : this(literalLength, formattedCount, (Action)(() => _ = action()), out shouldAppend)
        {
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: true, _state, _builder!.ToString(), actionExpression, nameof(ThrowsExactly));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.ThrowsAsync</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertNonStrictThrowsAsyncInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonStrictThrowsAsyncInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonStrictThrowsAsyncInterpolatedStringHandler(int literalLength, int formattedCount, Func<Task> action, out bool shouldAppend)
        {
            // The interpolated string handler pattern requires deciding 'shouldAppend' synchronously in the
            // constructor (the compiler skips evaluating the interpolation holes entirely when it is false).
            // Determining whether the assertion fails requires running the action, so the awaited task is
            // observed synchronously here. The async core uses ConfigureAwait(false) throughout.
            _state = IsThrowsAsyncFailingAsync<TException>(action, isStrictType: false).GetAwaiter().GetResult();
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: false, _state, _builder!.ToString(), actionExpression, nameof(ThrowsAsync));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.ThrowsExactlyAsync</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertThrowsExactlyAsyncInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertThrowsExactlyAsyncInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertThrowsExactlyAsyncInterpolatedStringHandler(int literalLength, int formattedCount, Func<Task> action, out bool shouldAppend)
        {
            // The interpolated string handler pattern requires deciding 'shouldAppend' synchronously in the
            // constructor (the compiler skips evaluating the interpolation holes entirely when it is false).
            // Determining whether the assertion fails requires running the action, so the awaited task is
            // observed synchronously here. The async core uses ConfigureAwait(false) throughout.
            _state = IsThrowsAsyncFailingAsync<TException>(action, isStrictType: true).GetAwaiter().GetResult();
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: true, _state, _builder!.ToString(), actionExpression, nameof(ThrowsExactlyAsync));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
        }
    }
}
