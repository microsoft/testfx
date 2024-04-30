// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Extensibility.Samples;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This is only some sample type")]
public static class AssertEx
{
    private static AssertIs s_assertIs;

    /// <summary>
    /// A simple assert extension to validate if an object is of a given type.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <param name="assert">Assert class.</param>
    /// <param name="obj">The object.</param>
    /// <returns>True if object is of the given type.</returns>
    /// <exception cref="AssertFailedException">If object is not of the given type.</exception>
#pragma warning disable IDE0060 // Remove unused parameter
    public static bool IsOfType<T>(this Assert assert, object obj) => obj is T
            ? true
            : throw new AssertFailedException(string.Format(
                CultureInfo.InvariantCulture,
                "Expected object of type {0} but found object of type {1}",
                typeof(T),
                obj ?? obj.GetType()));

    /// <summary>
    /// A chain/grouping of assert statements.
    /// </summary>
    /// <param name="assert">The Assert class.</param>
    /// <returns>The class that contains the assert methods for this grouping.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static AssertIs Is(this Assert assert)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        s_assertIs ??= new AssertIs();

        return s_assertIs;
    }
}
