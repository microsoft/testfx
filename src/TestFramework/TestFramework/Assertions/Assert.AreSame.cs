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
    /// Provides an interpolated string handler used by <c>Assert.AreSame</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly partial struct AssertAreSameInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly TArgument? _expected;
        private readonly TArgument? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreSameInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
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

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreSameFailed(_expected, _actual, _builder.ToString(), expectedExpression, actualExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreNotSame</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly partial struct AssertAreNotSameInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly TArgument? _notExpected;
        private readonly TArgument? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreNotSameInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreNotSameInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
        {
            _notExpected = notExpected;
            _actual = actual;
            shouldAppend = IsAreNotSameFailing(notExpected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreNotSameFailed(_notExpected, _actual, _builder.ToString(), notExpectedExpression, actualExpression);
            }
        }
    }

    /// <inheritdoc cref="AreSame{T}(T, T, string?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreSame<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreSameInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSame");
        message.ComputeAssertion(expectedExpression, actualExpression);
    }

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

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
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSame");

        if (!IsAreSameFailing(expected, actual))
        {
            return;
        }

        ReportAssertAreSameFailed(expected, actual, message, expectedExpression, actualExpression);
    }

    private static bool IsAreSameFailing<T>(T? expected, T? actual)
        => !object.ReferenceEquals(expected, actual);

    [DoesNotReturn]
    private static void ReportAssertAreSameFailed<T>(T? expected, T? actual, string? userMessage, string expectedExpression, string actualExpression)
    {
        StructuredAssertionMessage msg = new("Expected both values to refer to the same object.");

        if (expected is ValueType && actual is ValueType)
        {
            msg.WithAdditionalSummaryLine("Do not pass value types to AreSame \u2014 value types are boxed on each call, so references will never be the same.");
        }

        msg.WithUserMessage(userMessage);

        if (expected is not ValueType || actual is not ValueType)
        {
            string expectedText = expected is null ? "null" : $"{expected.GetType()} (hash: 0x{RuntimeHelpers.GetHashCode(expected):X})";
            string actualText = actual is null ? "null" : $"{actual.GetType()} (hash: 0x{RuntimeHelpers.GetHashCode(actual):X})";
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("expected:", expectedText)
                .AddLine("actual:", actualText);
            msg.WithEvidence(evidence).WithExpectedAndActual(expectedText, actualText);
        }

        msg.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreSame", expectedExpression, actualExpression, "<expected>", "<actual>"));

        ReportAssertFailed(msg);
    }

    /// <inheritdoc cref="AreNotSame{T}(T, T, string?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotSame<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotSameInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSame");
        message.ComputeAssertion(notExpectedExpression, actualExpression);
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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is the same as <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSame");

        if (IsAreNotSameFailing(notExpected, actual))
        {
            ReportAssertAreNotSameFailed(notExpected, actual, message, notExpectedExpression, actualExpression);
        }
    }

    private static bool IsAreNotSameFailing<T>(T? notExpected, T? actual)
        => object.ReferenceEquals(notExpected, actual);

    [DoesNotReturn]
    private static void ReportAssertAreNotSameFailed<T>(T? notExpected, T? actual, string? userMessage, string notExpectedExpression, string actualExpression)
    {
        StructuredAssertionMessage msg = new("Expected values to refer to different objects.");

        msg.WithAdditionalSummaryLine(
            notExpected is null && actual is null
                ? "Both values are null."
                : "Both values refer to the same object.");

        msg.WithUserMessage(userMessage);

        msg.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreNotSame", notExpectedExpression, actualExpression, "<notExpected>", "<actual>"));

        ReportAssertFailed(msg);
    }
}
