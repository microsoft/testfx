// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the <see cref="Assert"/> class and its nested <see cref="Assert.AssertSingleInterpolatedStringHandler{TItem}"/> struct.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region Helper Methods

    /// <summary>
    /// Invokes the ComputeAssertion method on a handler and returns the exception message if one is thrown.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <returns>The exception message thrown by ComputeAssertion.</returns>
    private static string GetComputeAssertionExceptionMessage<T>(Assert.AssertSingleInterpolatedStringHandler<T> handler)
    {
        try
        {
            // This call is expected to throw when _builder is not null.
            _ = handler.ComputeAssertion();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        throw new Exception("Expected exception was not thrown.");
    }

    #endregion

    #region AssertSingleInterpolatedStringHandler and ComputeAssertion Tests

    /// <summary>
    /// Tests that ComputeAssertion returns the single element when the collection has exactly one element.
    /// </summary>
    public void ComputeAssertion_WhenCollectionHasSingleElement_ReturnsElement()
    {
        // Arrange
        int singleItem = 42;
        var collection = new List<int> { singleItem };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<int>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeFalse();

        // Act
        int result = handler.ComputeAssertion();

        // Assert
        result.Should().Be(singleItem);
    }

    /// <summary>
    /// Tests that ComputeAssertion throws an exception when the collection does not contain exactly one element.
    /// </summary>
    public void ComputeAssertion_WhenCollectionDoesNotHaveSingleElement_ThrowsException()
    {
        // Arrange: collection with multiple elements forces shouldAppend true.
        var collection = new List<int> { 1, 2 };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<int>(literalLength: 5, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: verify that the exception message contains expected parts.
        exMsg.Should().Contain("ContainsSingle");
        exMsg.Should().Contain("2"); // actual count is 2.
    }

    #endregion

    #region AppendLiteral and AppendFormatted Tests

    /// <summary>
    /// Tests that AppendLiteral appends the given literal text which appears in the thrown message.
    /// </summary>
    public void AppendLiteral_WhenCalled_AppendsLiteralText()
    {
        // Arrange: use a collection with multiple elements to force using _builder.
        var collection = new List<string> { "a", "b" };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<string>(literalLength: 10, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        string literal = "LiteralText";
        handler.AppendLiteral(literal);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: the exception message should contain the literal appended.
        exMsg.Should().Contain(literal);
    }

    /// <summary>
    /// Tests the AppendFormatted overload that takes a generic value.
    /// </summary>
    public void AppendFormatted_GenericValue_WithoutFormat_AppendsValue()
    {
        // Arrange
        var collection = new List<string> { "x", "y" };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<string>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        string value = "FormattedValue";
        handler.AppendFormatted(value);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert
        exMsg.Should().Contain(value);
    }

    /// <summary>
    /// Tests the AppendFormatted overload with a generic value and a format string.
    /// </summary>
    public void AppendFormatted_GenericValue_WithFormat_AppendsFormattedValue()
    {
        // Arrange
        var collection = new List<int> { 1, 2 };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<int>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        int value = 123;
        string format = "D5";
        handler.AppendFormatted(value, format);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: Check if the value was formatted accordingly ("00123")
        exMsg.Should().Contain("00123");
    }

    /// <summary>
    /// Tests the AppendFormatted overload with a generic value and an alignment.
    /// </summary>
    public void AppendFormatted_GenericValue_WithAlignment_AppendsAlignedValue()
    {
        // Arrange
        var collection = new List<double> { 0.1, 0.2 };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<double>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        double value = 3.14;
        int alignment = 10;
        handler.AppendFormatted(value, alignment);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: alignment is applied via StringBuilder.AppendFormat so result should contain formatted spacing.
        exMsg.Should().Contain("3.14");
    }

    /// <summary>
    /// Tests the AppendFormatted overload with a generic value, alignment, and a format string.
    /// </summary>
    public void AppendFormatted_GenericValue_WithAlignmentAndFormat_AppendsFormattedValue()
    {
        // Arrange
        var collection = new List<DateTime> { DateTime.Now, DateTime.UtcNow };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<DateTime>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        var value = new DateTime(2023, 1, 1);
        int alignment = 8;
        string format = "yyyy";
        handler.AppendFormatted(value, alignment, format);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: formatted year "2023" should appear.
        exMsg.Should().Contain("2023");
    }

    /// <summary>
    /// Tests the AppendFormatted overload that takes a string value.
    /// </summary>
    public void AppendFormatted_StringValue_WithoutAdditionalParameters_AppendsString()
    {
        // Arrange
        var collection = new List<string> { "first", "second" };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<string>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        string value = "TestString";
        handler.AppendFormatted(value);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert
        exMsg.Should().Contain(value);
    }

    /// <summary>
    /// Tests the AppendFormatted overload with a string value along with alignment and format.
    /// </summary>
    public void AppendFormatted_StringValue_WithAlignmentAndFormat_AppendsFormattedString()
    {
        // Arrange
        var collection = new List<string> { "one", "two" };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<string>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        string value = "Hello";
        int alignment = 10;
        string format = "X"; // Though format for string doesn't change, we test the overload route.
        handler.AppendFormatted(value, alignment, format);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert
        exMsg.Should().Contain(value);
    }

    /// <summary>
    /// Tests the AppendFormatted overload for object with alignment and format.
    /// </summary>
    public void AppendFormatted_ObjectValue_WithAlignmentAndFormat_AppendsFormattedObject()
    {
        // Arrange
        var collection = new List<object> { 1, 2 };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<object>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        object value = 99;
        int alignment = 5;
        string format = "D3";
        handler.AppendFormatted(value, alignment, format);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert: Formatted value should appear (e.g. "099").
        exMsg.Should().Contain("099");
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Tests the AppendFormatted overload for ReadOnlySpan/<char/>.
    /// </summary>
    public void AppendFormatted_ReadOnlySpan_AppendsSpanValue()
    {
        // Arrange
        var collection = new List<string> { "alpha", "beta" };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<string>(literalLength: 0, formattedCount: 0, collection, out bool shouldAppend);
        shouldAppend.Should().BeTrue();
        ReadOnlySpan<char> spanValue = "SpanText".AsSpan();
        handler.AppendFormatted(spanValue);

        // Act
        string exMsg = GetComputeAssertionExceptionMessage(handler);

        // Assert
        exMsg.Should().Contain("SpanText");
    }
#endif

    #endregion

    #region ContainsSingle Tests

    /// <summary>
    /// Tests the ContainsSingle method without message parameters where the collection has a single element.
    /// </summary>
    public void ContainsSingle_NoMessage_WithSingleElement_ReturnsElement()
    {
        // Arrange
        var collection = new List<int> { 100 };

        // Act
        int result = Assert.ContainsSingle(collection);

        // Assert
        result.Should().Be(100);
    }

    /// <summary>
    /// Tests the ContainsSingle method with a message where the collection has a single element.
    /// </summary>
    public void ContainsSingle_WithMessage_WithSingleElement_ReturnsElement()
    {
        // Arrange
        var collection = new List<string> { "OnlyOne" };

        // Act
        string result = Assert.ContainsSingle(collection, "Custom message");

        // Assert
        result.Should().Be("OnlyOne");
    }

    /// <summary>
    /// Tests the ContainsSingle method that uses an interpolated string handler when the collection has multiple elements.
    /// Expects an exception.
    /// </summary>
    public void ContainsSingle_InterpolatedHandler_WithMultipleElements_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };
        var handler = new Assert.AssertSingleInterpolatedStringHandler<int>(literalLength: 5, formattedCount: 0, collection, out bool dummy);

        // Act
        Action action = () => Assert.ContainsSingle(collection, ref handler);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 3 element(s). ");
    }

    /// <summary>
    /// Tests the ContainsSingle method with message and parameters when the collection does not have exactly one element.
    /// Expects an exception.
    /// </summary>
    public void ContainsSingle_WithMessageAndParams_WithInvalidCollection_ThrowsException()
    {
        // Arrange
        var collection = new List<string> { "a", "b" };

        // Act
        Action action = () => Assert.ContainsSingle(collection, "Expected: {0}", "SingleItem");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*SingleItem*");
    }

    #endregion

    #region Contains Tests

    /// <summary>
    /// Tests the Contains method (value overload) when the expected item is present.
    /// </summary>
    public void Contains_ValueExpected_ItemExists_DoesNotThrow()
    {
        // Arrange
        var collection = new List<int> { 5, 10, 15 };

        // Act
        Action action = () => Assert.Contains(10, collection, "No failure expected", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the Contains method (value overload) when the expected item is not present.
    /// Expects an exception.
    /// </summary>
    public void Contains_ValueExpected_ItemDoesNotExist_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 5, 10, 15 };

        // Act
        Action action = () => Assert.Contains(20, collection, "Item {0} not found", 20);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*20*");
    }

    /// <summary>
    /// Tests the Contains method with a comparer when the expected item is present.
    /// </summary>
    public void Contains_WithComparer_ItemExists_DoesNotThrow()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana" };
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        Action action = () => Assert.Contains("APPLE", collection, comparer, "Should find apple", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the Contains method with a comparer when the expected item is not present.
    /// Expects an exception.
    /// </summary>
    public void Contains_WithComparer_ItemDoesNotExist_ThrowsException()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana" };
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        Action action = () => Assert.Contains("cherry", collection, comparer, "Missing {0}", "cherry");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*cherry*");
    }

    /// <summary>
    /// Tests the Contains method that accepts a predicate when an element satisfies the condition.
    /// </summary>
    public void Contains_Predicate_ItemMatches_DoesNotThrow()
    {
        // Arrange
        var collection = new List<int> { 2, 4, 6 };

        // Act
        Action action = () => Assert.Contains(IsEven, collection, "Even number exists", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the Contains method that accepts a predicate when no element satisfies the condition.
    /// Expects an exception.
    /// </summary>
    public void Contains_Predicate_NoItemMatches_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 1, 3, 5 };

        // Act
        Action action = () => Assert.Contains(IsEven, collection, "No even number found", null);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*even*");
    }

    /// <summary>
    /// Tests the string Contains overload when the substring is contained in the value.
    /// </summary>
    public void Contains_StringVersion_SubstringPresent_DoesNotThrow()
    {
        // Arrange
        string value = "The quick brown fox";
        string substring = "brown";

        // Act
        Action action = () => Assert.Contains(substring, value, StringComparison.Ordinal, "Substring found", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the string Contains overload when the substring is not contained in the value.
    /// Expects an exception.
    /// </summary>
    public void Contains_StringVersion_SubstringNotPresent_ThrowsException()
    {
        // Arrange
        string value = "The quick brown fox";
        string substring = "lazy";

        // Act
        Action action = () => Assert.Contains(substring, value, StringComparison.Ordinal, "Missing substring", null);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*lazy*");
    }

    #endregion

    #region DoesNotContain Tests

    /// <summary>
    /// Tests the DoesNotContain method (value overload) when the expected item is not present.
    /// </summary>
    public void DoesNotContain_ValueExpected_ItemNotPresent_DoesNotThrow()
    {
        // Arrange
        var collection = new List<int> { 5, 10, 15 };

        // Act
        Action action = () => Assert.DoesNotContain(20, collection, "No failure expected", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the DoesNotContain method (value overload) when the expected item is present.
    /// Expects an exception.
    /// </summary>
    public void DoesNotContain_ValueExpected_ItemPresent_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 5, 10, 15 };

        // Act
        Action action = () => Assert.DoesNotContain(10, collection, "Item {0} should not be found", 10);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*10*");
    }

    /// <summary>
    /// Tests the DoesNotContain method with a comparer when the item is not present.
    /// </summary>
    public void DoesNotContain_WithComparer_ItemNotPresent_DoesNotThrow()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana" };
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        Action action = () => Assert.DoesNotContain("cherry", collection, comparer, "No cherry found", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the DoesNotContain method with a comparer when the item is present.
    /// Expects an exception.
    /// </summary>
    public void DoesNotContain_WithComparer_ItemPresent_ThrowsException()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana" };
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        Action action = () => Assert.DoesNotContain("APPLE", collection, comparer, "Unexpected {0}", "APPLE");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*APPLE*");
    }

    /// <summary>
    /// Tests the DoesNotContain method that accepts a predicate when no element satisfies the predicate.
    /// </summary>
    public void DoesNotContain_Predicate_NoItemMatches_DoesNotThrow()
    {
        // Arrange
        var collection = new List<int> { 1, 3, 5 };

        // Act
        Action action = () => Assert.DoesNotContain(IsEven, collection, "All items are odd", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the DoesNotContain method that accepts a predicate when at least one element satisfies the predicate.
    /// Expects an exception.
    /// </summary>
    public void DoesNotContain_Predicate_AtLeastOneItemMatches_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 2, 3, 5 };

        // Act
        Action action = () => Assert.DoesNotContain(IsEven, collection, "An even number exists", null);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*even*");
    }

    /// <summary>
    /// Tests the string DoesNotContain overload when the substring is not contained in the value.
    /// </summary>
    public void DoesNotContain_StringVersion_SubstringNotPresent_DoesNotThrow()
    {
        // Arrange
        string value = "The quick brown fox";
        string substring = "lazy";

        // Act
        Action action = () => Assert.DoesNotContain(substring, value, StringComparison.Ordinal, "Should not contain", null);

        // Assert
        action.Should().NotThrow<AssertFailedException>();
    }

    /// <summary>
    /// Tests the string DoesNotContain overload when the substring is contained in the value.
    /// Expects an exception.
    /// </summary>
    public void DoesNotContain_StringVersion_SubstringPresent_ThrowsException()
    {
        // Arrange
        string value = "The quick brown fox";
        string substring = "brown";

        // Act
        Action action = () => Assert.DoesNotContain(substring, value, StringComparison.Ordinal, "Unexpected substring", null);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*brown*");
    }

    private static bool IsEven(int x) => x % 2 == 0;

    #endregion

    #region ContainsSingle with Predicate Tests

    /// <summary>
    /// Tests the ContainsSingle method with predicate when exactly one element matches.
    /// </summary>
    public void ContainsSinglePredicate_NoMessage_OneItemMatches_ReturnsElement()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        int result = Assert.ContainsSingle(x => x == 3, collection);

        // Assert
        result.Should().Be(3);
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate and message when exactly one element matches.
    /// </summary>
    public void ContainsSinglePredicate_WithMessage_OneItemMatches_ReturnsElement()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana", "cherry" };

        // Act
#pragma warning disable CA1865 // Use char overload - not netfx
        string result = Assert.ContainsSingle(x => x.StartsWith("b", StringComparison.Ordinal), collection, "Expected one item starting with 'b'");
#pragma warning restore CA1865 // Use char overload

        // Assert
        result.Should().Be("banana");
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate when no elements match.
    /// Expects an exception.
    /// </summary>
    public void ContainsSinglePredicate_NoItemMatches_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 1, 3, 5 };

        // Act
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected exactly one item to match the predicate but found 0 item(s)*");
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate when multiple elements match.
    /// Expects an exception.
    /// </summary>
    public void ContainsSinglePredicate_MultipleItemsMatch_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 2, 4, 6, 8 };

        // Act
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected exactly one item to match the predicate but found 4 item(s)*");
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate and formatted message when no elements match.
    /// Expects an exception with the custom message.
    /// </summary>
    public void ContainsSinglePredicate_WithMessage_NoItemMatches_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 1, 3, 5 };

        // Act
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, $"No even numbers found in collection with {collection.Count} items");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*No even numbers found in collection with 3 items*");
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate and formatted message when multiple elements match.
    /// Expects an exception with the custom message.
    /// </summary>
    public void ContainsSinglePredicate_WithMessage_MultipleItemsMatch_ThrowsException()
    {
        // Arrange
        var collection = new List<int> { 2, 4, 6 };

        // Act
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, $"Too many even numbers found: {collection.Count}");

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Too many even numbers found: 3*");
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate using complex objects.
    /// </summary>
    public void ContainsSinglePredicate_ComplexObjects_OneItemMatches_ReturnsElement()
    {
        // Arrange
        var items = new List<Person>
        {
            new("Alice", 25),
            new("Bob", 30),
            new("Charlie", 35),
        };

        // Act
        Person result = Assert.ContainsSingle(p => p.Age == 30, items);

        // Assert
        result.Name.Should().Be("Bob");
        result.Age.Should().Be(30);
    }

    /// <summary>
    /// Tests the ContainsSingle method with predicate using null values.
    /// </summary>
    public void ContainsSinglePredicate_WithNullValues_OneItemMatches_ReturnsElement()
    {
        // Arrange
        var collection = new List<string?> { "apple", null, "banana" };

        // Act
        string? result = Assert.ContainsSingle(x => x == null, collection);

        // Assert
        result.Should().BeNull();
    }

    #region New Error Message Tests

    /// <summary>
    /// Tests that Contains (item) failure shows specific error message.
    /// </summary>
    public void Contains_ItemNotFound_ShowsSpecificErrorMessage()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act
        Action action = () => Assert.Contains(5, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected collection to contain the specified item*");
    }

    /// <summary>
    /// Tests that Contains (predicate) failure shows specific error message.
    /// </summary>
    public void Contains_PredicateNotMatched_ShowsSpecificErrorMessage()
    {
        // Arrange
        var collection = new List<int> { 1, 3, 5 };

        // Act
        Action action = () => Assert.Contains(x => x % 2 == 0, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected at least one item to match the predicate*");
    }

    /// <summary>
    /// Tests that DoesNotContain (item) failure shows specific error message.
    /// </summary>
    public void DoesNotContain_ItemFound_ShowsSpecificErrorMessage()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act
        Action action = () => Assert.DoesNotContain(2, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected collection to not contain the specified item*");
    }

    /// <summary>
    /// Tests that DoesNotContain (predicate) failure shows specific error message.
    /// </summary>
    public void DoesNotContain_PredicateMatched_ShowsSpecificErrorMessage()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act
        Action action = () => Assert.DoesNotContain(x => x % 2 == 0, collection);

        // Assert
        action.Should().Throw<AssertFailedException>().WithMessage("*Expected no items to match the predicate*");
    }

    #endregion

    private record Person(string Name, int Age);

    #endregion
}
