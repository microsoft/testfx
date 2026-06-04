// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions associated
/// with collections within unit tests. If the condition being tested is not
/// met, an exception is thrown.
/// </summary>
[StackTraceHidden]
public sealed partial class CollectionAssert
{
    #region Singleton constructor

    private CollectionAssert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the CollectionAssert functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be <c>public static void AreEqualUnordered(this CollectionAssert customAssert, ICollection expected, ICollection actual)</c>
    /// and the call-site would be <c>CollectionAssert.That.AreEqualUnordered(list1, list2);</c>.
    /// </para>
    /// <para>
    /// For new custom assertions, prefer extending <see cref="Assert.That"/> instead, because <see cref="CollectionAssert"/> is likely to be deprecated in a future release.
    /// For more information, see <see href="https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-assertions#extension-hooks-on-stringassert-and-collectionassert">Extension hooks on StringAssert and CollectionAssert</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example defines a custom <c>AreEqualUnordered</c> assertion as an extension method
    /// on <see cref="CollectionAssert"/> and invokes it through <c>CollectionAssert.That</c>:
    /// <code language="csharp">
    /// using System.Collections.Generic;
    /// using System.Linq;
    /// using Microsoft.VisualStudio.TestTools.UnitTesting;
    ///
    /// public static class CustomCollectionAssertExtensions
    /// {
    ///     public static void AreEqualUnordered&lt;T&gt;(this CollectionAssert collectionAssert, IEnumerable&lt;T&gt; expected, IEnumerable&lt;T&gt; actual)
    ///     {
    ///         if (!expected.OrderBy(x =&gt; x).SequenceEqual(actual.OrderBy(x =&gt; x)))
    ///         {
    ///             throw new AssertFailedException("CollectionAssert.That.AreEqualUnordered failed. Collections do not contain the same elements.");
    ///         }
    ///     }
    /// }
    ///
    /// [TestClass]
    /// public class SetTests
    /// {
    ///     [TestMethod]
    ///     public void Items_MatchRegardlessOfOrder()
    ///     {
    ///         CollectionAssert.That.AreEqualUnordered(new[] { 1, 2, 3 }, new[] { 3, 1, 2 });
    ///     }
    /// }
    /// </code>
    /// </example>
    public static CollectionAssert That { get; } = new();

    #endregion

    #region DoNotUse

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for equality.
    /// This method should <b>not</b> be used for comparison of two instances for equality.
    /// Please use CollectionAssert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertEquals,
        error: false,
        DiagnosticId = "MSTEST0104",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertEquals,
        error: true,
        DiagnosticId = "MSTEST0104",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool Equals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseCollectionAssertEquals);
        return false;
    }

    /// <summary>
    /// Static ReferenceEquals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// reference equality. Please use CollectionAssert methods or Assert.AreSame and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertReferenceEquals,
        error: false,
        DiagnosticId = "MSTEST0105",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertReferenceEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertReferenceEquals,
        error: true,
        DiagnosticId = "MSTEST0105",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertReferenceEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool ReferenceEquals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseCollectionAssertReferenceEquals);
        return false;
    }

    #endregion
}
