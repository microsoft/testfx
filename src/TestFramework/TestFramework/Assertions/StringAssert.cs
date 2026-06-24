// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The string assert.
/// </summary>
[StackTraceHidden]
public sealed partial class StringAssert
{
    private StringAssert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the StringAssert functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be <c>public static void ContainsWords(this StringAssert customAssert, string value, ICollection substrings)</c>
    /// and the call-site would be <c>StringAssert.That.ContainsWords(value, substrings);</c>.
    /// </para>
    /// <para>
    /// For new custom assertions, prefer extending <see cref="Assert.That"/> instead, because <see cref="StringAssert"/> is likely to be deprecated in a future release.
    /// For more information, see <see href="https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-assertions#extension-hooks-on-stringassert-and-collectionassert">Extension hooks on StringAssert and CollectionAssert</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example defines a custom <c>ContainsWords</c> assertion as an extension method
    /// on <see cref="StringAssert"/> and invokes it through <c>StringAssert.That</c>:
    /// <code language="csharp">
    /// using System.Collections.Generic;
    /// using Microsoft.VisualStudio.TestTools.UnitTesting;
    ///
    /// public static class CustomStringAssertExtensions
    /// {
    ///     public static void ContainsWords(this StringAssert stringAssert, string value, IEnumerable&lt;string&gt; words)
    ///     {
    ///         foreach (string word in words)
    ///         {
    ///             if (value == null || !value.Contains(word))
    ///             {
    ///                 throw new AssertFailedException($"StringAssert.That.ContainsWords failed. Word &lt;{word}&gt; not found in &lt;{value}&gt;.");
    ///             }
    ///         }
    ///     }
    /// }
    ///
    /// [TestClass]
    /// public class MessageTests
    /// {
    ///     [TestMethod]
    ///     public void Greeting_ContainsExpectedWords()
    ///     {
    ///         StringAssert.That.ContainsWords("Hello, world!", new[] { "Hello", "world" });
    ///     }
    /// }
    /// </code>
    /// </example>
    public static StringAssert That { get; } = new();

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for equality.
    /// This method should <b>not</b> be used for comparison of two instances for equality.
    /// Please use StringAssert methods or Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: false,
        DiagnosticId = "MSTEST0102",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: true,
        DiagnosticId = "MSTEST0102",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool Equals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseStringAssertEquals);
        return false;
    }

    /// <summary>
    /// Static ReferenceEquals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// reference equality. Please use StringAssert methods or Assert.AreSame and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: false,
        DiagnosticId = "MSTEST0103",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: true,
        DiagnosticId = "MSTEST0103",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool ReferenceEquals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseStringAssertReferenceEquals);
        return false;
    }
}
