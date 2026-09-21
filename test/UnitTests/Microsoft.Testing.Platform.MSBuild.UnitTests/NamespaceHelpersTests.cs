// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.MSBuild;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class NamespaceHelpersTests
{
    [TestMethod]
    [DataRow("Microsoft.Testing.Platform")]
    [DataRow("_Root.Namespace123")]
    [DataRow("\u00C5ngstr\u00F6m.\u0394\u03BF\u03BA\u03B9\u03BC\u03AE.\u4E2D\u6587")]
    public void ToSafeNamespace_ValidNamespace_ReturnsValue(string value)
        => Assert.AreEqual(value, NamespaceHelpers.ToSafeNamespace(value));

    [TestMethod]
    [DataRow("1Namespace", "_1Namespace")]
    [DataRow("Root.2Namespace", "Root._2Namespace")]
    [DataRow("My-Namespace", "My_Namespace")]
    [DataRow("My Namespace", "My_Namespace")]
    [DataRow("My/Namespace", "My_Namespace")]
    public void ToSafeNamespace_InvalidCharacter_ReturnsReplacedValue(string value, string expected)
        => Assert.AreEqual(expected, NamespaceHelpers.ToSafeNamespace(value));

    [TestMethod]
    [DataRow(".Namespace", "_Namespace")]
    [DataRow("Namespace.", "Namespace_")]
    [DataRow("Namespace..Child", "Namespace..Child")]
    [DataRow("Namespace...", "Namespace.._")]
    [DataRow(".", "_")]
    public void ToSafeNamespace_DotBoundary_ReturnsExpectedValue(string value, string expected)
        => Assert.AreEqual(expected, NamespaceHelpers.ToSafeNamespace(value));

    [TestMethod]
    [DataRow("", "")]
    [DataRow(" \t\r\n", "")]
    [DataRow("  Namespace  ", "Namespace")]
    [DataRow("\tMy Namespace\r\n", "My_Namespace")]
    public void ToSafeNamespace_Whitespace_ReturnsExpectedValue(string value, string expected)
        => Assert.AreEqual(expected, NamespaceHelpers.ToSafeNamespace(value));

    [TestMethod]
    [DataRow("A\U0001F600B", "A_B")]
    [DataRow("A\U00010400B", "A_B")]
    [DataRow("\U0001F6001Name", "_1Name")]
    [DataRow("Root.\U0001F600.Child", "Root._.Child")]
    public void ToSafeNamespace_SurrogatePair_ReplacesPairAndProcessesFollowingCharacter(string value, string expected)
        => Assert.AreEqual(expected, NamespaceHelpers.ToSafeNamespace(value));
}
