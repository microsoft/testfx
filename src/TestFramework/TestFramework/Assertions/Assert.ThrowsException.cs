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
            _state = IsThrowsFailing<TException>(action, isStrictType: false, "Throws");
            shouldAppend = _state.FailAction is not null;
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
            if (_state.FailAction is not null)
            {
                _builder!.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "action", actionExpression) + " ");
                _state.FailAction(_builder!.ToString());
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // This will not hit, but need it for compiler.
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

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertThrowsExactlyInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: true, "ThrowsExactly");
            shouldAppend = _state.FailAction is not null;
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
            if (_state.FailAction is not null)
            {
                _builder!.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "action", actionExpression) + " ");
                _state.FailAction(_builder!.ToString());
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // This will not hit, but need it for compiler.
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
        => message.ComputeAssertion(actionExpression);

    /// <inheritdoc cref="Throws{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException Throws<TException>(Func<object?> action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertNonStrictThrowsInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
        => message.ComputeAssertion(actionExpression);

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
        => message.ComputeAssertion(actionExpression);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException ThrowsExactly<TException>(Func<object?> action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertThrowsExactlyInterpolatedStringHandler<TException> message, [CallerArgumentExpression(nameof(action))] string actionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
        => message.ComputeAssertion(actionExpression);

    private static TException ThrowsException<TException>(Action action, bool isStrictType, string? message, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        Ensure.NotNull(action);
        Ensure.NotNull(message);

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType, assertMethodName);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessageForActionExpression(message, actionExpression));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
        return null!;
    }

    private static TException ThrowsException<TException>(Action action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        Ensure.NotNull(action);
        Ensure.NotNull(messageBuilder);

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType, assertMethodName);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessageForActionExpression(messageBuilder(state.ExceptionThrown), actionExpression));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
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
        Ensure.NotNull(action);
        Ensure.NotNull(message);

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType, assertMethodName).ConfigureAwait(false);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessageForActionExpression(message, actionExpression));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
        return null!;
    }

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        Ensure.NotNull(action);
        Ensure.NotNull(messageBuilder);

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType, assertMethodName).ConfigureAwait(false);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessageForActionExpression(messageBuilder(state.ExceptionThrown), actionExpression));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
        return null!;
    }

    private static async Task<ThrowsExceptionState> IsThrowsAsyncFailingAsync<TException>(Func<Task> action, bool isStrictType, string assertMethodName)
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
                : ThrowsExceptionState.CreateFailingState(
                    userMessage =>
                    {
                        string finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.WrongExceptionThrown,
                            userMessage,
                            typeof(TException),
                            ex.GetType());
                        ThrowAssertFailed("Assert." + assertMethodName, finalMessage, actual: ex);
                    }, ex);
        }

        return ThrowsExceptionState.CreateFailingState(
            failAction: userMessage =>
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.NoExceptionThrown,
                    userMessage,
                    typeof(TException));
                ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
            }, null);
    }

    private static ThrowsExceptionState IsThrowsFailing<TException>(Action action, bool isStrictType, string assertMethodName)
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
                : ThrowsExceptionState.CreateFailingState(
                    userMessage =>
                    {
                        string finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.WrongExceptionThrown,
                            userMessage,
                            typeof(TException),
                            ex.GetType());
                        ThrowAssertFailed("Assert." + assertMethodName, finalMessage, actual: ex);
                    }, ex);
        }

        return ThrowsExceptionState.CreateFailingState(
            failAction: userMessage =>
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.NoExceptionThrown,
                    userMessage,
                    typeof(TException));
                ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
            }, null);
    }

    private readonly struct ThrowsExceptionState
    {
        public Exception? ExceptionThrown { get; }

        public Action<string>? FailAction { get; }

        private ThrowsExceptionState(Exception? exceptionThrown, Action<string>? failAction)
        {
            // If the assert is failing, failAction should be non-null, and exceptionWhenNotFailing may or may not be null.
            // If the assert is not failing, exceptionWhenNotFailing should be non-null, and failAction should be null.
            ExceptionThrown = exceptionThrown;
            FailAction = failAction;
        }

        public static ThrowsExceptionState CreateFailingState(Action<string> failAction, Exception? exceptionThrown)
            => new(exceptionThrown, failAction);

        public static ThrowsExceptionState CreateNotFailingState(Exception exception)
            => new(exception, failAction: null);
    }
}
