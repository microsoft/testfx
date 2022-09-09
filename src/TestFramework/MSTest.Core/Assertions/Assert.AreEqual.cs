// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP3_0_OR_GREATER && !NET6_0_OR_GREATER
#define HIDE_MESSAGELESS_IMPLEMENTATION
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T expected, T actual)
    {
        AreEqual(expected, actual, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T expected, T actual,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, message, null);
    }

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T expected, T actual,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (!object.Equals(expected, actual))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage;
            if (actual != null && expected != null && !actual.GetType().Equals(expected.GetType()))
            {
                // This is for cases like: Assert.AreEqual(42L, 42) -> Expected: <42>, Actual: <42>
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualDifferentTypesFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    expected.GetType().FullName,
                    ReplaceNulls(actual),
                    actual.GetType().FullName);
            }
            else
            {
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    ReplaceNulls(actual));
            }

            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T notExpected, T actual)
    {
        AreNotEqual(notExpected, actual, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T notExpected, T actual,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, message, null);
    }

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T notExpected, T actual,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (object.Equals(notExpected, actual))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualFailMsg,
                userMessage,
                ReplaceNulls(notExpected),
                ReplaceNulls(actual));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified objects are equal and throws an exception
    /// if the two objects are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="expected">
    /// The first object to compare. This is the object the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(object expected, object actual)
    {
        AreEqual(expected, actual, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified objects are equal and throws an exception
    /// if the two objects are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="expected">
    /// The first object to compare. This is the object the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(object expected, object actual,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, message, null);
    }

    /// <summary>
    /// Tests whether the specified objects are equal and throws an exception
    /// if the two objects are not equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="expected">
    /// The first object to compare. This is the object the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(object expected, object actual,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        AreEqual<object>(expected, actual, message, parameters);
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified objects are unequal and throws an exception
    /// if the two objects are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(object notExpected, object actual)
    {
        AreNotEqual(notExpected, actual, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified objects are unequal and throws an exception
    /// if the two objects are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(object notExpected, object actual,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, message, null);
    }

    /// <summary>
    /// Tests whether the specified objects are unequal and throws an exception
    /// if the two objects are equal. Different numeric types are treated
    /// as unequal even if the logical values are equal. 42L is not equal to 42.
    /// </summary>
    /// <param name="notExpected">
    /// The first object to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second object to compare. This is the object produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(object notExpected, object actual,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        AreNotEqual<object>(notExpected, actual, message, parameters);
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta)
    {
        AreEqual(expected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (float.IsNaN(expected) || float.IsNaN(actual) || float.IsNaN(delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }

        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta)
    {
        AreNotEqual(notExpected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            var finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta)
    {
        AreEqual(expected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta)
    {
        AreNotEqual(notExpected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            var finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta)
    {
        AreEqual(expected, actual, delta, string.Empty, null);
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, delta, message, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta,
        [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta)
    {
        AreNotEqual(notExpected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta,
        [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            var finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta)
    {
        AreEqual(expected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (double.IsNaN(expected) || double.IsNaN(actual) || double.IsNaN(delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }

        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta)
    {
        AreNotEqual(notExpected, actual, delta, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, delta, message, null);
    }

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase)
    {
        AreEqual(expected, actual, ignoreCase, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase, [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, ignoreCase, message, null);
    }

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture)
    {
        AreEqual(expected, actual, ignoreCase, culture, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture, [CallerArgumentExpression("actual")] string message = null)
    {
        AreEqual(expected, actual, ignoreCase, culture, message, null);
    }

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", "culture", string.Empty);
        if (CompareInternal(expected, actual, ignoreCase, culture) != 0)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage;

            // Comparison failed. Check if it was a case-only failure.
            if (!ignoreCase &&
                CompareInternal(expected, actual, ignoreCase, culture) == 0)
            {
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualCaseFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    ReplaceNulls(actual));
            }
            else
            {
                finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    ReplaceNulls(actual));
            }

            ThrowAssertFailed("Assert.AreEqual", finalMessage);
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase)
    {
        AreNotEqual(notExpected, actual, ignoreCase, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, ignoreCase, message, null);
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture)
    {
        AreNotEqual(notExpected, actual, ignoreCase, culture, string.Empty, null);
    }
#endif

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture, [CallerArgumentExpression("actual")] string message = null)
    {
        AreNotEqual(notExpected, actual, ignoreCase, culture, message, null);
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture, [CallerArgumentExpression("actual")] string message = null, params object[] parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", "culture", string.Empty);
        if (CompareInternal(notExpected, actual, ignoreCase, culture) == 0)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualFailMsg,
                userMessage,
                ReplaceNulls(notExpected),
                ReplaceNulls(actual));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }
}
