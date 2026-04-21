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
    /// Creates a new assertion scope that collects assertion failures instead of throwing them immediately.
    /// When the returned scope is disposed, all collected failures are thrown as a single <see cref="AssertFailedException"/>.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> representing the assertion scope.</returns>
    /// <example>
    /// <code>
    /// using (Assert.Scope())
    /// {
    ///     Assert.AreEqual(1, 2);  // collected, not thrown
    ///     Assert.IsTrue(false);   // collected, not thrown
    /// }
    /// // AssertFailedException is thrown here with all collected failures.
    /// </code>
    /// </example>
    [Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
    public static IDisposable Scope() => new AssertScope();
}
