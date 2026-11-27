// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreAllDistinctEnumerableOverloadTests : TestContainer
{
    public void AreAllDistinct_GenericEnumerable_DetectsDuplicatesAndRendersPlaceholder()
    {
        IEnumerable<int> values = [1, 2, 1];
        Action compare = () => Assert.AreAllDistinct(values, collectionExpression: "multi-line\nexpression");

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Replace("\r\n", "\n").Should().Be(
            """
            Assertion failed. Expected all items in collection to be distinct.

            duplicates: [1]
            collection: [1, 2, 1]

            Assert.AreAllDistinct(<collection>)
            """.Replace("\r\n", "\n"));
        failure.ActualText.Should().Be("[1, 2, 1]");
        IEnumerable<int> distinctValues = [1, 2, 3];
        Action distinct = () => Assert.AreAllDistinct(distinctValues);
        distinct.Should().NotThrow();
    }

    public void AreAllDistinct_GenericEnumerableWithComparer_UsesComparerAndValidatesParameters()
    {
        IEnumerable<string> values = ["value", "VALUE"];
        Action duplicate = () => Assert.AreAllDistinct(values, StringComparer.OrdinalIgnoreCase);
        Action nullCollection = () => Assert.AreAllDistinct((IEnumerable<string>?)null, StringComparer.OrdinalIgnoreCase);
        Action nullComparer = () => Assert.AreAllDistinct(values, (IEqualityComparer<string>?)null);

        duplicate.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Replace("\r\n", "\n").Should().Be(
                $$"""
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: ["VALUE"]
                collection: ["value", "VALUE"]
                comparer:   {{StringComparer.OrdinalIgnoreCase.GetType().Name}}

                Assert.AreAllDistinct(values, <comparer>)
                """.Replace("\r\n", "\n"));
        nullCollection.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'collection' is invalid. The value cannot be null.");
        nullComparer.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'comparer' is invalid. The value cannot be null.");
        IEnumerable<string> distinctValues = ["value", "other"];
        Action distinct = () => Assert.AreAllDistinct(distinctValues, StringComparer.OrdinalIgnoreCase);
        distinct.Should().NotThrow();
    }

    public void AreAllDistinct_NonGenericEnumerableOverloadsDetectDuplicatesAndValidateParameters()
    {
        IEnumerable values = new ArrayList { 1, 2, 1 };
        IEnumerable strings = new ArrayList { "value", "VALUE" };
        IEnumerable? nullValues = null;
        Action nullCollectionWithComparer = () => Assert.AreAllDistinct(nullValues, StringComparer.OrdinalIgnoreCase);
        Action defaultComparer = () => Assert.AreAllDistinct(values);
        Action customComparer = () => Assert.AreAllDistinct(strings, StringComparer.OrdinalIgnoreCase);
        Action nullCollection = () => Assert.AreAllDistinct(nullValues);
        Action nullComparer = () => Assert.AreAllDistinct(strings, (IEqualityComparer?)null);

        nullCollectionWithComparer.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'collection' is invalid. The value cannot be null.");
        AssertFailedException defaultFailure = defaultComparer.Should().ThrowExactly<AssertFailedException>().Which;
        defaultFailure.Message.Replace("\r\n", "\n").Should().Be(
            """
            Assertion failed. Expected all items in collection to be distinct.

            duplicates: [1]
            collection: [1, 2, 1]

            Assert.AreAllDistinct(values)
            """.Replace("\r\n", "\n"));
        defaultFailure.ActualText.Should().Be("[1, 2, 1]");
        customComparer.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Replace("\r\n", "\n").Should().Be(
                $$"""
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: ["VALUE"]
                collection: ["value", "VALUE"]
                comparer:   {{StringComparer.OrdinalIgnoreCase.GetType().Name}}

                Assert.AreAllDistinct(strings, <comparer>)
                """.Replace("\r\n", "\n"));
        nullCollection.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'collection' is invalid. The value cannot be null.");
        nullComparer.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'comparer' is invalid. The value cannot be null.");
        IEnumerable distinctValues = new ArrayList { "value", "other" };
        Action distinct = () => Assert.AreAllDistinct(distinctValues, StringComparer.OrdinalIgnoreCase);
        distinct.Should().NotThrow();
    }

    public void AreAllDistinct_MixedNullAndValueDuplicatesPreserveBoth()
    {
        IEnumerable<object?> values = [1, null, 2, 1, null];
        Action compare = () => Assert.AreAllDistinct(values);

        compare.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Replace("\r\n", "\n").Should().Be(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [1, null]
                collection: [1, null, 2, 1, null]

                Assert.AreAllDistinct(values)
                """.Replace("\r\n", "\n"));
    }
}
