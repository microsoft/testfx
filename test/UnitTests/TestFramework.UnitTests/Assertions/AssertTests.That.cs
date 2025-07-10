// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void That_BooleanCondition_FailsAsExpected()
    {
        Action act = () => Assert.That(() => false, "Boolean condition failed");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => False) failed.
                Message: Boolean condition failed
                """);
    }

    public void That_NumericComparison_FailsAsExpected()
    {
        int x = 5;

        Action act = () => Assert.That(() => x > 10, "x should be greater than 10");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => x > 10) failed.
                Message: x should be greater than 10
                Details:
                  x = 5
                """);
    }

    public void That_StringEquality_FailsAsExpected()
    {
        string s = "hello";

        Action act = () => Assert.That(() => s == "world", "String equality failed");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => s == "world") failed.
                Message: String equality failed
                Details:
                  s = "hello"
                """);
    }

    public void That_NullString_FailsAsExpected()
    {
        string? s = null;

        Action act = () => Assert.That(() => s != null, "String should not be null");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => s != null) failed.
                Message: String should not be null
                Details:
                  s = null
                """);
    }

    public void That_CombinedBooleanCondition_FailsAsExpected()
    {
        bool a = true;
        bool b = false;

        Action act = () => Assert.That(() => a && b, "Both should be true");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => a && b) failed.
                Message: Both should be true
                Details:
                  a = True
                  b = False
                """);
    }

    public void That_FormatNestedCollections()
    {
        var nested = new List<int[]> { new[] { 1, 2 }, new[] { 3 } };

        Action action = () => Assert.That(() => nested.Any(arr => arr.Length > 3), "Check nested arrays");

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => nested.Any(arr => arr.Length > 3)) failed.
                Message: Check nested arrays
                Details:
                  nested = [[1, 2], [3]]
                """);
    }

    public void That_PropertyComparison_FailsAsExpected()
    {
        var person = new { Name = "John", Age = 25 };

        Action act = () => Assert.That(() => person.Age > 30, "Age should be greater than 30");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => person.Age > 30) failed.
            Message: Age should be greater than 30
            Details:
              person.Age = 25
            """);
    }

    public void That_MethodCall_FailsAsExpected()
    {
        string text = "hello";

        Action act = () => Assert.That(() => text.StartsWith("world"), "Text should start with 'world'");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => text.StartsWith("world")) failed.
            Message: Text should start with 'world'
            Details:
              text = "hello"
            """);
    }

    public void That_ChainedPropertyAccess_FailsAsExpected()
    {
        var user = new { Profile = new { IsActive = false } };

        Action act = () => Assert.That(() => user.Profile.IsActive, "User profile should be active");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => user.Profile.IsActive) failed.
            Message: User profile should be active
            Details:
              user.Profile.IsActive = False
            """);
    }

    public void That_ArrayIndexAccess_FailsAsExpected()
    {
        int[] numbers = [1, 2, 3];

        Action act = () => Assert.That(() => numbers[1] == 5, "Second element should be 5");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => numbers[1] == 5) failed.
            Message: Second element should be 5
            Details:
              numbers[1] = 2
            """);
    }

    public void That_NullableComparison_FailsAsExpected()
    {
        int? nullableValue = null;

        Action act = () => Assert.That(() => nullableValue.HasValue, "Nullable should have value");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => nullableValue.HasValue) failed.
            Message: Nullable should have value
            Details:
              nullableValue.HasValue = False
            """);
    }

    public void That_OrElseCondition_FailsAsExpected()
    {
        bool condition1 = false;
        bool condition2 = false;

        Action act = () => Assert.That(() => condition1 || condition2, "At least one should be true");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => condition1 || condition2) failed.
            Message: At least one should be true
            Details:
              condition1 = False
              condition2 = False
            """);
    }

    public void That_ComplexExpression_FailsAsExpected()
    {
        int x = 5;
        int y = 10;
        int z = 15;

        Action act = () => Assert.That(() => (x + y) * 2 > z * 3, "Complex calculation should be greater");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => (x + y) * 2 > z * 3) failed.
            Message: Complex calculation should be greater
            Details:
              x = 5
              y = 10
              z = 15
            """);
    }

    public void That_LinqExpression_FailsAsExpected()
    {
        int[] numbers = [1, 2, 3, 4];

        Action act = () => Assert.That(() => numbers.All(n => n > 5), "All numbers should be greater than 5");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => numbers.All(n => n > 5)) failed.
            Message: All numbers should be greater than 5
            Details:
              numbers = [1, 2, 3, 4]
            """);
    }

    public void That_TypeComparison_FailsAsExpected()
    {
        object obj = "string";

        Action act = () => Assert.That(() => obj is int, "Object should be an integer");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => obj is int) failed.
            Message: Object should be an integer
            Details:
              obj = "string"
            """);
    }

    public void That_NotEqualComparison_FailsAsExpected()
    {
        string value = "test";

        Action act = () => Assert.That(() => value != "test", "Value should not be 'test'");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => value != "test") failed.
            Message: Value should not be 'test'
            Details:
              value = "test"
            """);
    }

    public void That_MultipleVariablesInComplexExpression_FailsAsExpected()
    {
        int min = 10;
        int max = 20;
        int current = 25;

        Action act = () => Assert.That(() => current >= min && current <= max, "Value should be within range");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => current >= min && current <= max) failed.
            Message: Value should be within range
            Details:
              current = 25
              max = 20
              min = 10
            """);
    }

    public void That_DictionaryAccess_FailsAsExpected()
    {
        var dict = new Dictionary<string, int> { ["key1"] = 10, ["key2"] = 20 };

        Action act = () => Assert.That(() => dict["key1"] > 15, "Dictionary value should be greater than 15");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => dict["key1"] > 15) failed.
            Message: Dictionary value should be greater than 15
            Details:
              dict["key1"] = 10
            """);
    }

    public void That_NestedMethodCalls_FailsAsExpected()
    {
        string text = "Hello World";

        Action act = () => Assert.That(() => text.Substring(6).StartsWith("Universe"), "Substring should start with 'Universe'");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => text.Substring(6).StartsWith("Universe")) failed.
            Message: Substring should start with 'Universe'
            Details:
              text = "Hello World"
            """);
    }

    public void That_StaticPropertyAccess_FailsAsExpected()
    {
        Action act = () => Assert.That(() => DateTime.Now.Year < 2000, "Current year should be before 2000");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => DateTime.Now.Year < 2000) failed.
            Message: Current year should be before 2000
            Details:
              DateTime.Now.Year = 2025
            """);
    }

    public void That_GenericMethodCall_FailsAsExpected()
    {
        var list = new List<string> { "a", "b", "c" };

        Action act = () => Assert.That(() => list.Contains("d"), "List should contain 'd'");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => list.Contains("d")) failed.
            Message: List should contain 'd'
            Details:
              list = ["a", "b", "c"]
            """);
    }

    public void That_ConditionalExpression_FailsAsExpected()
    {
        bool flag = true;
        int value1 = 5;
        int value2 = 10;

        Action act = () => Assert.That(() => (flag ? value1 : value2) > 15, "Conditional result should be greater than 15");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => (flag ? value1 : value2) > 15) failed.
            Message: Conditional result should be greater than 15
            Details:
              flag = True
              value1 = 5
              value2 = 10
            """);
    }

    public void That_ArrayLength_FailsAsExpected()
    {
        int[] numbers = [1, 2, 3, 4, 5];

        Action act = () => Assert.That(() => numbers.Length > 10, "Array should have more than 10 elements");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => numbers.Length > 10) failed.
                Message: Array should have more than 10 elements
                Details:
                  numbers.Length = 5
                """);
    }
}
