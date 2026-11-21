// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ArtifactFileNameSanitizerTests
{
    [TestMethod]
    [DataRow('"')]
    [DataRow('<')]
    [DataRow('>')]
    [DataRow('|')]
    [DataRow(':')]
    [DataRow('*')]
    [DataRow('?')]
    [DataRow('\\')]
    [DataRow('/')]
    [DataRow('@')]
    [DataRow('(')]
    [DataRow(')')]
    [DataRow('^')]
    [DataRow(' ')]
    [DataRow('\0')]
    [DataRow('\t')]
    [DataRow('\u001F')]
    public void ReplaceInvalidFileNameChars_ReplacesEachInvalidCharacter(char invalid)
        => Assert.AreEqual("a_b", ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars($"a{invalid}b"));

    [TestMethod]
    [DataRow('!')]
    [DataRow('#')]
    [DataRow('=')]
    [DataRow('-')]
    [DataRow('.')]
    [DataRow('~')]
    public void ReplaceInvalidFileNameChars_KeepsValidCharacters(char valid)
        => Assert.AreEqual($"a{valid}b", ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars($"a{valid}b"));

    [TestMethod]
    public void ReplaceInvalidFileNameChars_OnlyWhitespaceAfterReplacement_Throws()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars("\u00A0"));

        Assert.AreEqual("fileName", exception.ParamName);
        Assert.Contains("is empty after sanitizing", exception.Message);
    }

    [TestMethod]
    public void ReplaceInvalidFileNameChars_EmptyInput_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars(string.Empty));

    [TestMethod]
    public void ReplaceInvalidFileNameChars_TrimsTrailingNonAsciiWhitespace()
        => Assert.AreEqual("abc", ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars("abc\u00A0"));

    [TestMethod]
    [DataRow("CON", "_CON")]
    [DataRow("con.txt", "_con.txt")]
    [DataRow("COM1", "_COM1")]
    [DataRow("LPT9.log", "_LPT9.log")]
    [DataRow("CLOCK$", "_CLOCK$")]
    [DataRow("CONSOLE", "CONSOLE")]
    [DataRow("COM0", "COM0")]
    public void ReplaceInvalidFileNameChars_PrefixesReservedNames(string input, string expected)
        => Assert.AreEqual(expected, ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars(input));
}
