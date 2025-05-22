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

        internal TException ComputeAssertion()
        {
            if (_state.FailAction is not null)
            {
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
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

        internal TException ComputeAssertion()
        {
            if (_state.FailAction is not null)
            {
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="messageArgs">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException Throws<TException>(Action action, string message = "", params object[] messageArgs)
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: false, message, parameters: messageArgs);

    /// <inheritdoc cref="Throws{TException}(Action, string, object[])"/>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
    public static TException Throws<TException>(Func<object?> action, string message = "", params object[] messageArgs)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: false, message, parameters: messageArgs);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException Throws<TException>(Action action, Func<Exception?, string> messageBuilder)
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: false, messageBuilder);

    /// <inheritdoc cref="Throws{TException}(Action, Func{Exception?, string})"/>
    public static TException Throws<TException>(Func<object?> action, Func<Exception?, string> messageBuilder)
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: false, messageBuilder);

    /// <inheritdoc cref="Throws{TException}(Action, string, object[])" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException Throws<TException>(Action action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertNonStrictThrowsInterpolatedStringHandler<TException> message)
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
        => message.ComputeAssertion();

    /// <inheritdoc cref="Throws{TException}(Action, string, object[])" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException Throws<TException>(Func<object?> action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertNonStrictThrowsInterpolatedStringHandler<TException> message)
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
        => message.ComputeAssertion();

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="messageArgs">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException ThrowsExactly<TException>(Action action, string message = "", params object[] messageArgs)
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: true, message, parameters: messageArgs);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, object[])" />
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
    public static TException ThrowsExactly<TException>(Func<object?> action, string message = "", params object[] messageArgs)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: true, message, parameters: messageArgs);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static TException ThrowsExactly<TException>(Action action, Func<Exception?, string> messageBuilder)
        where TException : Exception
        => ThrowsException<TException>(action, isStrictType: true, messageBuilder);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, Func{Exception?, string})" />
    public static TException ThrowsExactly<TException>(Func<object?> action, Func<Exception?, string> messageBuilder)
    where TException : Exception
        => ThrowsException<TException>(() => _ = action(), isStrictType: true, messageBuilder);

    /// <inheritdoc cref="ThrowsExactly{TException}(Action, string, object[])" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static TException ThrowsExactly<TException>(Action action, [InterpolatedStringHandlerArgument(nameof(action))] ref AssertThrowsExactlyInterpolatedStringHandler<TException> message)
#pragma warning restore IDE0060 // Remove unused parameter
        where TException : Exception
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <typeparam name="T">
    /// The exact type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Action action)
        where T : Exception
        => ThrowsException<T>(action, string.Empty, null);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Action action, string message)
        where T : Exception
        => ThrowsException<T>(action, message, null);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Func<object?> action)
        where T : Exception
        => ThrowsException<T>(action, string.Empty, null);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Func<object?> action, string message)
        where T : Exception
        => ThrowsException<T>(action, message, null);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throw exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Func<object?> action, string message, params object?[]? parameters)
        where T : Exception
#pragma warning disable IDE0053 // Use expression body for lambda expression
        // Despite the discard, using lambda makes the action considered as Func and so recursing on the same method
        => ThrowsException<T>(() => { _ = action(); }, message, parameters);
#pragma warning restore IDE0053 // Use expression body for lambda expression

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ThrowsException<T>(Action action, string message, params object?[]? parameters)
        where T : Exception
        => ThrowsException<T>(action, isStrictType: true, message, parameters: parameters);

    private static TException ThrowsException<TException>(Action action, bool isStrictType, string message, [CallerMemberName] string assertMethodName = "", params object?[]? parameters)
        where TException : Exception
    {
        Guard.NotNull(action);
        Guard.NotNull(message);

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType, assertMethodName);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessage(message, parameters));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
        return null!;
    }

    private static TException ThrowsException<TException>(Action action, bool isStrictType, Func<Exception?, string> messageBuilder, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        Guard.NotNull(action);
        Guard.NotNull(messageBuilder);

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType, assertMethodName);
        if (state.FailAction is not null)
        {
            state.FailAction(messageBuilder(state.ExceptionThrown));
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
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="messageArgs">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsAsync<TException>(Func<Task> action, string message = "", params object[] messageArgs)
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: false, message, parameters: messageArgs);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <param name="messageArgs">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action, string message = "", params object[] messageArgs)
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: true, message, parameters: messageArgs);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (or derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsAsync<TException>(Func<Task> action, Func<Exception?, string> messageBuilder)
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: false, messageBuilder);

    /// <summary>
    /// Asserts that the delegate <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>
    /// (and not of derived type) and throws <c>AssertFailedException</c> if code does not throws exception or throws
    /// exception of type other than <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <param name="messageBuilder">
    /// A func that takes the thrown Exception (or null if the action didn't throw any exception) to construct the message to include in the exception when <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </param>
    /// <typeparam name="TException">
    /// The type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="TException"/>.
    /// </exception>
    /// <returns>
    /// The exception that was thrown.
    /// </returns>
    public static Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action, Func<Exception?, string> messageBuilder)
        where TException : Exception
        => ThrowsExceptionAsync<TException>(action, isStrictType: true, messageBuilder);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">
    /// Delegate to code to be tested and which is expected to throw exception.
    /// </param>
    /// <typeparam name="T">
    /// Type of exception expected to be thrown.
    /// </typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The <see cref="Task"/> executing the delegate.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action)
        where T : Exception
        => await ThrowsExceptionAsync<T>(action, string.Empty, null)
            .ConfigureAwait(false);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">Delegate to code to be tested and which is expected to throw exception.</param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <typeparam name="T">Type of exception expected to be thrown.</typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The <see cref="Task"/> executing the delegate.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action, string message)
        where T : Exception
        => await ThrowsExceptionAsync<T>(action, message, null)
            .ConfigureAwait(false);

    /// <summary>
    /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception
    /// of type <typeparamref name="T"/> (and not of derived type) and throws <c>AssertFailedException</c>
    /// if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="action">Delegate to code to be tested and which is expected to throw exception.</param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="action"/>
    /// does not throws exception of type <typeparamref name="T"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <typeparam name="T">Type of exception expected to be thrown.</typeparam>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="action"/> does not throws exception of type <typeparamref name="T"/>.
    /// </exception>
    /// <returns>
    /// The <see cref="Task"/> executing the delegate.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action, string message, params object?[]? parameters)
        where T : Exception
        => await ThrowsExceptionAsync<T>(action, true, message, parameters: parameters)
            .ConfigureAwait(false);

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, string message, [CallerMemberName] string assertMethodName = "", params object?[]? parameters)
        where TException : Exception
    {
        Guard.NotNull(action);
        Guard.NotNull(message);

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType, assertMethodName).ConfigureAwait(false);
        if (state.FailAction is not null)
        {
            state.FailAction(BuildUserMessage(message, parameters));
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // This will not hit, but need it for compiler.
        return null!;
    }

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, Func<Exception?, string> messageBuilder, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        Guard.NotNull(action);
        Guard.NotNull(messageBuilder);

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType, assertMethodName).ConfigureAwait(false);
        if (state.FailAction is not null)
        {
            state.FailAction(messageBuilder(state.ExceptionThrown));
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
                        ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
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
                        ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
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
