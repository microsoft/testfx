// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineOptionArgumentValidatorTests
{
    [TestMethod]
    [DataRow("on")]
    [DataRow("ON")]
    [DataRow("On")]
    [DataRow("true")]
    [DataRow("TRUE")]
    [DataRow("enable")]
    [DataRow("ENABLE")]
    [DataRow("1")]
    public void IsOnValue_AcceptedValues_ReturnsTrue(string value)
    {
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsOnValue(value));
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsValidBooleanArgument(value));
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOffValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsAutoValue(value));
    }

    [TestMethod]
    [DataRow("off")]
    [DataRow("OFF")]
    [DataRow("Off")]
    [DataRow("false")]
    [DataRow("FALSE")]
    [DataRow("disable")]
    [DataRow("DISABLE")]
    [DataRow("0")]
    public void IsOffValue_AcceptedValues_ReturnsTrue(string value)
    {
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsOffValue(value));
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsValidBooleanArgument(value));
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOnValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsAutoValue(value));
    }

    [TestMethod]
    [DataRow("auto")]
    [DataRow("AUTO")]
    [DataRow("Auto")]
    public void IsAutoValue_AcceptedValues_ReturnsTrue(string value)
    {
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsAutoValue(value));
        Assert.IsTrue(CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsValidBooleanArgument(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOnValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOffValue(value));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("yes")]
    [DataRow("no")]
    [DataRow("2")]
    [DataRow("enabled")]
    [DataRow("disabled")]
    public void InvalidValues_ReturnsFalse(string value)
    {
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOnValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsOffValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsAutoValue(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsValidBooleanArgument(value));
        Assert.IsFalse(CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(value));
    }
}
