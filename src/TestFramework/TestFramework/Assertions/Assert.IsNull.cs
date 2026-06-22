// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
    // Keep the compiler-facing handler overloads in sync with the related handlers in Assert.IsTrue.cs.

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsNull</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertIsNullInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNullInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNullInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsNullFailing(value);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNullFailed(_value, _builder.ToString(), valueExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsNotNull</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertIsNotNullInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotNullInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotNullInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            shouldAppend = IsNotNullFailing(value);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotNullFailed(_builder.ToString(), valueExpression, "value");
            }
        }
    }

    /// <inheritdoc cref="IsNull(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNull(object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertIsNullInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNull");
        message.ComputeAssertion(valueExpression);
    }

    /// <summary>
    /// Tests whether the specified object is null and throws an exception
    /// if it is not.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not null. The message is shown in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null.
    /// </exception>
    public static void IsNull(object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNull");

        if (IsNullFailing(value))
        {
            ReportAssertIsNullFailed(value, message, valueExpression);
        }
    }

    private static bool IsNullFailing(object? value) => value is not null;

    [DoesNotReturn]
    private static void ReportAssertIsNullFailed(object? value, string? message, string valueExpression)
    {
        string actualValue = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("actual:", actualValue);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsNullFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(AssertionValueRenderer.RenderValue(null), actualValue);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsNull", valueExpression, nameof(value)));

        ReportAssertFailed(structured);
    }

    /// <inheritdoc cref="IsNull(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotNull([NotNull] object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertIsNotNullInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Deliberately keeping [NotNull] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotNull");
        message.ComputeAssertion(valueExpression);
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is non-null and throws an exception
    /// if it is null.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is null. The message is shown in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null.
    /// </exception>
    public static void IsNotNull([NotNull] object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotNull");

        if (IsNotNullFailing(value))
        {
            ReportAssertIsNotNullFailed(message, valueExpression, nameof(value));
        }
    }

    private static bool IsNotNullFailing([NotNullWhen(false)] object? value) => value is null;

    [DoesNotReturn]
    private static void ReportAssertIsNotNullFailed(string? message, string valueExpression, string paramName)
    {
        string actualValue = AssertionValueRenderer.RenderValue(null);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("actual:", actualValue);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsNotNullFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("not null", actualValue);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsNotNull", valueExpression, paramName));

        ReportAssertFailed(structured);
    }
}
