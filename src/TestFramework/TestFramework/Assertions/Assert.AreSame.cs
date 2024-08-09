// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public static partial class Assert
{
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual)
        => AreSame(expected, actual, string.Empty, null);

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual, string? message)
        => AreSame(expected, actual, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> does not refer to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreSame<T>(T? expected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (ReferenceEquals(expected, actual))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        string finalMessage = userMessage;

        if (expected is ValueType)
        {
            if (actual is ValueType)
            {
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreSameGivenValues,
                    userMessage);
            }
        }

        ThrowAssertFailed("Assert.AreSame", finalMessage);
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual)
        => AreNotSame(notExpected, actual, string.Empty, null);

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual, string? message)
        => AreNotSame(notExpected, actual, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> refers to the same object
    /// as <paramref name="actual"/>.
    /// </exception>
    public static void AreNotSame<T>(T? notExpected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (ReferenceEquals(notExpected, actual))
        {
            ThrowAssertFailed("Assert.AreNotSame", BuildUserMessage(message, parameters));
        }
    }
}
