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
    /// Throws an AssertInconclusiveException.
    /// </summary>
    /// <exception cref="AssertInconclusiveException">
    /// Always thrown.
    /// </exception>
    [DoesNotReturn]
    public static void Inconclusive()
        => Inconclusive(string.Empty, null);

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
    [DoesNotReturn]
    public static void Inconclusive(string? message)
        => Inconclusive(message, null);

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
    [DoesNotReturn]
    public static void Inconclusive([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        string userMessage = BuildUserMessage(message, parameters);
        throw new AssertInconclusiveException(
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, "Assert.Inconclusive", userMessage));
    }
}
