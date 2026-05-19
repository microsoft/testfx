// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ResponseFileHelperTests
{
    [TestMethod]
    public void SplitCommandLine_EmptyString_ReturnsEmpty()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine(string.Empty)];

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void SplitCommandLine_WhitespaceOnly_ReturnsEmpty()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("   \t  ")];

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void SplitCommandLine_NullInput_ThrowsNullReferenceException()
        => Assert.Throws<NullReferenceException>(() => ResponseFileHelper.SplitCommandLine(null!).ToArray());

    [TestMethod]
    public void SplitCommandLine_SingleToken_ReturnsSingleElement()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("hello")];

        Assert.HasCount(1, result);
        Assert.AreEqual("hello", result[0]);
    }

    [TestMethod]
    public void SplitCommandLine_MultipleTokens_ReturnsAllElements()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("one two three")];

        Assert.HasCount(3, result);
        Assert.AreEqual("one", result[0]);
        Assert.AreEqual("two", result[1]);
        Assert.AreEqual("three", result[2]);
    }

    [TestMethod]
    public void SplitCommandLine_LeadingAndTrailingWhitespace_IsIgnored()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("  hello world  ")];

        Assert.HasCount(2, result);
        Assert.AreEqual("hello", result[0]);
        Assert.AreEqual("world", result[1]);
    }

    [TestMethod]
    public void SplitCommandLine_MultipleSpacesBetweenTokens_TokensSplitCorrectly()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("a   b")];

        Assert.HasCount(2, result);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
    }

    [TestMethod]
    public void SplitCommandLine_QuotedStringWithSpace_ReturnsSingleToken()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("\"hello world\"")];

        Assert.HasCount(1, result);
        Assert.AreEqual("hello world", result[0]);
    }

    [TestMethod]
    public void SplitCommandLine_QuotedAndUnquotedTokens_SplitsCorrectly()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("--opt \"some value\" other")];

        Assert.HasCount(3, result);
        Assert.AreEqual("--opt", result[0]);
        Assert.AreEqual("some value", result[1]);
        Assert.AreEqual("other", result[2]);
    }

    [TestMethod]
    public void SplitCommandLine_MultipleQuotedTokens_EachUnquotedSeparately()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("\"foo bar\" \"baz qux\"")];

        Assert.HasCount(2, result);
        Assert.AreEqual("foo bar", result[0]);
        Assert.AreEqual("baz qux", result[1]);
    }

    [TestMethod]
    public void SplitCommandLine_OptionWithArgument_ReturnsSeparateTokens()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("--filter TestClass1")];

        Assert.HasCount(2, result);
        Assert.AreEqual("--filter", result[0]);
        Assert.AreEqual("TestClass1", result[1]);
    }

    [TestMethod]
    public void SplitCommandLine_DashPrefixedOptions_PreserveDashes()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("--timeout 30 -v")];

        Assert.HasCount(3, result);
        Assert.AreEqual("--timeout", result[0]);
        Assert.AreEqual("30", result[1]);
        Assert.AreEqual("-v", result[2]);
    }

    [TestMethod]
    public void SplitCommandLine_QuotedStringContainingMultipleSpaces_PreservesInternalSpaces()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("\"a  b  c\"")];

        Assert.HasCount(1, result);
        Assert.AreEqual("a  b  c", result[0]);
    }

    [TestMethod]
    public void SplitCommandLine_UnclosedQuote_ReturnsEmpty()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("\"hello world")];

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void SplitCommandLine_TabSeparatedTokens_SplitsOnTab()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("one\ttwo")];

        Assert.HasCount(2, result);
        Assert.AreEqual("one", result[0]);
        Assert.AreEqual("two", result[1]);
    }

    [TestMethod]
    public void SplitCommandLine_RealWorldResponseFileLine_ParsesCorrectly()
    {
        string[] result = [.. ResponseFileHelper.SplitCommandLine("--filter \"FullyQualifiedName~MyNamespace.MyClass\" --timeout 60")];

        Assert.HasCount(4, result);
        Assert.AreEqual("--filter", result[0]);
        Assert.AreEqual("FullyQualifiedName~MyNamespace.MyClass", result[1]);
        Assert.AreEqual("--timeout", result[2]);
        Assert.AreEqual("60", result[3]);
    }
}
