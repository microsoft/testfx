// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ValueExpressionTests
{
    // The value of an expression is a regular expression that has to match the whole input, ignoring the case. The
    // optimized paths (empty pattern, wildcards only, plain ASCII literal) must behave exactly like the regular expression.
    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("", "x", false)]
    [DataRow("", "xa", false)]
    [DataRow("", "abc", false)]
    [DataRow(".", "", false)]
    [DataRow(".", "x", true)]
    [DataRow(".", "k", true)]
    [DataRow(".", "*", true)]
    [DataRow(".", "xa", false)]
    [DataRow(".", "..", false)]
    [DataRow("..", "", false)]
    [DataRow("..", "x", false)]
    [DataRow("..", "xy", true)]
    [DataRow("..", "abc", false)]
    [DataRow(".a", "zz", false)]
    [DataRow(".a", "xa", true)]
    [DataRow(".a", "XA", true)]
    [DataRow("\\*", "*", true)]
    [DataRow("\\*", "x", false)]
    [DataRow("\\*", "abc", false)]
    [DataRow("\\*", "", false)]
    public void IsMatch_WithRegexPattern_MatchesTheWholeInput(string pattern, string input, bool expected)
        => Assert.AreEqual(expected, new ValueExpression(pattern).IsMatch(input));

    // '.' does not match a newline, so an input starting with one cannot be matched by wildcards only. A '$' matches
    // before a final newline, so a lone newline still matches the empty pattern and two newlines match a literal one.
    [TestMethod]
    public void IsMatch_WithNewlineAsFirstCharacter_FollowsRegexSemantics()
    {
        Assert.IsFalse(new ValueExpression(".*").IsMatch("\nA"));
        Assert.IsFalse(new ValueExpression(".*.*").IsMatch("\nA"));
        Assert.IsFalse(new ValueExpression(".*.*.*").IsMatch("\nA"));
        Assert.IsTrue(new ValueExpression(string.Empty).IsMatch("\n"));
        Assert.IsTrue(new ValueExpression("\\n").IsMatch("\n\n"));
    }

    [TestMethod]
    [DataRow("k", true)]
    [DataRow("K", true)]
    [DataRow("\u212A", true, DisplayName = "Kelvin sign input")]
    [DataRow("x", false)]
    public void IsMatch_WithKelvinSignPattern_MatchesTheAsciiLetterK(string input, bool expected)
        => Assert.AreEqual(expected, new ValueExpression("\u212A").IsMatch(input));

    // Literals outside of the ASCII range go through the regular expression: the result must be the one of a case
    // insensitive regular expression, whatever the case mapping of the runtime says about the characters.
    [TestMethod]
    [DataRow("\u03F4", "\u03B8", DisplayName = "Greek capital theta symbol pattern, small theta input")]
    [DataRow("\u03F4", "\u0398", DisplayName = "Greek capital theta symbol pattern, capital theta input")]
    [DataRow("\u03B8", "\u03D1", DisplayName = "Greek small theta pattern, theta symbol input")]
    [DataRow("\u03B8", "\u03F4", DisplayName = "Greek small theta pattern, capital theta symbol input")]
    [DataRow("\u00DF", "\u1E9E", DisplayName = "Sharp s pattern, capital sharp s input")]
    [DataRow("\u0130", "i", DisplayName = "Dotted capital I pattern, ASCII i input")]
    [DataRow("\uD801\uDC28", "\uD801\uDC00", DisplayName = "Deseret small long I pattern, capital long I input")]
    public void IsMatch_WithNonAsciiLiteral_AgreesWithCaseInsensitiveRegex(string pattern, string input)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            bool expected = new Regex($"^{pattern}$", RegexOptions.IgnoreCase).IsMatch(input);

            Assert.AreEqual(expected, new ValueExpression(pattern).IsMatch(input));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void IsMatch_WhenTheRegexIsBuilt_RestoresTheCultureOfTheCallingThread()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            ValueExpression expression = new("a.*");

            // The first match builds the regular expression with the culture of the expression, not the one of the caller.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            bool isMatch = expression.IsMatch("abc");

            Assert.IsTrue(isMatch);
            Assert.AreEqual("fr-FR", CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void MatchesFilter_WithEscapedAsterisk_MatchesOnlyTheAsteriskCharacter()
    {
        TreeNodeFilter filter = new("/\\*");

        Assert.IsTrue(filter.MatchesFilter("/*", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/x", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/abc", new PropertyBag()));
    }

    [TestMethod]
    public void MatchesFilter_WithWildcardSegment_DoesNotMatchANameStartingWithNewline()
    {
        TreeNodeFilter filter = new("/*");

        Assert.IsFalse(filter.MatchesFilter("/\nA", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/A\n", new PropertyBag()));
    }

    [TestMethod]
    public void MatchesFilter_WithNewlineInFilter_MatchesLikeRegexAtTheEndOfTheName()
    {
        TreeNodeFilter filter = new("/\n");

        Assert.IsTrue(filter.MatchesFilter("/\n", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/\n\n", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/\n\n\n", new PropertyBag()));
    }
}
