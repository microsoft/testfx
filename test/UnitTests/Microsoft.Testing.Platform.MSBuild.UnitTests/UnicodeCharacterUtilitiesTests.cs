// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.MSBuild;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class UnicodeCharacterUtilitiesTests
{
    [TestMethod]
    [DataRow('a')]
    [DataRow('Z')]
    [DataRow('_')]
    [DataRow('\u00C5')]
    [DataRow('\u00E9')]
    [DataRow('\u01C5')]
    [DataRow('\u02B0')]
    [DataRow('\u4E2D')]
    [DataRow('\u2167')]
    public void IsIdentifierStartCharacter_ValidCharacter_ReturnsTrue(char value)
        => Assert.IsTrue(UnicodeCharacterUtilities.IsIdentifierStartCharacter(value));

    [TestMethod]
    [DataRow('0')]
    [DataRow('9')]
    [DataRow('@')]
    [DataRow('[')]
    [DataRow('`')]
    [DataRow('{')]
    [DataRow('\u007F')]
    [DataRow('.')]
    [DataRow('-')]
    [DataRow(' ')]
    [DataRow('\u0301')]
    [DataRow('\u203F')]
    [DataRow('\u200C')]
    public void IsIdentifierStartCharacter_InvalidCharacter_ReturnsFalse(char value)
        => Assert.IsFalse(UnicodeCharacterUtilities.IsIdentifierStartCharacter(value));

    [TestMethod]
    [DataRow('a')]
    [DataRow('Z')]
    [DataRow('_')]
    [DataRow('0')]
    [DataRow('9')]
    [DataRow('\u00C5')]
    [DataRow('\u4E2D')]
    [DataRow('\u0661')]
    [DataRow('\u0301')]
    [DataRow('\u0903')]
    [DataRow('\u203F')]
    [DataRow('\u200C')]
    public void IsIdentifierPartCharacter_ValidCharacter_ReturnsTrue(char value)
        => Assert.IsTrue(UnicodeCharacterUtilities.IsIdentifierPartCharacter(value));

    [TestMethod]
    [DataRow('@')]
    [DataRow('[')]
    [DataRow('`')]
    [DataRow('{')]
    [DataRow('\u007F')]
    [DataRow('.')]
    [DataRow('-')]
    [DataRow(' ')]
    [DataRow('/')]
    [DataRow('\u00A9')]
    [DataRow('\u20DD')]
    public void IsIdentifierPartCharacter_InvalidCharacter_ReturnsFalse(char value)
        => Assert.IsFalse(UnicodeCharacterUtilities.IsIdentifierPartCharacter(value));
}
