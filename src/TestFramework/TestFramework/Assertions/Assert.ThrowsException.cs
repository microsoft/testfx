// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
