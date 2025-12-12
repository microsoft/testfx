// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public class ConditionTests
{
    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithNullProperty_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => Array.Empty<string>());

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringEqualsOperator_WithNonEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Equal, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithNullProperty_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => Array.Empty<string>());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotEqualsOperator_WithNonEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.NotEqual, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringContainsOperator_WithNullProperty_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Contains, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringContainsOperator_WithNonEmptyArray_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Contains, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotContainsOperator_WithNullProperty_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.NotContains, string.Empty);
        bool result = condition.Evaluate(propertyName => null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Evaluate_EmptyStringNotContainsOperator_WithNonEmptyArray_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.NotContains, string.Empty);
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_NonEmptyStringEqualsOperator_WithMatchingValue_ShouldReturnTrue()
    {
        var condition = new Condition("TestCategory", Operation.Equal, "CategoryA");
        bool result = condition.Evaluate(propertyName => new[] { "CategoryA" });

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Evaluate_NonEmptyStringEqualsOperator_WithNonMatchingValue_ShouldReturnFalse()
    {
        var condition = new Condition("TestCategory", Operation.Equal, "CategoryA");
        bool result = condition.Evaluate(propertyName => new[] { "CategoryB" });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Parse_EmptyValueAfterEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory=");

        Assert.AreEqual("TestCategory", condition.Name);
        Assert.AreEqual(Operation.Equal, condition.Operation);
        Assert.AreEqual(string.Empty, condition.Value);
    }

    [TestMethod]
    public void Parse_WhitespaceValueAfterEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory=   ");

        Assert.AreEqual("TestCategory", condition.Name);
        Assert.AreEqual(Operation.Equal, condition.Operation);
        Assert.AreEqual(string.Empty, condition.Value);
    }

    [TestMethod]
    public void Parse_EmptyValueWithNotEquals_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory!=");

        Assert.AreEqual("TestCategory", condition.Name);
        Assert.AreEqual(Operation.NotEqual, condition.Operation);
        Assert.AreEqual(string.Empty, condition.Value);
    }

    [TestMethod]
    public void Parse_EmptyValueWithContains_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory~");

        Assert.AreEqual("TestCategory", condition.Name);
        Assert.AreEqual(Operation.Contains, condition.Operation);
        Assert.AreEqual(string.Empty, condition.Value);
    }

    [TestMethod]
    public void Parse_EmptyValueWithNotContains_ShouldCreateConditionWithEmptyValue()
    {
        Condition condition = Condition.Parse("TestCategory!~");

        Assert.AreEqual("TestCategory", condition.Name);
        Assert.AreEqual(Operation.NotContains, condition.Operation);
        Assert.AreEqual(string.Empty, condition.Value);
    }
}
