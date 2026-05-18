// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineOptionTests
{
    [TestMethod]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var option = new CommandLineOption("my-option", "Description text.", ArgumentArity.ExactlyOne, isHidden: false);

        Assert.AreEqual("my-option", option.Name);
        Assert.AreEqual("Description text.", option.Description);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
    }

    [TestMethod]
    public void Constructor_WithIsHiddenTrue_SetsHiddenProperty()
    {
        var option = new CommandLineOption("opt", "Desc.", ArgumentArity.Zero, isHidden: true);

        Assert.IsTrue(option.IsHidden);
    }

    [TestMethod]
    public void Constructor_NullName_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => _ = new CommandLineOption(null!, "Desc.", ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_EmptyName_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption(string.Empty, "Desc.", ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_WhitespaceName_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption("   ", "Desc.", ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_NullDescription_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => _ = new CommandLineOption("opt", null!, ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_EmptyDescription_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption("opt", string.Empty, ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_WhitespaceDescription_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption("opt", "   ", ArgumentArity.Zero, false));

    [TestMethod]
    public void Constructor_InvalidArityMaxLessThanMin_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption("opt", "Desc.", new ArgumentArity(2, 1), false));

    [TestMethod]
    [DataRow("opt!")]
    [DataRow("opt@")]
    [DataRow("opt#")]
    [DataRow("opt$")]
    [DataRow("opt space")]
    public void Constructor_NameWithInvalidCharacters_ThrowsArgumentException(string invalidName)
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new CommandLineOption(invalidName, "Desc.", ArgumentArity.Zero, false));

    [TestMethod]
    [DataRow("opt")]
    [DataRow("my-option")]
    [DataRow("option123")]
    [DataRow("?")]
    [DataRow("a-b-c")]
    public void Constructor_NameWithValidCharacters_DoesNotThrow(string validName)
    {
        var option = new CommandLineOption(validName, "Desc.", ArgumentArity.Zero, false);
        Assert.AreEqual(validName, option.Name);
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var opt1 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);
        var opt2 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);

        Assert.IsTrue(opt1.Equals(opt2));
        Assert.IsTrue(opt1.Equals((object)opt2));
    }

    [TestMethod]
    public void Equals_DifferentName_ReturnsFalse()
    {
        var opt1 = new CommandLineOption("opt1", "Desc.", ArgumentArity.ExactlyOne, false);
        var opt2 = new CommandLineOption("opt2", "Desc.", ArgumentArity.ExactlyOne, false);

        Assert.IsFalse(opt1.Equals(opt2));
    }

    [TestMethod]
    public void Equals_DifferentDescription_ReturnsFalse()
    {
        var opt1 = new CommandLineOption("opt", "Desc1.", ArgumentArity.ExactlyOne, false);
        var opt2 = new CommandLineOption("opt", "Desc2.", ArgumentArity.ExactlyOne, false);

        Assert.IsFalse(opt1.Equals(opt2));
    }

    [TestMethod]
    public void Equals_DifferentArity_ReturnsFalse()
    {
        var opt1 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);
        var opt2 = new CommandLineOption("opt", "Desc.", ArgumentArity.ZeroOrOne, false);

        Assert.IsFalse(opt1.Equals(opt2));
    }

    [TestMethod]
    public void Equals_DifferentIsHidden_ReturnsFalse()
    {
        var opt1 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, isHidden: false);
        var opt2 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, isHidden: true);

        Assert.IsFalse(opt1.Equals(opt2));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        var opt = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);

        Assert.IsFalse(opt.Equals((CommandLineOption?)null));
        Assert.IsFalse(opt.Equals((object?)null));
    }

    [TestMethod]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var opt1 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);
        var opt2 = new CommandLineOption("opt", "Desc.", ArgumentArity.ExactlyOne, false);

        Assert.AreEqual(opt1.GetHashCode(), opt2.GetHashCode());
    }
}
