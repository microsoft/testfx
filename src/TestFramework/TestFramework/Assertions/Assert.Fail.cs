// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
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
    public static void Fail(string message = "")
        => ThrowAssertFailed("Assert.Fail", BuildUserMessage(message));
}
