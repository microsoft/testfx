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
        public static void IsTrue(bool condition)
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
        public static void IsTrue(bool condition, string message)
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
        public static void IsTrue(bool condition, string message, params object[] parameters)
        {
            if (!condition)
            {
                HandleFail("Assert.IsTrue", message, parameters);
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
        public static void IsFalse(bool condition)
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
        public static void IsFalse(bool condition, string message)
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
        public static void IsFalse(bool condition, string message, params object[] parameters)
        {
            if (condition)
            {
                HandleFail("Assert.IsFalse", message, parameters);
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
                HandleFail("Assert.IsNull", message, parameters);
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
        public static void IsNotNull(object value)
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
        public static void IsNotNull(object value, string message)
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
        public static void IsNotNull(object value, string message, params object[] parameters)
        {
            if (value == null)
            {
                HandleFail("Assert.IsNotNull", message, parameters);
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
                string finalMessage = message;

                ValueType valExpected = expected as ValueType;
                if (valExpected != null)
                {
                    ValueType valActual = actual as ValueType;
                    if (valActual != null)
                    {
                        finalMessage = string.Format(
                            CultureInfo.CurrentCulture,
                            FrameworkMessages.AreSameGivenValues,
                            message == null ? string.Empty : ReplaceNulls(message));
                    }
                }

                HandleFail("Assert.AreSame", finalMessage, parameters);
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
                HandleFail("Assert.AreNotSame", message, parameters);
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
                string finalMessage;
                if (actual != null && expected != null && !actual.GetType().Equals(expected.GetType()))
                {
                    // This is for cases like: Assert.AreEqual(42L, 42) -> Expected: <42>, Actual: <42>
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.AreEqualDifferentTypesFailMsg,
                        message == null ? string.Empty : ReplaceNulls(message),
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
                        message == null ? string.Empty : ReplaceNulls(message),
                        ReplaceNulls(expected),
                        ReplaceNulls(actual));
                }

                HandleFail("Assert.AreEqual", finalMessage, parameters);
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
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreNotEqualFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    ReplaceNulls(notExpected),
                    ReplaceNulls(actual));
                HandleFail("Assert.AreNotEqual", finalMessage, parameters);
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
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreEqual", finalMessage, parameters);
            }

            if (Math.Abs(expected - actual) > delta)
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreEqual", finalMessage, parameters);
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
                var finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreNotEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreNotEqual", finalMessage, parameters);
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
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreEqual", finalMessage, parameters);
            }

            if (Math.Abs(expected - actual) > delta)
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreEqual", finalMessage, parameters);
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
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreNotEqualDeltaFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                    delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
                HandleFail("Assert.AreNotEqual", finalMessage, parameters);
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
                string finalMessage;

                // Comparison failed. Check if it was a case-only failure.
                if (!ignoreCase &&
                    CompareInternal(expected, actual, ignoreCase, culture) == 0)
                {
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.AreEqualCaseFailMsg,
                        message == null ? string.Empty : ReplaceNulls(message),
                        ReplaceNulls(expected),
                        ReplaceNulls(actual));
                }
                else
                {
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.AreEqualFailMsg,
                        message == null ? string.Empty : ReplaceNulls(message),
                        ReplaceNulls(expected),
                        ReplaceNulls(actual));
                }

                HandleFail("Assert.AreEqual", finalMessage, parameters);
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
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreNotEqualFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    ReplaceNulls(notExpected),
                    ReplaceNulls(actual));
                HandleFail("Assert.AreNotEqual", finalMessage, parameters);
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
                HandleFail("Assert.IsInstanceOfType", message, parameters);
            }

            var elementTypeInfo = value.GetType().GetTypeInfo();
            var expectedTypeInfo = expectedType.GetTypeInfo();
            if (!expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.IsInstanceOfFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    expectedType.ToString(),
                    value.GetType().ToString());
                HandleFail("Assert.IsInstanceOfType", finalMessage, parameters);
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
            if (wrongType == null || value == null)
            {
                HandleFail("Assert.IsNotInstanceOfType", message, parameters);
            }

            var elementTypeInfo = value.GetType().GetTypeInfo();
            var expectedTypeInfo = wrongType.GetTypeInfo();
            if (expectedTypeInfo.IsAssignableFrom(elementTypeInfo))
            {
                string finalMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.IsNotInstanceOfFailMsg,
                    message == null ? string.Empty : ReplaceNulls(message),
                    wrongType.ToString(),
                    value.GetType().ToString());
                HandleFail("Assert.IsNotInstanceOfType", finalMessage, parameters);
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
        public static void Fail(string message, params object[] parameters)
        {
            HandleFail("Assert.Fail", message, parameters);
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
            string finalMessage = string.Empty;
            if (!string.IsNullOrEmpty(message))
            {
                if (parameters == null)
                {
                    finalMessage = ReplaceNulls(message);
                }
                else
                {
                    finalMessage = string.Format(CultureInfo.CurrentCulture, ReplaceNulls(message), parameters);
                }
            }

            throw new AssertInconclusiveException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, "Assert.Inconclusive", finalMessage));
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
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static T ThrowsException<T>(Action action)
            where T : Exception
        {
            return ThrowsException<T>(action, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static T ThrowsException<T>(Action action, string message)
            where T : Exception
        {
            return ThrowsException<T>(action, message, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static T ThrowsException<T>(Func<object> action)
            where T : Exception
        {
            return ThrowsException<T>(action, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static T ThrowsException<T>(Func<object> action, string message)
            where T : Exception
        {
            return ThrowsException<T>(action, message, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static T ThrowsException<T>(Func<object> action, string message, params object[] parameters)
            where T : Exception
        {
            return ThrowsException<T>(() => { action(); }, message, parameters);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public static T ThrowsException<T>(Action action, string message, params object[] parameters)
            where T : Exception
        {
            var finalMessage = string.Empty;

            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (!typeof(T).Equals(ex.GetType()))
                {
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.WrongExceptionThrown,
                        ReplaceNulls(message),
                        typeof(T).Name,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                    HandleFail("Assert.ThrowsException", finalMessage, parameters);
                }

                return (T)ex;
            }

            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.NoExceptionThrown,
                ReplaceNulls(message),
                typeof(T).Name);
            HandleFail("Assert.ThrowsException", finalMessage, parameters);

            // This will not hit, but need it for compiler.
            return null;
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws
        /// <code>
        /// AssertFailedException
        /// </code>
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
        public static async Task<T> ThrowsExceptionAsync<T>(Func<Task> action)
            where T : Exception
        {
            return await ThrowsExceptionAsync<T>(action, string.Empty, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <code>AssertFailedException</code> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
            return await ThrowsExceptionAsync<T>(action, message, null);
        }

        /// <summary>
        /// Tests whether the code specified by delegate <paramref name="action"/> throws exact given exception of type <typeparamref name="T"/> (and not of derived type)
        /// and throws <code>AssertFailedException</code> if code does not throws exception or throws exception of type other than <typeparamref name="T"/>.
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
            var finalMessage = string.Empty;

            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                if (!typeof(T).Equals(ex.GetType()))
                {
                    finalMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.WrongExceptionThrown,
                        ReplaceNulls(message),
                        typeof(T).Name,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace);
                    HandleFail("Assert.ThrowsException", finalMessage, parameters);
                }

                return (T)ex;
            }

            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.NoExceptionThrown,
                ReplaceNulls(message),
                typeof(T).Name);
            HandleFail("Assert.ThrowsException", finalMessage, parameters);

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
        /// message describing conditions for assertion failure
        /// </param>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        internal static void HandleFail(string assertionName, string message, params object[] parameters)
        {
            string finalMessage = string.Empty;
            if (!string.IsNullOrEmpty(message))
            {
                if (parameters == null)
                {
                    finalMessage = ReplaceNulls(message);
                }
                else
                {
                    finalMessage = string.Format(CultureInfo.CurrentCulture, ReplaceNulls(message), parameters);
                }
            }

            throw new AssertFailedException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, finalMessage));
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
        internal static void CheckParameterNotNull(object param, string assertionName, string parameterName, string message, params object[] parameters)
        {
            if (param == null)
            {
                HandleFail(assertionName, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName, message), parameters);
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
