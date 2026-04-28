// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PasteArgumentsTests
{
    [TestMethod]
    public void AppendArgument_SimpleArgument_AppendsWithoutQuoting()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "simple");
        Assert.AreEqual("simple", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_EmptyString_ProducesEmptyQuotes()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, string.Empty);
        Assert.AreEqual("\"\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_ArgumentWithSpaces_WrapsInQuotes()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "hello world");
        Assert.AreEqual("\"hello world\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_ArgumentWithTab_WrapsInQuotes()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "hello\tworld");
        Assert.AreEqual("\"hello\tworld\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_ArgumentWithQuote_EscapesQuote()
    {
        // Input: a"b  →  "a\"b"
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a\"b");
        Assert.AreEqual("\"a\\\"b\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_ArgumentWithQuoteAndSpaces_EscapesQuote()
    {
        // Input: say "hi"  →  "say \"hi\""
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "say \"hi\"");
        Assert.AreEqual("\"say \\\"hi\\\"\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_BackslashNotBeforeQuoteOrEnd_NotDoubled()
    {
        // Input (has space so goes complex): "a\b c"  →  "a\b c"
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a\\b c");
        Assert.AreEqual("\"a\\b c\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_BackslashAtEndOfArgument_Doubled()
    {
        // Input: "a b\"  (a space b backslash)  →  "a b\\"
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a b\\");
        Assert.AreEqual("\"a b\\\\\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_TwoBackslashesAtEndOfArgument_AllDoubled()
    {
        // Input: "a b\\"  (a space b 2xbackslash) →  "a b\\\\"
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a b\\\\");
        Assert.AreEqual("\"a b\\\\\\\\\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_BackslashBeforeQuote_DoublesBackslashAndEscapesQuote()
    {
        // Input: a \\"b  (a space backslash quote b) →  "a \\\"b"
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a \\\"b");
        Assert.AreEqual("\"a \\\\\\\"b\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_TwoBackslashesBeforeQuote_DoublesAndEscapesQuote()
    {
        // Input: a \\\"b (a space 2xbackslash quote b)
        // 2 backslashes before quote: (2*2)+1=5 backslashes output + escaped quote
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "a \\\\\"b");
        Assert.AreEqual("\"a \\\\\\\\\\\"b\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_PathWithBackslashes_SimpleArgument()
    {
        // No spaces or quotes → simple path: backslashes not modified
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "C:\\path\\to\\file.txt");
        Assert.AreEqual("C:\\path\\to\\file.txt", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_PathWithSpaces_WrapsInQuotes()
    {
        // Spaces → complex path, trailing backslash must be doubled
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "C:\\Program Files\\");
        Assert.AreEqual("\"C:\\Program Files\\\\\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_SecondCall_AddsSeparatorSpace()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "first");
        PasteArguments.AppendArgument(sb, "second");
        Assert.AreEqual("first second", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_MultipleArguments_ProducesCorrectCommandLine()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "C:\\Program Files\\app.exe");
        PasteArguments.AppendArgument(sb, "--output");
        PasteArguments.AppendArgument(sb, "C:\\My Documents\\result.txt");
        Assert.AreEqual("\"C:\\Program Files\\app.exe\" --output \"C:\\My Documents\\result.txt\"", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_SimpleArgumentWithBackslashes_NoQuotingNeeded()
    {
        // Backslashes with no spaces or quotes → simple path, no doubling
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "path\\to\\dir");
        Assert.AreEqual("path\\to\\dir", sb.ToString());
    }

    [TestMethod]
    public void AppendArgument_WhitespaceOnly_WrapsInQuotes()
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, "   ");
        Assert.AreEqual("\"   \"", sb.ToString());
    }
}
