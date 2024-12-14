// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
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

        string userMessage, finalMessage;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;
            if (!isExceptionOfType)
            {
                userMessage = BuildUserMessage(message, parameters);
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.WrongExceptionThrown,
                    userMessage,
                    typeof(TException),
                    ex.GetType());
                ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
            }

            return (TException)ex;
        }

        userMessage = BuildUserMessage(message, parameters);
        finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.NoExceptionThrown,
            userMessage,
            typeof(TException));
        ThrowAssertFailed("Assert." + assertMethodName, finalMessage);

        // This will not hit, but need it for compiler.
        return null;
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

        string userMessage, finalMessage;
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;

            if (!isExceptionOfType)
            {
                userMessage = BuildUserMessage(message, parameters);
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.WrongExceptionThrown,
                    userMessage,
                    typeof(TException),
                    ex.GetType());
                ThrowAssertFailed("Assert." + assertMethodName, finalMessage);
            }

            return (TException)ex;
        }

        userMessage = BuildUserMessage(message, parameters);
        finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.NoExceptionThrown,
            userMessage,
            typeof(TException));
        ThrowAssertFailed("Assert." + assertMethodName, finalMessage);

        // This will not hit, but need it for compiler.
        return null!;
    }
}
