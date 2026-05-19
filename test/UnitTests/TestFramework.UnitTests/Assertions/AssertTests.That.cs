// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reflection;

using AwesomeAssertions;

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
                Assert.That(() => false) failed.
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
        int year = DateTime.Now.Year;
        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"""
            Assert.That(() => DateTime.Now.Year < 2000) failed.
            Message: Current year should be before 2000
            Details:
              DateTime.Now.Year = {year}
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
            .WithMessage($"""
                Assert.That(() => new DateTime(year, month, day) == DateTime.MinValue) failed.
                Details:
                  DateTime.MinValue = {DateTime.MinValue.ToString(CultureInfo.InvariantCulture)}
                  day = 25
                  month = 12
                  new DateTime(year, month, day) = {new DateTime(year, month, day).ToString(CultureInfo.InvariantCulture)}
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

    public void That_NonGenericCollection_FormatsCorrectly()
    {
        // Arrange - Using ArrayList as a non-generic IEnumerable collection
        var nonGenericCollection = new ArrayList { 1, "hello", true, 42.5 };

        // Act & Assert
        Action action = () => Assert.That(() => nonGenericCollection.Count == 10, "Collection should have 10 items");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => nonGenericCollection.Count == 10) failed.
                Message: Collection should have 10 items
                Details:
                  nonGenericCollection.Count = 4
                """);
    }

    public void That_NonGenericCollectionInComparison_FormatsCorrectly()
    {
        // Arrange - Using ArrayList in a comparison to trigger FormatValue that formats enumerables
        var arrayList = new ArrayList { 1, 2, 3 };
        int[] expectedItems = [1, 2, 3, 4];

        // Act & Assert
        Action action = () => Assert.That(() => arrayList.Count == expectedItems.Length, "Collections should have same count");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.That(() => arrayList.Count == expectedItems.Length) failed.
                Message: Collections should have same count
                Details:
                  arrayList.Count = 3
                  expectedItems.Length = 4
                """);
    }

    public void That_ArrayIndexWithParameterExpression_ExtractsArrayVariable()
    {
        // Arrange
        int[] arrayParam = [10, 20, 30];

        // Act & Assert
        Action action = () => Assert.That(() => arrayParam[arrayParam.Length - 2] == 999);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => arrayParam[arrayParam.Length - 2] == 999) failed.
            Details:
              arrayParam.Length = 3
              arrayParam[arrayParam.Length - 2] = 20
            """);
    }

    public void That_WithCapturedVariableConstants_IncludesInDetails()
    {
        // Test captured variables from closures (non-literal constants)
        string captured = "captured_value";
        int capturedNumber = 42;

        Action act = () => Assert.That(() => captured == "wrong_value" && capturedNumber > 50);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => captured == "wrong_value" && capturedNumber > 50) failed.
            Details:
              captured = "captured_value"
              capturedNumber = 42
            """);
    }

    public void That_WithStringLiteralsAndCustomMessage_SkipsLiteralInDetails()
    {
        // Test that string literals are not included in details with custom message
        string testVar = "actual";

        Action act = () => Assert.That(() => testVar == "expected", "Values should match");

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => testVar == "expected") failed.
            Message: Values should match
            Details:
              testVar = "actual"
            """);
    }

    public void That_WithNumericLiterals_SkipsLiteralInDetails()
    {
        // Test various numeric literals are not included in details
        int value = 5;
        double doubleValue = 3.14;
        float floatValue = 2.5f;
        decimal decimalValue = 100.50m;

        Action act = () => Assert.That(() => value == 10 && doubleValue == 2.71 && floatValue == 1.5f && decimalValue == 200.75m);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => value == 10 && doubleValue == 2.71 && floatValue == 1.5f && decimalValue == 200.75m) failed.
            Details:
              decimalValue = 100.50
              doubleValue = 3.14
              floatValue = 2.5
              value = 5
            """);
    }

    [SuppressMessage("Style", "IDE0100:Remove redundant equality", Justification = "Expected use case")]
    public void That_WithBooleanLiterals_SkipsLiteralInDetails()
    {
        // Test boolean literals are not included in details
        bool condition = false;

        Action act = () => Assert.That(() => condition == true);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => condition == true) failed.
            Details:
              condition = False
            """);
    }

    public void That_WithCharacterLiterals_SkipsLiteralInDetails()
    {
        // Test character literals are not included in details
        char letter = 'a';

        Action act = () => Assert.That(() => letter == 'b');

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => letter == 'b') failed.
            Details:
              letter = a
            """);
    }

    public void That_WithNullLiterals_SkipsLiteralInDetails()
    {
        // Test null literals are not included in details
        string? nullableString = "not null";

        Action act = () => Assert.That(() => nullableString == null);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => nullableString == null) failed.
            Details:
              nullableString = "not null"
            """);
    }

    public void That_WithFuncDelegate_SkipsInDetails()
    {
        // Test that Func delegates are not included in details
        Func<int, bool> predicate = x => x > 0;
        int value = -1;

        Action act = () => Assert.That(() => predicate(value));

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => predicate(value)) failed.
            Details:
              value = -1
            """);
    }

    public void That_WithActionDelegate_SkipsInDetails()
    {
        // Test that Action delegates are not included in details
        Action<string> action = Console.WriteLine;
        bool shouldExecute = false;

        Action act = () => Assert.That(() => shouldExecute && action != null);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => shouldExecute && action != null) failed.
            Details:
              shouldExecute = False
            """);
    }

    public void That_WithGenericFuncDelegate_SkipsInDetails()
    {
        // Test that generic Func delegates are not included in details
        Func<string, int, bool> complexFunc = (s, i) => s.Length > i;
        string text = "hi";
        int threshold = 5;

        Action act = () => Assert.That(() => complexFunc(text, threshold));

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => complexFunc(text, threshold)) failed.
            Details:
              text = "hi"
              threshold = 5
            """);
    }

    [SuppressMessage("Style", "IDE0100:Remove redundant equality", Justification = "Expected use case")]
    public void That_WithComplexConstantExpression_HandlesCorrectly()
    {
        // Test complex scenarios with mixed constant types
        const int ConstValue = 100;
        string dynamicValue = "dynamic";
        bool flag = false;

        Action act = () => Assert.That(() => dynamicValue.Length == ConstValue && flag == true);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => dynamicValue.Length == ConstValue && flag == true) failed.
            Details:
              dynamicValue.Length = 7
              flag = False
            """);
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1122:Use string.Empty for empty strings", Justification = "Expected use case")]
    public void That_WithEmptyStringConstant_SkipsInDetails()
    {
        // Test empty string constants are handled correctly
        string value = "non-empty";

        Action act = () => Assert.That(() => value == "");

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => value == "") failed.
            Details:
              value = "non-empty"
            """);
    }

    public void That_WithNegativeNumericLiterals_SkipsInDetails()
    {
        // Test negative numeric literals are properly identified
        int positiveValue = 5;

        Action act = () => Assert.That(() => positiveValue == -10);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => positiveValue == -10) failed.
            Details:
              positiveValue = 5
            """);
    }

    public void That_WithFloatAndDoubleNotation_SkipsInDetails()
    {
        // Test float (f) and double (d) suffixes are handled
        double value = 1.0;

        Action act = () => Assert.That(() => value == 2.5d && value != 3.14f);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => value == 2.5d && value != 3.14f) failed.
            Details:
              value = 1
            """);
    }

    public void That_WithCapturedVariableNamesContainingKeywords_HandlesCorrectly()
    {
        // Test that captured variables with names that might conflict with keywords work
        object @null = new();
        string @true = "false";
        int @false = 1;

        Action act = () => Assert.That(() => @null == null && @true == "true" && @false == 0);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => @null == null && @true == "true" && @false == 0) failed.
            Details:
              false = 1
              null = System.Object
              true = "false"
            """);
    }

    public void That_WithConstExpressionInIndexAccess_FormatsCorrectly()
    {
        // Test constant expressions used in array/indexer access
        int[] array = [1, 2, 3];
        const int Index = 0;
        int dynamicIndex = 1;

        Action act = () => Assert.That(() => array[Index] == 5 && array[dynamicIndex] == 10);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => array[Index] == 5 && array[dynamicIndex] == 10) failed.
            Details:
              array[0] = 1
              array[dynamicIndex] = 2
              dynamicIndex = 1
            """);
    }

    public void That_WithMultipleStringLiterals_OnlyIncludesVariables()
    {
        // Test multiple string literals in complex expression
        string firstName = "John";
        string lastName = "Doe";

        Action act = () => Assert.That(() => firstName == "Jane" && lastName == "Smith");

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => firstName == "Jane" && lastName == "Smith") failed.
            Details:
              firstName = "John"
              lastName = "Doe"
            """);
    }

    [SuppressMessage("Style", "IDE0100:Remove redundant equality", Justification = "Expected use case")]
    public void That_WithMixedLiteralsAndVariables_FiltersCorrectly()
    {
        // Test that only variables are included, not literals of any type
        string name = "Test";
        int age = 25;
        bool isActive = true;
        char grade = 'B';

        Action act = () => Assert.That(() => name == "Admin" && age == 30 && isActive == false && grade == 'A');

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => name == "Admin" && age == 30 && isActive == false && grade == 'A') failed.
            Details:
              age = 25
              grade = B
              isActive = True
              name = "Test"
            """);
    }

    public void That_WithCustomMessageAndLiterals_SkipsLiteralsInDetails()
    {
        // Test custom message with literals
        int count = 3;
        string status = "pending";

        Action act = () => Assert.That(() => count > 5 && status == "completed", "Operation should be ready");

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => count > 5 && status == "completed") failed.
            Message: Operation should be ready
            Details:
              count = 3
              status = "pending"
            """);
    }

    public void That_WithCapturedDecimalLiteral_SkipsInDetails()
    {
        // Test decimal literals with 'm' suffix
        decimal price = 19.99m;
        decimal tax = 2.50m;

        Action act = () => Assert.That(() => price + tax == 25.00m);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => price + tax == 25.00m) failed.
            Details:
              price = 19.99
              tax = 2.50
            """);
    }

    public void That_WithNullConstantAndVariable_OnlyIncludesVariable()
    {
        // Test null constant vs null variable
        string? nullVariable = null;
        string nonNullVariable = "value";

        Action act = () => Assert.That(() => nullVariable != null && nonNullVariable == null);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => nullVariable != null && nonNullVariable == null) failed.
            Details:
              nonNullVariable = "value"
              nullVariable = null
            """);
    }

    public void That_DoesNotEvaluateTwice_WhenAssertionFails()
    {
        var box = new Box();

        // If we evaluate twice, box.GetValueWithSideEffect() is called once on comparison, and once when message for assertion is built.
        // We compare to 0 to force failure.
        Action act = () => Assert.That(() => box.GetValueWithSideEffect() == 0);

        // GetValueWithSideEffect() should report 1, which is the value when we evaluate only once.
        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => box.GetValueWithSideEffect() == 0) failed.
            Details:
              box.GetValueWithSideEffect() = 1
            """);

        // We call again, this should be second call now.
        box.GetValueWithSideEffect().Should().Be(2);
    }

    public void That_DoesNotEvaluateTwice_WhenAssertionFails_NoSideEffect()
    {
        int i = 1;
        Action act = () => Assert.That(() => i + i == 0);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => i + i == 0) failed.
            Details:
              i = 1
            """);
    }

    public void That_DoesEvaluateTwice_WhenMethodIsLeaf()
    {
        var box = new Box();

        // Compare to 0 to force failure.
        Action act = () => Assert.That(() => box.GetValueWithSideEffect() + box.GetValueWithSideEffect() == 0);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => box.GetValueWithSideEffect() + box.GetValueWithSideEffect() == 0) failed.
            Details:
              box.GetValueWithSideEffect() = 1
              box.GetValueWithSideEffect() (2) = 2
            """);
    }

    public void That_FastPathFailure_StillExtractsDetails()
    {
        int x = 3;

        Action act = () => Assert.That(() => x == 5);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => x == 5) failed.
            Details:
              x = 3
            """);
    }

    public void That_FieldAccessWithSideEffectingParent_DoesNotEvaluateTwice()
    {
        var provider = new BoxProvider();

        Action act = () => Assert.That(() => provider.GetBox().Value == 0);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => provider.GetBox().Value == 0) failed.
            Details:
              provider.GetBox().Value = 1
            """);

        provider.Calls.Should().Be(1);
    }

    public void That_AndAlso_ShortCircuits_DoesNotEvaluateRightWhenLeftIsFalse()
    {
        string? s = null;

        // s != null is false; the right operand s.Length > 0 must NOT execute on the assertion
        // path (it would NRE) and the diagnostic walk must not surface "<Failed to evaluate>"
        // for the benign short-circuit.
        Action act = () => Assert.That(() => s != null && s.Length > 0);

        act.Should().Throw<AssertFailedException>()
            .WithMessage("""
            Assert.That(() => s != null && s.Length > 0) failed.
            Details:
              s = null
            """);
    }

    public void That_OrElse_ShortCircuit_PassingAssertion_DoesNotInvokeRight()
    {
        var counter = new SideEffectCounter();
        bool leftTrue = true;

        // Assertion passes because leftTrue is true; counter.Increment() must NOT run.
        Action act = () => Assert.That(() => leftTrue || counter.Increment() > 0);

        act.Should().NotThrow();
        counter.Count.Should().Be(0);
    }

    public void That_Coalesce_ShortCircuit_PassingAssertion_DoesNotInvokeRight()
    {
        var counter = new SideEffectCounter();
        string? value = "hello";

        // Non-null value short-circuits the null-coalescing operator; counter.Increment() must NOT run.
        Action act = () => Assert.That(() => (value ?? counter.Increment().ToString(CultureInfo.InvariantCulture)) == "hello");

        act.Should().NotThrow();
        counter.Count.Should().Be(0);
    }

    public void That_Conditional_PassingAssertion_DoesNotInvokeUnselectedBranch()
    {
        var counter = new SideEffectCounter();
        bool useTrueBranch = true;

        // useTrueBranch picks the IfTrue side; counter.Increment() in IfFalse must NOT run.
        Action act = () => Assert.That(() => (useTrueBranch ? 1 : counter.Increment()) == 1);

        act.Should().NotThrow();
        counter.Count.Should().Be(0);
    }

    public void That_PropagatesUserExceptions_OnSinglePassPath()
    {
        var thrower = new Thrower();

        // The lambda throws InvalidOperationException; previously the single-pass evaluator
        // would swallow it and turn it into a confusing InvalidCastException ("<Failed to evaluate>" -> bool).
        // Now the original exception must surface unchanged.
        Action act = () => Assert.That(() => thrower.Throw());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("boom");
    }

    public void That_NewExpressionWithSideEffectingArgument_DoesNotEvaluateTwice()
    {
        var counter = new SideEffectCounter();

        Action act = () => Assert.That(() => new Wrapper(counter.Increment()).Value == 99);

        act.Should().Throw<AssertFailedException>();
        counter.Count.Should().Be(1);
    }

    public void That_NewArrayWithSideEffectingElement_DoesNotEvaluateTwice()
    {
        var counter = new SideEffectCounter();

        Action act = () => Assert.That(() => new[] { counter.Increment() }.Length == 99);

        act.Should().Throw<AssertFailedException>();
        counter.Count.Should().Be(1);
    }

    public void That_InvocationWithSideEffectingArgument_DoesNotEvaluateTwice()
    {
        var counter = new SideEffectCounter();
        Func<int, int> identity = x => x;

        Action act = () => Assert.That(() => identity(counter.Increment()) == 99);

        act.Should().Throw<AssertFailedException>();
        counter.Count.Should().Be(1);
    }

    public void That_QueryableWithQuotedLambda_FailsAsAssertionFailure()
    {
        int[] items = [1, 2, 3];

        // The Where/Any call uses a quoted lambda; the previous walker tried to unwrap the Quote
        // and produced an InvalidCastException instead of an AssertFailedException.
        Action act = () => Assert.That(() => items.AsQueryable().Any(i => i > 99));

        act.Should().Throw<AssertFailedException>();
    }

    private sealed class SideEffectCounter
    {
        public int Count { get; private set; }

        public int Increment()
        {
            Count++;
            return Count;
        }
    }

    private sealed class Thrower
    {
        public bool Throw() => throw new InvalidOperationException("boom");
    }

    private sealed class Wrapper(int value)
    {
        public int Value { get; } = value;
    }

    private class Box
    {
        private int _c;

        public int GetValueWithSideEffect()
        {
            _c++;
            return _c;
        }
    }

    private sealed class BoxProvider
    {
        public int Calls { get; private set; }

        public ValueBox GetBox()
        {
            Calls++;
            return new ValueBox(Calls);
        }
    }

    private readonly struct ValueBox(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class MutableContainer
    {
#pragma warning disable SA1401 // Fields should be private - intentional: this type tests Expression.Assign on nested members.
        public MutableBox Inner = new() { Value = 0 };
#pragma warning restore SA1401
    }

    private sealed class MutableBox
    {
#pragma warning disable SA1401 // Fields should be private - intentional: this type tests Expression.Assign on a field.
        public int Value;
#pragma warning restore SA1401
    }

    public void That_ManuallyConstructedAssignExpression_SurfacesRhsAndPreservesSideEffect()
    {
        // Construct: (container.Inner.Value = ComputeValue()) < 0   — fails, so message must
        // include enough context for diagnosis AND the assignment side-effect must apply.
        var container = new MutableContainer();
        FieldInfo innerFi = typeof(MutableContainer).GetField(nameof(MutableContainer.Inner))!;
        FieldInfo valueFi = typeof(MutableBox).GetField(nameof(MutableBox.Value))!;

        MemberExpression innerAccess = Expression.Field(Expression.Constant(container), innerFi);
        MemberExpression valueAccess = Expression.Field(innerAccess, valueFi);
        MethodCallExpression rhs = Expression.Call(typeof(MutableBoxHelper).GetMethod(nameof(MutableBoxHelper.ComputeValue))!);
        BinaryExpression assign = Expression.Assign(valueAccess, rhs);
        BinaryExpression body = Expression.LessThan(assign, Expression.Constant(0));
        var lambda = Expression.Lambda<Func<bool>>(body);

        Action act = () => Assert.That(lambda);

        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        // Assert on the method name only (not the parenthesized rendering) so that future
        // renderer tweaks (e.g., dropping/adding parentheses for nullary calls) don't break
        // this regression test.
        ex.Message.Should().Contain(nameof(MutableBoxHelper.ComputeValue));
        // The Inner member of the receiver chain should surface in the failure details.
        ex.Message.Should().Contain("Inner");
        // The assignment side-effect must apply.
        container.Inner.Value.Should().Be(42);
    }

    // Regression for Assign double-evaluation: the RHS of Expression.Assign was evaluated
    // once during the bottom-up pass, but the generic "rebuild and compile" path then
    // produced an invalid Assign(Constant, Constant) that the catch sentineled, and the
    // parent LessThan was re-invoked from the original tree — re-running ComputeValue a
    // second time. Assign is now special-cased so the RHS runs exactly once.
    public void That_ManuallyConstructedAssign_EvaluatesRhsExactlyOnce()
    {
        var container = new MutableContainer();
        FieldInfo innerFi = typeof(MutableContainer).GetField(nameof(MutableContainer.Inner))!;
        FieldInfo valueFi = typeof(MutableBox).GetField(nameof(MutableBox.Value))!;

        CountingComputeValue.Calls = 0;

        MemberExpression innerAccess = Expression.Field(Expression.Constant(container), innerFi);
        MemberExpression valueAccess = Expression.Field(innerAccess, valueFi);
        MethodCallExpression rhs = Expression.Call(typeof(CountingComputeValue).GetMethod(nameof(CountingComputeValue.Get))!);
        BinaryExpression assign = Expression.Assign(valueAccess, rhs);
        BinaryExpression body = Expression.LessThan(assign, Expression.Constant(0));
        var lambda = Expression.Lambda<Func<bool>>(body);

        Action act = () => Assert.That(lambda);

        act.Should().Throw<AssertFailedException>();
        CountingComputeValue.Calls.Should().Be(1);
        container.Inner.Value.Should().Be(42);
    }

    // Regression: a compound assignment whose only side effect is the assignment itself (no
    // method or property reads triggering single-pass) was taking the side-effect-free fast
    // path. The fast path ran the assignment once, then EvaluateAllSubExpressions ran it
    // again on the failure-diagnostic path. RequiresSinglePassEvaluation now recognizes
    // assignment node types so these expressions always take the single-pass evaluator.
    public void That_CompoundAssignmentOnField_AppliesExactlyOnceOnFailure()
    {
        var container = new MutableContainer();
        FieldInfo innerFi = typeof(MutableContainer).GetField(nameof(MutableContainer.Inner))!;
        FieldInfo valueFi = typeof(MutableBox).GetField(nameof(MutableBox.Value))!;

        MemberExpression innerAccess = Expression.Field(Expression.Constant(container), innerFi);
        MemberExpression valueAccess = Expression.Field(innerAccess, valueFi);
        BinaryExpression addAssign = Expression.AddAssign(valueAccess, Expression.Constant(7));
        BinaryExpression body = Expression.LessThan(addAssign, Expression.Constant(0));
        var lambda = Expression.Lambda<Func<bool>>(body);

        Action act = () => Assert.That(lambda);

        act.Should().Throw<AssertFailedException>();
        // Exactly one += 7 must apply — not two.
        container.Inner.Value.Should().Be(7);
    }

    // Regression: assigning to a member whose receiver chain has a side effect (e.g.,
    // `provider.GetBox().Value = 42`) previously ran the receiver method twice because the
    // rebuild kept the original (unevaluated) Left in place. The Left's sub-children are
    // now substituted with cached constants while the writable MemberExpression wrapper is
    // preserved, so the receiver runs exactly once.
    public void That_AssignToFieldOfSideEffectingReceiver_EvaluatesReceiverExactlyOnce()
    {
        var provider = new MutableBoxProvider();

        MethodInfo getBoxMethod = typeof(MutableBoxProvider).GetMethod(nameof(MutableBoxProvider.GetBox))!;
        MethodCallExpression getBoxCall = Expression.Call(Expression.Constant(provider), getBoxMethod);
        FieldInfo valueFi = typeof(MutableBox).GetField(nameof(MutableBox.Value))!;
        MemberExpression valueAccess = Expression.Field(getBoxCall, valueFi);
        BinaryExpression assign = Expression.Assign(valueAccess, Expression.Constant(42));
        BinaryExpression body = Expression.LessThan(assign, Expression.Constant(0));
        var lambda = Expression.Lambda<Func<bool>>(body);

        Action act = () => Assert.That(lambda);
        act.Should().Throw<AssertFailedException>();

        // Receiver method (GetBox) must run exactly once.
        provider.Calls.Should().Be(1);
        provider.Box.Value.Should().Be(42);
    }

    private sealed class MutableBoxProvider
    {
        public int Calls { get; private set; }

        public MutableBox Box { get; } = new();

        public MutableBox GetBox()
        {
            Calls++;
            return Box;
        }
    }

    private static class CountingComputeValue
    {
#pragma warning disable SA1401 // Fields should be private - intentional: test counter must be readable from the test assembly.
        public static int Calls;
#pragma warning restore SA1401

        public static int Get()
        {
            Calls++;
            return 42;
        }
    }

    // Regression: a member whose static type is Func/Action but whose runtime value is null should
    // still appear in the failure details. Filtering must remain runtime-typed (via the cached
    // value's GetType()) rather than static-typed at analysis time.
    private sealed class HolderWithFuncField
    {
#pragma warning disable SA1401 // Fields should be private - intentional: test fixture exposing a delegate-typed field.
        public Func<int>? Callback;
#pragma warning restore SA1401
    }

    public void That_NullDelegateTypedMember_StillAppearsInDetails()
    {
        var holder = new HolderWithFuncField { Callback = null };

        Action act = () => Assert.That(() => holder.Callback != null);

        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("Callback");
        ex.Message.Should().Contain("null");
    }

    // ---- Object-typed sub-expressions: locks down current behavior when two side-effecting
    // method calls return the same mutable reference. The cache stores reference values, so by
    // the time details are extracted both slots point to the post-mutation object; with
    // first-occurrence-by-name and "same value -> dedupe" semantics in TryAddExpressionValue,
    // only one entry surfaces. This is a known UX limitation worth pinning down with a test.
    public void That_TwoCallsReturningSameMutatedReference_DeduplicatesInDetails()
    {
        var box = new BoxOfShapes();

        // Same reference returned twice but mutated between calls; the != check therefore returns
        // false (same reference) so the assertion fails.
        Action act = () => Assert.That(() => box.GetValueWithSideEffect() != box.GetValueWithSideEffect());

        act.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assert.That(() => box.GetValueWithSideEffect() != box.GetValueWithSideEffect()) failed.
                Details:
                  box.GetValueWithSideEffect() = Shape: Circle
                """);
    }

    private sealed class Shape
    {
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"Shape: {Name}";
    }

    private sealed class BoxOfShapes
    {
        private Shape? _c;

        public Shape GetValueWithSideEffect()
        {
            if (_c is null)
            {
                _c = new Shape { Name = "Square" };
                return _c;
            }

            _c.Name = "Circle";
            return _c;
        }
    }
}

internal static class MutableBoxHelper
{
    public static int ComputeValue() => 42;
}
