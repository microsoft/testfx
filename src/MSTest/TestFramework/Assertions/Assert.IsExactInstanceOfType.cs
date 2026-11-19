// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2263 // Prefer generic overload when type is known - false positives

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsExactInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertIsExactInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _expectedType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsExactInstanceOfTypeInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="expectedType">The expected type for the value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? expectedType, out bool shouldAppend)
        {
            _value = value;
            _expectedType = expectedType;
            shouldAppend = IsExactInstanceOfTypeFailing(value, expectedType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsExactInstanceOfTypeFailed(_value, _expectedType, _builder.ToString(), valueExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsExactInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArg">The type the value is expected to be related to.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler{TArg}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsExactInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsExactInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString(), valueExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsNotExactInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertIsNotExactInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _wrongType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotExactInstanceOfTypeInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="wrongType">The type the value is not expected to be an instance of.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? wrongType, out bool shouldAppend)
        {
            _value = value;
            _wrongType = wrongType;
            shouldAppend = IsNotExactInstanceOfTypeFailing(value, wrongType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotExactInstanceOfTypeFailed(_value, _wrongType, _builder.ToString(), valueExpression);
            }
        }
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsNotExactInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArg">The type the value is expected to be related to.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [GenerateAssertInterpolatedStringAppendMethods]
    public readonly partial struct AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler{TArg}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsNotExactInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotExactInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString(), valueExpression);
            }
        }
    }

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether the specified object is exactly an instance of the expected
    /// type and throws an exception if the expected type does not match exactly.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected exact type of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not exactly an instance of <paramref name="expectedType"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not exactly the type
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsExactInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsExactInstanceOfType");

        if (IsExactInstanceOfTypeFailing(value, expectedType))
        {
            ReportAssertIsExactInstanceOfTypeFailed(value, expectedType, message, valueExpression);
        }
    }

    /// <inheritdoc cref="IsExactInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsExactInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [InterpolatedStringHandlerArgument(nameof(value), nameof(expectedType))] ref AssertIsExactInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Deliberately keeping [NotNull] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsExactInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is exactly an instance of the generic
    /// type and throws an exception if the generic type does not match exactly.
    /// </summary>
    /// <typeparam name="T">The expected exact type of <paramref name="value"/>.</typeparam>
    public static T IsExactInstanceOfType<T>([NotNull] object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        IsExactInstanceOfType(value, typeof(T), message, valueExpression);
        return (T)value;
    }

    /// <inheritdoc cref="IsExactInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static T IsExactInstanceOfType<T>([NotNull] object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Deliberately keeping [NotNull] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsExactInstanceOfType");
        message.ComputeAssertion(valueExpression);
        return (T)value!;
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    private static bool IsExactInstanceOfTypeFailing([NotNullWhen(false)] object? value, [NotNullWhen(false)] Type? expectedType)
        => IsTypeMatchFailing(value, expectedType, exact: true);

    [DoesNotReturn]
    private static void ReportAssertIsExactInstanceOfTypeFailed(object? value, Type? expectedType, string? userMessage, string valueExpression)
        => ReportAssertTypeMatchFailed(value, expectedType, userMessage, valueExpression, exact: true);

    /// <summary>
    /// Tests whether the specified object is not exactly an instance of the wrong
    /// type and throws an exception if the specified type matches exactly.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be exactly of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The exact type that <paramref name="value"/> should not be.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is exactly an instance of <paramref name="wrongType"/>. The message is shown
    /// in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is exactly the type
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotExactInstanceOfType(object? value, [NotNull] Type? wrongType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotExactInstanceOfType");

        if (IsNotExactInstanceOfTypeFailing(value, wrongType))
        {
            ReportAssertIsNotExactInstanceOfTypeFailed(value, wrongType, message, valueExpression);
        }
    }

    /// <inheritdoc cref="IsNotExactInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotExactInstanceOfType(object? value, [NotNull] Type? wrongType, [InterpolatedStringHandlerArgument(nameof(value), nameof(wrongType))] ref AssertIsNotExactInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotExactInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is not exactly an instance of the wrong generic
    /// type and throws an exception if the specified type matches exactly.
    /// </summary>
    /// <typeparam name="T">The exact type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotExactInstanceOfType<T>(object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => IsNotExactInstanceOfType(value, typeof(T), message, valueExpression);

    /// <inheritdoc cref="IsNotExactInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotExactInstanceOfType<T>(object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotExactInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }

    private static bool IsNotExactInstanceOfTypeFailing(object? value, [NotNullWhen(false)] Type? wrongType)
        => IsTypeMismatchFailing(value, wrongType, exact: true);

    [DoesNotReturn]
    private static void ReportAssertIsNotExactInstanceOfTypeFailed(object? value, Type? wrongType, string? userMessage, string valueExpression)
        => ReportAssertTypeMismatchFailed(value, wrongType, userMessage, valueExpression, exact: true);
}
