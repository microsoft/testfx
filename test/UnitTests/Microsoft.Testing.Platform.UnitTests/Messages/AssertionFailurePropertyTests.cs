// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages.UnitTests;

[TestClass]
public sealed class AssertionFailurePropertyTests
{
    [TestMethod]
    public void ToStringIsCorrect()
        => Assert.AreEqual(
            "AssertionFailureProperty { Expected = 5, Actual = 2 }",
            new AssertionFailureProperty("5", "2").ToString());

    [TestMethod]
    public void Constructor_WhenOnlyExpectedIsProvided_KeepsActualNull()
    {
        var property = new AssertionFailureProperty("5", null);

        Assert.AreEqual("5", property.Expected);
        Assert.IsNull(property.Actual);
    }

    [TestMethod]
    public void Constructor_WhenOnlyActualIsProvided_KeepsExpectedNull()
    {
        var property = new AssertionFailureProperty(null, "2");

        Assert.IsNull(property.Expected);
        Assert.AreEqual("2", property.Actual);
    }

    [TestMethod]
    public void Constructor_WhenBothValuesAreNull_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => new AssertionFailureProperty(null, null));

    [TestMethod]
    public void Constructor_AllowsEmptyStrings()
    {
        // An empty string is a meaningful rendering (e.g. an empty expected string), unlike null.
        var property = new AssertionFailureProperty(string.Empty, string.Empty);

        Assert.IsEmpty(property.Expected!);
        Assert.IsEmpty(property.Actual!);
    }

    [TestMethod]
    public void Equals_WhenSameValues_ReturnsTrue()
    {
        var property = new AssertionFailureProperty("5", "2");
        var other = new AssertionFailureProperty("5", "2");

        Assert.AreEqual(property, other);
        Assert.AreEqual(property.GetHashCode(), other.GetHashCode());
    }

    [TestMethod]
    public void Equals_WhenDifferentValues_ReturnsFalse()
    {
        var property = new AssertionFailureProperty("5", "2");

        Assert.AreNotEqual(property, new AssertionFailureProperty("5", "3"));
        Assert.AreNotEqual(property, new AssertionFailureProperty("4", "2"));
    }

    [TestMethod]
    public void Equals_WhenOtherIsNullOrDifferentType_ReturnsFalse()
    {
        var property = new AssertionFailureProperty("5", "2");

        Assert.IsFalse(property.Equals(null));
        Assert.IsFalse(property.Equals((object?)"5"));
    }
}
