// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestGroup]
public class ConditionTests
{
    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithNullProperty_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => Array.Empty<string>());

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithNonEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithNullProperty_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => Array.Empty<string>());

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithNonEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_EmptyStringContainsOperator_WithNullProperty_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Contains, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_EmptyStringContainsOperator_WithNonEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Contains, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotContainsOperator_WithNullProperty_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotContains, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotContainsOperator_WithNonEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.NotContains, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_NonEmptyStringEqualsOperator_WithMatchingValue_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, "CategoryA");
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_NonEmptyStringEqualsOperator_WithNonMatchingValue_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Equal, "CategoryA");
        bool result = condition.Evaluate(propertyName => new[] { "CategoryB" });

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Parse_EmptyValueAfterEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory=");

        condition.Name.Should().Be("TestCategory");
        condition.Operation.Should().Be(Operation.Equal);
        condition.Value.Should().Be(string.Empty);
    }

    [TestMethod]
    public void Parse_WhitespaceValueAfterEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory=   ");

        condition.Name.Should().Be("TestCategory");
        condition.Operation.Should().Be(Operation.Equal);
        condition.Value.Should().Be(string.Empty);
    }

    [TestMethod]
    public void Parse_EmptyValueWithNotEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory!=");

        condition.Name.Should().Be("TestCategory");
        condition.Operation.Should().Be(Operation.NotEqual);
        condition.Value.Should().Be(string.Empty);
    }

    [TestMethod]
    public void Parse_EmptyValueWithContains_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory~");

        condition.Name.Should().Be("TestCategory");
        condition.Operation.Should().Be(Operation.Contains);
        condition.Value.Should().Be(string.Empty);
    }

    [TestMethod]
    public void Parse_EmptyValueWithNotContains_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory!~");

        condition.Name.Should().Be("TestCategory");
        condition.Operation.Should().Be(Operation.NotContains);
        condition.Value.Should().Be(string.Empty);
    }
}
