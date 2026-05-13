// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertionValueRendererTests : TestContainer
{
    public void RenderValue_Null_ReturnsNull() =>
        AssertionValueRenderer.RenderValue(null).Should().Be("null");

    public void RenderValue_EmptyString_ReturnsQuotedEmpty() =>
        AssertionValueRenderer.RenderValue(string.Empty).Should().Be("\"\"");

    public void RenderValue_SimpleString_ReturnsQuotedString() =>
        AssertionValueRenderer.RenderValue("hello world").Should().Be("\"hello world\"");

    public void RenderValue_StringWithEmbeddedQuotes_EscapesQuotes() =>
        AssertionValueRenderer.RenderValue("she said \"hello\"").Should().Be("\"she said \\\"hello\\\"\"");

    public void RenderValue_StringWithNewline_EscapesNewline() =>
        AssertionValueRenderer.RenderValue("line1\nline2").Should().Be("\"line1\\nline2\"");

    public void RenderValue_StringWithCarriageReturn_EscapesCR() =>
        AssertionValueRenderer.RenderValue("line1\rline2").Should().Be("\"line1\\rline2\"");

    public void RenderValue_StringWithTab_EscapesTab() =>
        AssertionValueRenderer.RenderValue("col1\tcol2").Should().Be("\"col1\\tcol2\"");

    public void RenderValue_StringWithNullChar_EscapesNull() =>
        AssertionValueRenderer.RenderValue("abc\0def").Should().Be("\"abc\\0def\"");

    public void RenderValue_StringWithBackslash_EscapesBackslash() =>
        AssertionValueRenderer.RenderValue("path\\to\\file").Should().Be("\"path\\\\to\\\\file\"");

    public void RenderValue_WhitespaceOnlyString_ReturnsQuotedWhitespace() =>
        AssertionValueRenderer.RenderValue("   ").Should().Be("\"   \"");

    public void RenderValue_Integer_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(42).Should().Be("42");

    public void RenderValue_NegativeInteger_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(-7).Should().Be("-7");

    public void RenderValue_Double_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(3.14).Should().Be(3.14.ToString(CultureInfo.CurrentCulture));

    public void RenderValue_BoolTrue_ReturnsLowercase() =>
        AssertionValueRenderer.RenderValue(true).Should().Be("true");

    public void RenderValue_BoolFalse_ReturnsLowercase() =>
        AssertionValueRenderer.RenderValue(false).Should().Be("false");

    public void RenderValue_ListOfInts_ReturnsJsonArray()
    {
        var list = new List<int> { 1, 2, 3 };
        AssertionValueRenderer.RenderValue(list).Should().Be("[1, 2, 3]");
    }

    public void RenderValue_EmptyList_ReturnsEmptyBrackets() =>
        AssertionValueRenderer.RenderValue(new List<int>()).Should().Be("[]");

    public void RenderValue_ListOfStrings_ReturnsQuotedElements()
    {
        var list = new List<string> { "apple", "cherry", "date" };
        AssertionValueRenderer.RenderValue(list).Should().Be("[\"apple\", \"cherry\", \"date\"]");
    }

    public void RenderValue_ListWithNull_RendersNullElement()
    {
        var list = new List<string?> { "apple", null, "date" };
        AssertionValueRenderer.RenderValue(list).Should().Be("[\"apple\", null, \"date\"]");
    }

    public void RenderValue_ObjectWithToString_ReturnsToString() =>
        AssertionValueRenderer.RenderValue(new ObjectWithCustomToString("my-object")).Should().Be("my-object");

    public void RenderValue_Char_ReturnsSingleQuoted() =>
        AssertionValueRenderer.RenderValue('a').Should().Be("'a'");

    public void RenderValue_CharNewline_ReturnsEscaped() =>
        AssertionValueRenderer.RenderValue('\n').Should().Be("'\\n'");

    private sealed class ObjectWithCustomToString
    {
        private readonly string _value;

        public ObjectWithCustomToString(string value)
        {
            _value = value;
        }

        public override string ToString() => _value;
    }
}
