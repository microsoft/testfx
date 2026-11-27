// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.InvocationLifetime;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.InvocationLifetime;

[TestClass]
public sealed class InvocationLifetimeJsonTests
{
    [TestMethod]
    public void AppendString_EscapesQuotesBackslashesAndControlCharacters()
    {
        StringBuilder builder = new();

        InvocationLifetimeJson.AppendString(builder, "a\"b\\c\n\u0001");

        Assert.AreEqual("\"a\\\"b\\\\c\\n\\u0001\"", builder.ToString());
    }

    [TestMethod]
    public void TryParseStringArray_RoundTripsEscapedValues()
    {
        StringBuilder builder = new("[");
        InvocationLifetimeJson.AppendString(builder, "x\"y");
        builder.Append(',');
        InvocationLifetimeJson.AppendString(builder, "tab\tz");
        builder.Append(']');

        Assert.AreSequenceEqual(["x\"y", "tab\tz"], InvocationLifetimeJson.TryParseStringArray(builder.ToString())!);
    }

    [TestMethod]
    public void TryParseStringArray_AcceptsWhitespaceAndStandardEscapes()
        => Assert.AreSequenceEqual(["a/b", "\u00e9", string.Empty], InvocationLifetimeJson.TryParseStringArray(""" [ "a\/b" , "\u00E9", "" ] """)!);

    [TestMethod]
    public void TryParseStringArray_AcceptsEmptyArray()
        => Assert.IsEmpty(InvocationLifetimeJson.TryParseStringArray("[]")!);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a;b")]
    [DataRow("[\"a\"")]
    [DataRow("[\"a\",]")]
    [DataRow("[\"a\"] x")]
    [DataRow("[1]")]
    [DataRow("[\"\\x\"]")]
    [DataRow("[\"\\u12\"]")]
    public void TryParseStringArray_ReturnsNullForInvalidInput(string? json)
        => Assert.IsNull(InvocationLifetimeJson.TryParseStringArray(json));
}
