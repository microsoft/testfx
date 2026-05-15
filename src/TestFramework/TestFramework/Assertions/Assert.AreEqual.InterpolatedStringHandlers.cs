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
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _expected;
        private readonly object? _actual;

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
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionTwoParametersMessage, "expected", expectedExpression, "actual", actualExpression) + " ");
                ReportAssertAreEqualFailed(_expected, _actual, _builder.ToString());
            }
        }

        public void AppendLiteral(string? value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _notExpected;
        private readonly object? _actual;

        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, null, out shouldAppend)
        {
        }

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
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionTwoParametersMessage, "notExpected", notExpectedExpression, "actual", actualExpression) + " ");
                ReportAssertAreNotEqualFailed(_notExpected, _actual, _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string>? _failAction;

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
            : this(literalLength, formattedCount, expected, actual, ignoreCase, CultureInfo.InvariantCulture, out shouldAppend)
        {
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreEqualFailed(expected, actual, ignoreCase, culture, userMessage);
            }
        }

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
        {
            if (_failAction is not null)
            {
                _builder!.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionTwoParametersMessage, "expected", expectedExpression, "actual", actualExpression) + " ");
                _failAction.Invoke(_builder!.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string>? _failAction;

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, float notExpected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal notExpected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, long notExpected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, double notExpected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, out shouldAppend)
        {
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ReportAssertAreNotEqualFailed(notExpected, actual, userMessage);
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
        {
            if (_failAction is not null)
            {
                _builder!.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionTwoParametersMessage, "notExpected", notExpectedExpression, "actual", actualExpression) + " ");
                _failAction.Invoke(_builder!.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
