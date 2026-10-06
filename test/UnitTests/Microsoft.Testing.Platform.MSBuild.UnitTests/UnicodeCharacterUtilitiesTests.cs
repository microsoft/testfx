// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.MSBuild;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class UnicodeCharacterUtilitiesTests
{
    [TestMethod]
    [DataRow('A')]
    [DataRow('a')]
    [DataRow('Z')]
    [DataRow('z')]
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
    [DataRow('A')]
    [DataRow('a')]
    [DataRow('Z')]
    [DataRow('z')]
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

[TestClass]
public sealed class TimeSpanParserTests
{
    [TestMethod]
    public void TryParse_BareNumber_UsesMilliseconds()
    {
        Assert.IsTrue(TryParse("200", out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), result);
    }

    [TestMethod]
    public void TryParse_ExplicitSuffix_ParsesInvariantNumber()
    {
        Assert.IsTrue(TryParse("1.5s", out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromSeconds(1.5), result);
    }

    [TestMethod]
    [DataRow("2ms")]
    [DataRow("2milliseconds")]
    public void TryParse_MillisecondAliases_UseMilliseconds(string value)
    {
        Assert.IsTrue(TryParse(value, out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromMilliseconds(2), result);
    }

    [TestMethod]
    public void TryParse_MinuteSuffix_UsesMinutes()
    {
        Assert.IsTrue(TryParse("2m", out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromMinutes(2), result);
    }

    [TestMethod]
    public void TryParse_HourSuffix_UsesHours()
    {
        Assert.IsTrue(TryParse("2h", out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromHours(2), result);
    }

    [TestMethod]
    public void TryParse_DaySuffix_UsesDays()
    {
        Assert.IsTrue(TryParse("2d", out TimeSpan result));
        Assert.AreEqual(TimeSpan.FromDays(2), result);
    }

    private static bool TryParse(string value, out TimeSpan result)
    {
        Type parser = typeof(ConfigurationFileTask).Assembly.GetType(
            "Microsoft.Testing.Platform.Helpers.TimeSpanParser",
            throwOnError: true)!;
        MethodInfo method = parser.GetMethod(
            "TryParse",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(string), typeof(TimeSpan).MakeByRefType()],
            modifiers: null)!;
        object?[] arguments = [value, default(TimeSpan)];
        bool parsed = (bool)method.Invoke(null, arguments)!;
        result = (TimeSpan)arguments[1]!;
        return parsed;
    }
}
