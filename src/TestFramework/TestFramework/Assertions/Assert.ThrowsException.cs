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
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertNonStrictThrowsInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        public AssertNonStrictThrowsInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: false);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

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

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertThrowsExactlyInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: true);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

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

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException Throws<TException>(Action action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: false, message, actionExpression);

    /// <inheritdoc cref="Throws{TException}(Action, string, string)"/>
    public static TException Throws<TException>(Func<object?> action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: false, message, actionExpression);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException Throws<TException>(Action action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: false, messageBuilder, actionExpression);

    /// <inheritdoc cref="Throws{TException}(Action, Func{Exception?, string}, string)"/>
    public static TException Throws<TException>(Func<object?> action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: false, messageBuilder, actionExpression);

    /// <inheritdoc cref="Throws{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException Throws<TException>(Action action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertNonStrictThrowsInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall("Assert.Throws");
        return message.ComputeAssertion(actionExpression);
    }

    /// <inheritdoc cref="Throws{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException Throws<TException>(Func<object?> action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertNonStrictThrowsInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall("Assert.Throws");
        return message.ComputeAssertion(actionExpression);
    }

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException ThrowsExactly<TException>(Action action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: true, message, actionExpression);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, string)" />
    public static TException ThrowsExactly<TException>(Func<object?> action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: true, message, actionExpression);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException ThrowsExactly<TException>(Action action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: true, messageBuilder, actionExpression);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, Func{Exception?, string}, string)" />
    public static TException ThrowsExactly<TException>(Func<object?> action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
    where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: true, messageBuilder, actionExpression);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException ThrowsExactly<TException>(Action action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertThrowsExactlyInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall("Assert.ThrowsExactly");
        return message.ComputeAssertion(actionExpression);
    }

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException ThrowsExactly<TException>(Func<object?> action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertThrowsExactlyInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall("Assert.ThrowsExactly");
        return message.ComputeAssertion(actionExpression);
    }

    private static TException ThrowsException<TException>(Action action, bool isStrictType, string? message, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = message ?? throw new ArgumentNullException(nameof(message));

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, message, actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    private static TException ThrowsException<TException>(Action action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, messageBuilder(state.ExceptionThrown), actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    public static Task<TException> ThrowsAsync<TException>(Func<Task> action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: false, message, actionExpression);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    public static Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action, string? message = "", [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: true, message, actionExpression);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsAsync<TException>(Func<Task> action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: false, messageBuilder, actionExpression);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throw exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="actionExpression">
    /// The syntactic expression of action as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action, Func<Exception?, string> messageBuilder, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: true, messageBuilder, actionExpression);

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, string? message, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = message ?? throw new ArgumentNullException(nameof(message));

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType).ConfigureAwait(false);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, message, actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType).ConfigureAwait(false);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, messageBuilder(state.ExceptionThrown), actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    [DebuggerDisableUserUnhandledExceptions]
    private static async Task<ThrowsExceptionState> IsThrowsAsyncFailingAsync<TException>(Func<Task> action, bool isStrictType)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;

            return isExceptionOfType
                ? ThrowsExceptionState.CreateNotFailingState(ex)
                : ThrowsExceptionState.CreateWrongTypeState(ex);
        }

        return ThrowsExceptionState.CreateNoExceptionState();
    }

    [DebuggerDisableUserUnhandledExceptions]
    private static ThrowsExceptionState IsThrowsFailing<TException>(Action action, bool isStrictType)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;

            return isExceptionOfType
                ? ThrowsExceptionState.CreateNotFailingState(ex)
                : ThrowsExceptionState.CreateWrongTypeState(ex);
        }

        return ThrowsExceptionState.CreateNoExceptionState();
    }

    [StackTraceHidden]
    private static void ReportThrowsFailed<TException>(
        bool isStrictType,
        ThrowsExceptionState state,
        string? userMessage,
        string actionExpression,
        string assertMethodName)
        where TException : Exception
    {
        Type expectedType = typeof(TException);
        string expectedTypeName = GetDisplayTypeName(expectedType, includeNamespace: false);
        string expectedTypeFullName = GetDisplayTypeName(expectedType, includeNamespace: true);

        StructuredAssertionMessage message;

        if (state.FailureKind == ThrowsFailureKind.NoExceptionThrown)
        {
            string summary = isStrictType
                ? $"Expected exception of exact type {expectedTypeName} but no exception was thrown."
                : $"Expected exception of type {expectedTypeName} (or derived) but no exception was thrown.";

            message = new StructuredAssertionMessage(summary)
                .WithUserMessage(userMessage)
                .WithCallSiteExpression(FormatCallSiteExpression($"Assert.{assertMethodName}<{expectedTypeName}>", actionExpression, "action"));
        }
        else
        {
            Exception actualException = state.ExceptionThrown!;
            Type actualType = actualException.GetType();
            string actualTypeName = GetDisplayTypeName(actualType, includeNamespace: false);
            string actualTypeFullName = GetDisplayTypeName(actualType, includeNamespace: true);

            string summary = isStrictType
                ? $"Expected exception of exact type {expectedTypeName} but caught {actualTypeName}."
                : $"Expected exception of type {expectedTypeName} (or derived) but caught {actualTypeName}.";

            string expectedTypeLabel = isStrictType ? expectedTypeFullName : $"{expectedTypeFullName} (or derived)";

            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("expected type:", expectedTypeLabel)
                .AddLine("actual type:", actualTypeFullName)
                .AddLine("actual exception:", $"{actualTypeFullName}: {actualException.Message}");

            message = new StructuredAssertionMessage(summary)
                .WithUserMessage(userMessage)
                .WithEvidence(evidence)
                .WithCallSiteExpression(FormatCallSiteExpression($"Assert.{assertMethodName}<{expectedTypeName}>", actionExpression, "action"));
        }

        ReportAssertFailed(message);
    }

    // Renders a type name without the CLR backtick-arity suffix and with closed generic arguments expanded recursively
    // (e.g. "MyException`1" with int → "MyException<Int32>") so it stays readable in summary lines and pasteable in call-site lines.
    private static string GetDisplayTypeName(Type type, bool includeNamespace)
    {
        if (!type.IsGenericType)
        {
            return includeNamespace ? type.FullName ?? type.Name : type.Name;
        }

        string name = type.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name.Substring(0, tick);
        }

        if (includeNamespace && !string.IsNullOrEmpty(type.Namespace))
        {
            name = $"{type.Namespace}.{name}";
        }

        Type[] args = type.GetGenericArguments();
        StringBuilder sb = new(name.Length + 16);
        sb.Append(name);
        sb.Append('<');
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(GetDisplayTypeName(args[i], includeNamespace));
        }

        sb.Append('>');
        return sb.ToString();
    }

    private enum ThrowsFailureKind : byte
    {
        NotFailing,
        NoExceptionThrown,
        WrongExceptionType,
    }

    [StackTraceHidden]
    private readonly struct ThrowsExceptionState
    {
        public Exception? ExceptionThrown { get; }

        public ThrowsFailureKind FailureKind { get; }

        private ThrowsExceptionState(ThrowsFailureKind failureKind, Exception? exceptionThrown)
        {
            ExceptionThrown = exceptionThrown;
            FailureKind = failureKind;
        }

        public static ThrowsExceptionState CreateWrongTypeState(Exception exceptionThrown)
            => new(ThrowsFailureKind.WrongExceptionType, exceptionThrown);

        public static ThrowsExceptionState CreateNoExceptionState()
            => new(ThrowsFailureKind.NoExceptionThrown, null);

        public static ThrowsExceptionState CreateNotFailingState(Exception exception)
            => new(ThrowsFailureKind.NotFailing, exception);
    }

    // assertMethodName comes from [CallerMemberName] for the Throws/ThrowsExactly/ThrowsAsync/
    // ThrowsExactlyAsync helpers — a small fixed set. Use a switch to avoid allocating a fresh
    // "Assert." + name string on every call.
    private static string GetTrackedThrowsName(string assertMethodName)
        => assertMethodName switch
        {
            "Throws" => "Assert.Throws",
            "ThrowsExactly" => "Assert.ThrowsExactly",
            "ThrowsAsync" => "Assert.ThrowsAsync",
            "ThrowsExactlyAsync" => "Assert.ThrowsExactlyAsync",
            _ => string.Concat("Assert.", assertMethodName),
        };
}
