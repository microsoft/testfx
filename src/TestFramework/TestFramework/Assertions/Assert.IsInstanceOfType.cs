// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public static partial class Assert
{
    /// <summary>
    /// Tests whether the specified object is an instance of the expected
    /// type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType)
        => IsInstanceOfType(value, expectedType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value)
        => IsInstanceOfType(value, typeof(T), string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance)
        => IsInstanceOfType<T>(value, out instance, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the expected
    /// type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not an instance of <paramref name="expectedType"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, string? message)
        => IsInstanceOfType(value, expectedType, message, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, string? message)
        => IsInstanceOfType(value, typeof(T), message, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance, string? message)
        => IsInstanceOfType<T>(value, out instance, message, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the expected
    /// type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not an instance of <paramref name="expectedType"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (expectedType == null || value == null)
        {
            ThrowAssertFailed("Assert.IsInstanceOfType", BuildUserMessage(message, parameters));
        }

        TypeInfo elementTypeInfo = value.GetType().GetTypeInfo();
        TypeInfo expectedTypeInfo = expectedType.GetTypeInfo();
        if (!expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsInstanceOfFailMsg,
                userMessage,
                expectedType.ToString(),
                value.GetType().ToString());
            ThrowAssertFailed("Assert.IsInstanceOfType", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => IsInstanceOfType(value, typeof(T), message, parameters);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        IsInstanceOfType(value, typeof(T), message, parameters);
        instance = (T)value;
    }

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The type that <paramref name="value"/> should not be.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType)
        => IsNotInstanceOfType(value, wrongType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value)
        => IsNotInstanceOfType(value, typeof(T), string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The type that <paramref name="value"/> should not be.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is an instance of <paramref name="wrongType"/>. The message is shown
    /// in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, string? message)
        => IsNotInstanceOfType(value, wrongType, message, null);

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value, string? message)
        => IsNotInstanceOfType(value, typeof(T), message, null);

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The type that <paramref name="value"/> should not be.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is an instance of <paramref name="wrongType"/>. The message is shown
    /// in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (wrongType == null)
        {
            ThrowAssertFailed("Assert.IsNotInstanceOfType", BuildUserMessage(message, parameters));
        }

        // Null is not an instance of any type.
        if (value == null)
        {
            return;
        }

        TypeInfo elementTypeInfo = value.GetType().GetTypeInfo();
        TypeInfo expectedTypeInfo = wrongType.GetTypeInfo();
        if (expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsNotInstanceOfFailMsg,
                userMessage,
                wrongType.ToString(),
                value.GetType().ToString());
            ThrowAssertFailed("Assert.IsNotInstanceOfType", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => IsNotInstanceOfType(value, typeof(T), message, parameters);
}
