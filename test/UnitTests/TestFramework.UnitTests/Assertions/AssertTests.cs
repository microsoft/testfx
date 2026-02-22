// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region Instance tests
    public void InstanceShouldReturnAnInstanceOfAssert() => Assert.That.Should().NotBeNull();

    public void InstanceShouldCacheAssertInstance() => Assert.That.Should().BeSameAs(Assert.That);
    #endregion

    #region BuildUserMessage tests

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        string message = Assert.BuildUserMessage("{");
        message.Should().Be("{");
    }
    #endregion

    #region Obsolete methods tests
#if DEBUG
    public void ObsoleteEqualsMethodThrowsAssertFailedException()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Action act = () => Assert.Equals("test", "test");
#pragma warning restore CS0618 // Type or member is obsolete
        act.Should().Throw<AssertFailedException>()
           .WithMessage("*Assert.Equals should not be used for Assertions*");
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Action act = () => Assert.ReferenceEquals(obj, obj);
#pragma warning restore CS0618 // Type or member is obsolete
        act.Should().Throw<AssertFailedException>()
           .WithMessage("*Assert.ReferenceEquals should not be used for Assertions*");
    }
#endif
    #endregion

    private static Task<string> GetHelloStringAsync()
        => Task.FromResult("Hello");

    private sealed class DummyClassTrackingToStringCalls
    {
        public bool WasToStringCalled { get; private set; }

        public override string ToString()
        {
            WasToStringCalled = true;
            return nameof(DummyClassTrackingToStringCalls);
        }
    }

    private sealed class DummyIFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
            => "DummyIFormattable.ToString()";
    }

    #region FormatValue truncation

    public void FormatValue_WhenStringExceedsMaxLength_ShouldTruncateWithEllipsis()
    {
        // FormatValue truncation applies to non-string-diff contexts like IsNull
        // 300 'x' chars -> quoted as "xxx..." (302 chars total) -> truncated at 256 chars with ellipsis
        string longValue = new('x', 300);
        // Truncate takes first 256 chars of quoted string: opening quote + 255 x's, then appends "... (302 chars)"
        string expectedValue = "\"" + new string('x', 255) + "... (302 chars)";

        Action action = () => Assert.IsNull(longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsNull failed.
                Expected value to be null.
                  value (longValue): {expectedValue}
                """);
    }

    public void FormatValue_WhenStringIsWithinMaxLength_ShouldNotTruncate()
    {
        string value = new('x', 50);
        string expectedFullValue = "\"" + new string('x', 50) + "\"";

        Action action = () => Assert.IsNull(value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsNull failed.
                Expected value to be null.
                  value: {expectedFullValue}
                """);
    }

    public void FormatValue_WhenCustomToStringExceedsMaxLength_ShouldTruncate()
    {
        // Custom ToString returns 300 chars ? truncated at 256
        var obj = new ObjectWithLongToString();
        string expectedValue = new string('L', 256) + "... (300 chars)";

        Action action = () => Assert.IsNull(obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsNull failed.
                Expected value to be null.
                  value (obj): {expectedValue}
                """);
    }

    public void TruncateExpression_WhenExpressionExceeds100Chars_ShouldShowEllipsis()
    {
        // Variable name is 113 chars ? exceeds 100 char limit ? truncated with "..."
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = null!;

        Action action = () => Assert.IsNotNull(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNotNull failed.
                Expected a non-null value.
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): (null)
                """);
    }

    #endregion

    #region FormatValue newline escaping

    public void FormatValue_WhenValueContainsNewlines_ShouldEscapeThem()
    {
        var obj = new ObjectWithNewlineToString();

        Action action = () => Assert.IsNull(obj);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNull failed.
                Expected value to be null.
                  value (obj): line1\r\nline2\nline3
                """);
    }

    public void FormatValue_WhenStringContainsNewlines_ShouldEscapeThem()
    {
        string value = "hello\nworld";

        Action action = () => Assert.IsNull(value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNull failed.
                Expected value to be null.
                  value: "hello\nworld"
                """);
    }

    #endregion

    #region FormatValue collection preview

    public void FormatValue_WhenValueIsCollection_ShouldShowPreview()
    {
        var collection = new List<int> { 1, 2, 3 };

        Action action = () => Assert.IsNull(collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNull failed.
                Expected value to be null.
                  value (collection): [1, 2, 3] (3 elements)
                """);
    }

    public void FormatCollectionPreview_WhenTotalStringLengthExceeds256_ShouldTruncate()
    {
        // Each element is a 30-char string ? FormatValue wraps in quotes ? "aaa...aaa" = 32 chars
        // With ", " separator: first = 32, subsequent = 34 each
        // 32 + 6×34 = 236 = 256, 32 + 7×34 = 270 > 256
        // So 7 elements should display, then "...", with total count 20
        var collection = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            collection.Add(new string((char)('a' + (i % 26)), 30));
        }

        Action action = () => Assert.Contains("not-there", collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.Contains failed.
                Expected collection to contain the specified item.
                  expected: "not-there"
                  collection*[*...*] (20 elements)
                """);
    }

    public void FormatCollectionPreview_WhenElementToStringExceeds50_ShouldTruncateElement()
    {
        // Element has 80-char string ? FormatValue(maxLength:50) ? "zzz..." (82 chars quoted) ? truncated at 50
        var collection = new List<string> { new string('z', 80), "short" };
        string expectedFirstElement = "\"" + new string('z', 49) + "... (82 chars)";

        Action action = () => Assert.Contains("not-there", collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.Contains failed.
                Expected collection to contain the specified item.
                  expected: "not-there"
                  collection*[{expectedFirstElement}, "short"] (2 elements)
                """);
    }

    public void FormatCollectionPreview_WhenCollectionContainsNestedCollections_ShouldShowNestedPreview()
    {
        var inner1 = new List<int> { 1, 2 };
        var inner2 = new List<int> { 3, 4 };
        var outer = new List<List<int>> { inner1, inner2 };

        Action action = () => Assert.IsNull(outer);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNull failed.
                Expected value to be null.
                  value (outer): [[1, 2] (2 elements), [3, 4] (2 elements)] (2 elements)
                """);
    }

    public void FormatCollectionPreview_WhenNestedCollectionIsLarge_ShouldTruncateInnerAt50()
    {
        // Inner collection has many elements ? inner preview string budget is 50 chars
        // Elements 0-9 are 1 char each: "0" takes 1, subsequent take 3 (digit + ", ")
        // 1 + 9×3 = 28 chars for 0-9. Then 10-99 are 2 char digits + 2 sep = 4 each
        // 28 + 4n = 50 ? n = 5.5 ? 5 more (10-14). 28 + 5×4 = 48, next would be 52 > 50
        // So inner preview shows: 0-14 (15 elements), then "..."
        var inner = new List<int>();
        for (int i = 0; i < 50; i++)
        {
            inner.Add(i);
        }

        var outer = new List<List<int>> { inner };

        Action action = () => Assert.IsNull(outer);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNull failed.
                Expected value to be null.
                  value (outer): [[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, ...] (50 elements)] (1 element)
                """);
    }

    public void FormatCollectionPreview_WhenElementContainsNewlines_ShouldEscapeThem()
    {
        var collection = new List<string> { "line1\nline2", "ok" };

        Action action = () => Assert.Contains("not-there", collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.Contains failed.
                Expected collection to contain the specified item.
                  expected: "not-there"
                  collection*["line1\nline2", "ok"] (2 elements)
                """);
    }

    public void FormatCollectionPreview_WhenSingleElement_ShouldShowSingularForm()
    {
        var collection = new List<int> { 42 };

        Action action = () => Assert.Contains(99, collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.Contains failed.
                Expected collection to contain the specified item.
                  expected: 99
                  collection*[42] (1 element)
                """);
    }

    #endregion
}

[TestClass]
public sealed class ObjectWithNewlineToString
{
    public override string ToString() => "line1\r\nline2\nline3";
}

[TestClass]
public sealed class ObjectWithLongToString
{
    public override string ToString() => new string('L', 300);
}
