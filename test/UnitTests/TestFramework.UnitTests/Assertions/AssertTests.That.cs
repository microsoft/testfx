// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void That_WithNullExpression_Throws()
    {
        Action act = () => Assert.That(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("condition");
    }

    public void That_HappyPath_DoesNotThrow()
    {
        Action act = () => Assert.That(() => true);

        act.Should().NotThrow<AssertFailedException>();
    }

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
        object obj = "hello";

        Action act = () => Assert.That(() => obj is int, "Object should be an integer");

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
            Assert.That(() => obj is int) failed.
            Message: Object should be an integer
            Details:
              obj = "hello"
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
              text.Substring(6) = "World"
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

    public void That_UnaryExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        int value = 5;

        // Act & Assert
        Action action = () => Assert.That(() => !Convert.ToBoolean(value));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => !Convert.ToBoolean(value)) failed.
                Details:
                  value = 5
                """);
    }

    public void That_UnaryExpression_WithNegation_ExtractsVariablesCorrectly()
    {
        // Arrange
        bool flag = true;

        // Act & Assert
        Action action = () => Assert.That(() => !flag);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => !flag) failed.
                Details:
                  flag = True
                """);
    }

    public void That_InvocationExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        Func<int, bool> predicate = x => x > 10;
        int testValue = 5;

        // Act & Assert
        Action action = () => Assert.That(() => predicate(testValue));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => predicate(testValue)) failed.
                Details:
                  testValue = 5
                """);
    }

    public void That_InvocationExpression_WithComplexArguments_ExtractsVariablesCorrectly()
    {
        // Arrange
        Func<string, int, bool> complexFunc = (s, i) => s.Length == i;
        string text = "hello";
        int expectedLength = 3;

        // Act & Assert
        Action action = () => Assert.That(() => complexFunc(text, expectedLength));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => complexFunc(text, expectedLength)) failed.
                Details:
                  expectedLength = 3
                  text = "hello"
                """);
    }

    public void That_NewExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        int year = 2023;
        int month = 12;
        int day = 25;

        // Act & Assert
        Action action = () => Assert.That(() => new DateTime(year, month, day) == DateTime.MinValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new DateTime(year, month, day) == DateTime.MinValue) failed.
                Details:
                  DateTime.MinValue = 1/1/0001 12:00:00 AM
                  day = 25
                  month = 12
                  new DateTime(year, month, day) = 12/25/2023 12:00:00 AM
                  year = 2023
                """);
    }

    public void That_NewExpression_WithComplexArguments_ExtractsVariablesCorrectly()
    {
        // Arrange
        string firstName = "John";
        string lastName = "Doe";

        // Act & Assert
        Action action = () => Assert.That(() => new { Name = firstName + " " + lastName }.Name == "Jane Doe");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new { Name = firstName + " " + lastName }.Name == "Jane Doe") failed.
                Details:
                  firstName = "John"
                  lastName = "Doe"
                  new { Name = ((firstName + " ") + lastName) }.Name = "John Doe"
                """);
    }

    public void That_ListInitExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        int first = 1;
        int second = 2;
        int third = 3;

        // Act & Assert
        Action action = () => Assert.That(() => new List<int> { first, second, third }.Count == 5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new List<int> { first, second, third }.Count == 5) failed.
                Details:
                  first = 1
                  new List<int> { first, second, third }.Count = 3
                  second = 2
                  third = 3
                """);
    }

    public void That_ListInitExpression_WithComplexElements_ExtractsVariablesCorrectly()
    {
        // Arrange
        string name1 = "Alice";
        string name2 = "Bob";
        int age1 = 25;
        int age2 = 30;

        // Act & Assert
        Action action = () => Assert.That(() => new List<object> { new { Name = name1, Age = age1 }, new { Name = name2, Age = age2 } }.Count == 1);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new List<object> { new { Name = name1, Age = age1 }, new { Name = name2, Age = age2 } }.Count == 1) failed.
                Details:
                  age1 = 25
                  age2 = 30
                  name1 = "Alice"
                  name2 = "Bob"
                  new List<object> { new { Name = name1, Age = age1 }, new { Name = name2, Age = age2 } }.Count = 2
                """);
    }

    public void That_NewArrayExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        int x = 10;
        int y = 20;
        int z = 30;

        // Act & Assert
        Action action = () => Assert.That(() => new[] { x, y, z }.Length == 5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new[] { x, y, z }.Length == 5) failed.
                Details:
                  new [] {x, y, z}.Length = 3
                  x = 10
                  y = 20
                  z = 30
                """);
    }

    public void That_NewArrayExpression_WithExpressions_ExtractsVariablesCorrectly()
    {
        // Arrange
        int multiplier = 5;
        int baseValue = 3;

        // Act & Assert
        Action action = () => Assert.That(() => new[] { baseValue * multiplier, baseValue + multiplier }.Length == 1);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => new[] { baseValue * multiplier, baseValue + multiplier }.Length == 1) failed.
                Details:
                  baseValue = 3
                  multiplier = 5
                  new [] {(baseValue * multiplier), (baseValue + multiplier)}.Length = 2
                """);
    }

    public void That_IndexExpression_ExtractsVariablesCorrectly()
    {
        // Arrange
        var dict = new Dictionary<string, int> { ["key1"] = 100, ["key2"] = 200 };
        string key = "key1";
        int expectedValue = 150;

        // Act & Assert
        Action action = () => Assert.That(() => dict[key] == expectedValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => dict[key] == expectedValue) failed.
                Details:
                  dict[key] = 100
                  expectedValue = 150
                  key = "key1"
                """);
    }

    public void That_IndexExpression_WithMultipleIndices_ExtractsVariablesCorrectly()
    {
        // Arrange
        int[,] matrix = new int[3, 3]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 },
        };
        int row = 1;
        int col = 2;
        int expectedValue = 10;

        // Act & Assert
        Action action = () => Assert.That(() => matrix[row, col] == expectedValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => matrix[row, col] == expectedValue) failed.
                Details:
                  col = 2
                  expectedValue = 10
                  matrix[row, col] = 6
                  row = 1
                """);
    }

    public void That_IndexExpression_WithComplexIndexArguments_ExtractsVariablesCorrectly()
    {
        // Arrange
        int[] array = [10, 20, 30, 40, 50];
        int offset = 2;
        int start = 1;

        // Act & Assert
        Action action = () => Assert.That(() => array[start + offset] == 100);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => array[start + offset] == 100) failed.
                Details:
                  array[start + offset] = 40
                  offset = 2
                  start = 1
                """);
    }
}
