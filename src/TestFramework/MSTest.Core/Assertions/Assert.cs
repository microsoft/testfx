// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// A collection of helper classes to test various conditions within
    /// unit tests. If the condition being tested is not met, an exception
    /// is thrown.
    /// </summary>
    public sealed class Assert
    {
        private static Assert that;

        #region Singleton constructor

        private Assert()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Assert functionality.
        /// </summary>
        /// <remarks>
        /// Users can use this to plug-in custom assertions through C# extension methods.
        /// For instance, the signature of a custom assertion provider could be "public static void IsOfType&lt;T&gt;(this Assert assert, object obj)"
        /// Users could then use a syntax similar to the default assertions which in this case is "Assert.That.IsOfType&lt;Dog&gt;(animal);"
        /// More documentation is at "https://github.com/Microsoft/testfx-docs".
        /// </remarks>
        public static Assert That
        {
            get
            {
                if (that == null)
                {
                    that = new Assert();
                }

                return that;
            }
        }

        #endregion

        #region Boolean

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool condition)
        {
            IsTrue(condition, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool? condition)
        {
            IsTrue(condition, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is false. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool condition, string message)
        {
            IsTrue(condition, message, null);
        }

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is false. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string message)
        {
            IsTrue(condition, message, null);
        }

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is false. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool condition, string message, params object[] parameters)
        {
            if (!condition)
            {
                ThrowAssertFailed("Assert.IsTrue", BuildUserMessage(message, parameters));
            }
        }

        /// <summary>
        /// Tests whether the specified condition is true and throws an exception
        /// if the condition is false.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be true.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is false. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is false.
        /// </exception>
        public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string message, params object[] parameters)
        {
            if (condition == false || condition == null)
            {
                ThrowAssertFailed("Assert.IsTrue", BuildUserMessage(message, parameters));
            }
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool condition)
        {
            IsFalse(condition, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool? condition)
        {
            IsFalse(condition, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is true. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool condition, string message)
        {
            IsFalse(condition, message, null);
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is true. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string message)
        {
            IsFalse(condition, message, null);
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is true. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool condition, string message, params object[] parameters)
        {
            if (condition)
            {
                ThrowAssertFailed("Assert.IsFalse", BuildUserMessage(message, parameters));
            }
        }

        /// <summary>
        /// Tests whether the specified condition is false and throws an exception
        /// if the condition is true.
        /// </summary>
        /// <param name="condition">
        /// The condition the test expects to be false.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="condition"/>
        /// is true. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="condition"/> is true.
        /// </exception>
        public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string message, params object[] parameters)
        {
            if (condition == true || condition == null)
            {
                ThrowAssertFailed("Assert.IsFalse", BuildUserMessage(message, parameters));
            }
        }

        #endregion

        #region Null

        /// <summary>
        /// Tests whether the specified object is null and throws an exception
        /// if it is not.
        /// </summary>
        /// <param name="value">
        /// The object the test expects to be null.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is not null.
        /// </exception>
        public static void IsNull(object value)
        {
            IsNull(value, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified object is null and throws an exception
        /// if it is not.
        /// </summary>
        /// <param name="value">
        /// The object the test expects to be null.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// is not null. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is not null.
        /// </exception>
        public static void IsNull(object value, string message)
        {
            IsNull(value, message, null);
        }

        /// <summary>
        /// Tests whether the specified object is null and throws an exception
        /// if it is not.
        /// </summary>
        /// <param name="value">
        /// The object the test expects to be null.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// is not null. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is not null.
        /// </exception>
        public static void IsNull(object value, string message, params object[] parameters)
        {
            if (value != null)
            {
                ThrowAssertFailed("Assert.IsNull", BuildUserMessage(message, parameters));
            }
        }

        /// <summary>
        /// Tests whether the specified object is non-null and throws an exception
        /// if it is null.
        /// </summary>
        /// <param name="value">
        /// The object the test expects not to be null.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is null.
        /// </exception>
        public static void IsNotNull([NotNull] object value)
        {
            IsNotNull(value, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified object is non-null and throws an exception
        /// if it is null.
        /// </summary>
        /// <param name="value">
        /// The object the test expects not to be null.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// is null. The message is shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is null.
        /// </exception>
        public static void IsNotNull([NotNull] object value, string message)
        {
            IsNotNull(value, message, null);
        }

        /// <summary>
        /// Tests whether the specified object is non-null and throws an exception
        /// if it is null.
        /// </summary>
        /// <param name="value">
        /// The object the test expects not to be null.
        /// </param>
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// is null. The message is shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> is null.
        /// </exception>
        public static void IsNotNull([NotNull] object value, string message, params object[] parameters)
        {
            if (value == null)
            {
                ThrowAssertFailed("Assert.IsNotNull", BuildUserMessage(message, parameters));
            }
        }

        #endregion

        #region AreSame

        /// <summary>
        /// Tests whether the specified objects both refer to the same object and
        /// throws an exception if the two inputs do not refer to the same object.
        /// </summary>
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
        public static void AreSame(object expected, object actual)
        {
            AreSame(expected, actual, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified objects both refer to the same object and
        /// throws an exception if the two inputs do not refer to the same object.
        /// </summary>
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
        public static void AreSame(object expected, object actual, string message)
        {
            AreSame(expected, actual, message, null);
        }

        /// <summary>
        /// Tests whether the specified objects both refer to the same object and
        /// throws an exception if the two inputs do not refer to the same object.
        /// </summary>
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
        public static void AreSame(object expected, object actual, string message, params object[] parameters)
        {
            if (!ReferenceEquals(expected, actual))
            {
                string userMessage = BuildUserMessage(message, parameters);
                string finalMessage = userMessage;

                if (expected is ValueType valExpected)
                {
                    if (actual is ValueType valActual)
                    {
                        finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.AreSameGivenValues,
                            userMessage);
                    }
                }

                ThrowAssertFailed("Assert.AreSame", finalMessage);
            }
        }

        /// <summary>
        /// Tests whether the specified objects refer to different objects and
        /// throws an exception if the two inputs refer to the same object.
        /// </summary>
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
        public static void AreNotSame(object notExpected, object actual)
        {
            AreNotSame(notExpected, actual, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the specified objects refer to different objects and
        /// throws an exception if the two inputs refer to the same object.
        /// </summary>
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
        public static void AreNotSame(object notExpected, object actual, string message)
        {
            AreNotSame(notExpected, actual, message, null);
        }

        /// <summary>
        /// Tests whether the specified objects refer to different objects and
        /// throws an exception if the two inputs refer to the same object.
        /// </summary>
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
        public static void AreNotSame(object notExpected, object actual, string message, params object[] parameters)
        {
            if (ReferenceEquals(notExpected, actual))
            {
                ThrowAssertFailed("Assert.AreNotSame", BuildUserMessage(message, parameters));
            }
        }

        #endregion

        #region AreEqual

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
        public static void AreEqual<T>(T expected, T actual, string message)
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
        public static void AreEqual<T>(T expected, T actual, string message, params object[] parameters)
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
        public static void AreNotEqual<T>(T notExpected, T actual, string message)
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
        public static void AreNotEqual<T>(T notExpected, T actual, string message, params object[] parameters)
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
        public static void AreEqual(object expected, object actual, string message)
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
        public static void AreEqual(object expected, object actual, string message, params object[] parameters)
        {
            AreEqual<object>(expected, actual, message, parameters);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(object notExpected, object actual)
        {
            AreNotEqual(notExpected, actual, string.Empty, null);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(object notExpected, object actual, string message)
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
        public static void AreNotEqual(object notExpected, object actual, string message, params object[] parameters)
        {
            AreNotEqual<object>(notExpected, actual, message, parameters);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to
        /// <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(float expected, float actual, float delta)
        {
            AreEqual(expected, actual, delta, string.Empty, null);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to
        /// <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(float expected, float actual, float delta, string message)
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
        public static void AreEqual(float expected, float actual, float delta, string message, params object[] parameters)
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
        public static void AreNotEqual(float notExpected, float actual, float delta, string message)
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
        public static void AreNotEqual(float notExpected, float actual, float delta, string message, params object[] parameters)
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
        public static void AreEqual(decimal expected, decimal actual, decimal delta, string message)
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
        public static void AreEqual(decimal expected, decimal actual, decimal delta, string message, params object[] parameters)
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
        public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, string message)
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
        public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, string message, params object[] parameters)
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
        public static void AreEqual(long expected, long actual, long delta, string message)
        {
            AreEqual(expected, actual, delta, message, null);
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
        public static void AreEqual(long expected, long actual, long delta, string message, params object[] parameters)
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
        public static void AreNotEqual(long notExpected, long actual, long delta, string message)
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
        public static void AreNotEqual(long notExpected, long actual, long delta, string message, params object[] parameters)
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
        public static void AreEqual(double expected, double actual, double delta, string message)
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
        public static void AreEqual(double expected, double actual, double delta, string message, params object[] parameters)
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
        public static void AreNotEqual(double notExpected, double actual, double delta, string message)
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
        public static void AreNotEqual(double notExpected, double actual, double delta, string message, params object[] parameters)
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
            Assert.AreEqual(expected, actual, ignoreCase, string.Empty, null);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(string expected, string actual, bool ignoreCase, string message)
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
        public static void AreEqual(string expected, string actual, bool ignoreCase, string message, params object[] parameters)
        {
            AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture)
        {
            AreEqual(expected, actual, ignoreCase, culture, string.Empty, null);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture, string message)
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
        public static void AreEqual(string expected, string actual, bool ignoreCase, CultureInfo culture, string message, params object[] parameters)
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
        public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, string message)
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
        public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, string message, params object[] parameters)
        {
            AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture)
        {
            AreNotEqual(notExpected, actual, ignoreCase, culture, string.Empty, null);
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
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
        /// </exception>
        public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture, string message)
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
        public static void AreNotEqual(string notExpected, string actual, bool ignoreCase, CultureInfo culture, string message, params object[] parameters)
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

        #endregion

        #region Type

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
        public static void IsInstanceOfType(object value, Type expectedType)
        {
            IsInstanceOfType(value, expectedType, string.Empty, null);
        }

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
        public static void IsInstanceOfType(object value, Type expectedType, string message)
        {
            IsInstanceOfType(value, expectedType, message, null);
        }

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
        public static void IsInstanceOfType(object value, Type expectedType, string message, params object[] parameters)
        {
            if (expectedType == null || value == null)
            {
                ThrowAssertFailed("Assert.IsInstanceOfType", BuildUserMessage(message, parameters));
            }

            var elementTypeInfo = value.GetType().GetTypeInfo();
            var expectedTypeInfo = expectedType.GetTypeInfo();
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
        public static void IsNotInstanceOfType(object value, Type wrongType)
        {
            IsNotInstanceOfType(value, wrongType, string.Empty, null);
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
        public static void IsNotInstanceOfType(object value, Type wrongType, string message)
        {
            IsNotInstanceOfType(value, wrongType, message, null);
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
        public static void IsNotInstanceOfType(object value, Type wrongType, string message, params object[] parameters)
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

            var elementTypeInfo = value.GetType().GetTypeInfo();
            var expectedTypeInfo = wrongType.GetTypeInfo();
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

        #endregion

        #region Fail

        /// <summary>
        /// Throws an AssertFailedException.
        /// </summary>
        /// <exception cref="AssertFailedException">
        /// Always thrown.
        /// </exception>
        [DoesNotReturn]
        public static void Fail()
        {
            Fail(string.Empty, null);
        }

        /// <summary>
        /// Throws an AssertFailedException.
        /// </summary>
        /// <param name="message">
        /// The message to include in the exception. The message is shown in
        /// test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Always thrown.
        /// </exception>
        [DoesNotReturn]
        public static void Fail(string message)
        {
            Fail(message, null);
        }

        /// <summary>
        /// Throws an AssertFailedException.
        /// </summary>
        /// <param name="message">
        /// The message to include in the exception. The message is shown in
        /// test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Always thrown.
        /// </exception>
        [DoesNotReturn]
        public static void Fail(string message, params object[] parameters)
        {
            ThrowAssertFailed("Assert.Fail", BuildUserMessage(message, parameters));
        }

        #endregion

        #region Inconclusive

        /// <summary>
        /// Throws an AssertInconclusiveException.
        /// </summary>
        /// <exception cref="AssertInconclusiveException">
        /// Always thrown.
        /// </exception>
        public static void Inconclusive()
        {
            Inconclusive(string.Empty, null);
        }

        /// <summary>
        /// Throws an AssertInconclusiveException.
        /// </summary>
        /// <param name="message">
        /// The message to include in the exception. The message is shown in
        /// test results.
        /// </param>
        /// <exception cref="AssertInconclusiveException">
        /// Always thrown.
        /// </exception>
        public static void Inconclusive(string message)
        {
            Inconclusive(message, null);
        }

        /// <summary>
        /// Throws an AssertInconclusiveException.
        /// </summary>
        /// <param name="message">
        /// The message to include in the exception. The message is shown in
        /// test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertInconclusiveException">
        /// Always thrown.
        /// </exception>
        public static void Inconclusive(string message, params object[] parameters)
        {
            string userMessage = BuildUserMessage(message, parameters);
            throw new AssertInconclusiveException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, "Assert.Inconclusive", userMessage));
        }

        #endregion

        #region Equals Assertion

        /// <summary>
        /// Static equals overloads are used for comparing instances of two types for reference
        /// equality. This method should <b>not</b> be used for comparison of two instances for
        /// equality. This object will <b>always</b> throw with Assert.Fail. Please use
        /// Assert.AreEqual and associated overloads in your unit tests.
        /// </summary>
        /// <param name="objA"> Object A </param>
        /// <param name="objB"> Object B </param>
        /// <returns> False, always. </returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
        public static new bool Equals(object objA, object objB)
        {
            Fail(FrameworkMessages.DoNotUseAssertEquals);
            return false;
        }

        #endregion Equals Assertion

        #region ThrowsException

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static T ThrowsException<T>(Action action)
            where T : Exception
        {
            return ThrowsException<T>(action, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static T ThrowsException<T>(Action action, string message)
            where T : Exception
        {
            return ThrowsException<T>(action, message, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static T ThrowsException<T>(Func<object> action)
            where T : Exception
        {
            return ThrowsException<T>(action, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static T ThrowsException<T>(Func<object> action, string message)
            where T : Exception
        {
            return ThrowsException<T>(action, message, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static T ThrowsException<T>(Func<object> action, string message, params object[] parameters)
            where T : Exception
        {
            return ThrowsException<T>(() => { action(); }, message, parameters);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and format appropriately.")]
        public static T ThrowsException<T>(Action action, string message, params object[] parameters)
            where T : Exception
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            string userMessage, finalMessage;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (!typeof(T).Equals(ex.GetType()))
                {
                    userMessage = BuildUserMessage(message, parameters);
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.WrongExceptionThrown,
                        userMessage,
                        typeof(T).Name,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                    ThrowAssertFailed("Assert.ThrowsException", finalMessage);
                }

                return (T)ex;
            }

            userMessage = BuildUserMessage(message, parameters);
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.NoExceptionThrown,
                userMessage,
                typeof(T).Name);
            ThrowAssertFailed("Assert.ThrowsException", finalMessage);

            // This will not hit, but need it for compiler.
            return null;
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action)
            where T : Exception
        {
            return await ThrowsExceptionAsync<T>(action, string.Empty, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action, string message)
            where T : Exception
        {
            return await ThrowsExceptionAsync<T>(action, message, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <c>AssertFailedException</c> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
        public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action, string message, params object[] parameters)
            where T : Exception
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            string userMessage, finalMessage;
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!typeof(T).Equals(ex.GetType()))
                {
                    userMessage = BuildUserMessage(message, parameters);
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.WrongExceptionThrown,
                        userMessage,
                        typeof(T).Name,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                    ThrowAssertFailed("Assert.ThrowsException", finalMessage);
                }

                return (T)ex;
            }

            userMessage = BuildUserMessage(message, parameters);
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.NoExceptionThrown,
                userMessage,
                typeof(T).Name);
            ThrowAssertFailed("Assert.ThrowsException", finalMessage);

            // This will not hit, but need it for compiler.
            return null;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Replaces null characters ('\0') with "\\0".
        /// </summary>
        /// <param name="input">
        /// The string to search.
        /// </param>
        /// <returns>
        /// The converted string with null characters replaced by "\\0".
        /// </returns>
        /// <remarks>
        /// This is only public and still present to preserve compatibility with the V1 framework.
        /// </remarks>
        public static string ReplaceNullChars(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.Replace("\0", "\\0");
        }

        /// <summary>
        /// Helper function that creates and throws an AssertionFailedException
        /// </summary>
        /// <param name="assertionName">
        /// name of the assertion throwing an exception
        /// </param>
        /// <param name="message">
        /// The assertion failure message
        /// </param>
        [DoesNotReturn]
        internal static void ThrowAssertFailed(string assertionName, string message)
        {
            throw new AssertFailedException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, ReplaceNulls(message)));
        }

        /// <summary>
        /// Builds the formatted message using the given user format message and parameters.
        /// </summary>
        /// <param name="format">
        /// A composite format string
        /// </param>
        /// <param name="parameters">
        /// An object array that contains zero or more objects to format.
        /// </param>
        /// <returns>
        /// The formatted string based on format and paramters.
        /// </returns>
        internal static string BuildUserMessage(string format, params object[] parameters)
        {
            if (format is null)
            {
                return ReplaceNulls(format);
            }

            if (format.Length == 0)
            {
                return string.Empty;
            }

            return parameters == null || parameters.Length == 0
                ? ReplaceNulls(format)
                : string.Format(CultureInfo.CurrentCulture, ReplaceNulls(format), parameters);
        }

        /// <summary>
        /// Checks the parameter for valid conditions
        /// </summary>
        /// <param name="param">
        /// The parameter.
        /// </param>
        /// <param name="assertionName">
        /// The assertion Name.
        /// </param>
        /// <param name="parameterName">
        /// parameter name
        /// </param>
        /// <param name="message">
        /// message for the invalid parameter exception
        /// </param>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        internal static void CheckParameterNotNull([NotNull] object param, string assertionName, string parameterName, string message, params object[] parameters)
        {
            if (param == null)
            {
                string userMessage = BuildUserMessage(message, parameters);
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName, userMessage);
                ThrowAssertFailed(assertionName, finalMessage);
            }
        }

        /// <summary>
        /// Safely converts an object to a string, handling null values and null characters.
        /// Null values are converted to "(null)". Null characters are converted to "\\0".
        /// </summary>
        /// <param name="input">
        /// The object to convert to a string.
        /// </param>
        /// <returns>
        /// The converted string.
        /// </returns>
        [SuppressMessage("ReSharper", "RedundantToStringCall", Justification = "We are ensuring ToString() isn't overloaded in a way to misbehave")]
        internal static string ReplaceNulls(object input)
        {
            // Use the localized "(null)" string for null values.
            if (input == null)
            {
                return FrameworkMessages.Common_NullInMessages.ToString();
            }
            else
            {
                // Convert it to a string.
                string inputString = input.ToString();

                // Make sure the class didn't override ToString and return null.
                if (inputString == null)
                {
                    return FrameworkMessages.Common_ObjectString.ToString();
                }

                return ReplaceNullChars(inputString);
            }
        }

        private static int CompareInternal(string expected, string actual, bool ignoreCase, CultureInfo culture)
        {
            return string.Compare(expected, actual, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        #endregion
    }
}
