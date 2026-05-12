// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions associated
/// with collections within unit tests. If the condition being tested is not
/// met, an exception is thrown.
/// </summary>
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
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be "public static void AreEqualUnordered(this CollectionAssert customAssert, ICollection expected, ICollection actual)"
    /// Users could then use a syntax similar to the default assertions which in this case is "CollectionAssert.That.AreEqualUnordered(list1, list2);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
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
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertEquals,
#if DEBUG
        error: false)]
#else
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
    [Obsolete(
        FrameworkConstants.DoNotUseCollectionAssertReferenceEquals,
#if DEBUG
        error: false)]
#else
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
