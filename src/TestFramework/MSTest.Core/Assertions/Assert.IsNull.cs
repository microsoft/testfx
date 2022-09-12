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
#endif

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
    public static void IsNull(object value,
        [CallerArgumentExpression("value")] string message = null)
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
    public static void IsNull(object value,
        [CallerArgumentExpression("value")] string message = null, params object[] parameters)
    {
        if (value != null)
        {
            ThrowAssertFailed("Assert.IsNull", BuildUserMessage(message, parameters));
        }
    }

#if HIDE_MESSAGELESS_IMPLEMENTATION
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
#endif

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
    public static void IsNotNull([NotNull] object value,
        [CallerArgumentExpression("value")] string message = null)
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
    public static void IsNotNull([NotNull] object value,
        [CallerArgumentExpression("value")] string message = null, params object[] parameters)
    {
        if (value == null)
        {
            ThrowAssertFailed("Assert.IsNotNull", BuildUserMessage(message, parameters));
        }
    }
}
