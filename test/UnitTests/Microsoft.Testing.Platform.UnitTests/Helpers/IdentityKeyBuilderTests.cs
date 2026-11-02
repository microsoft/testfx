// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class IdentityKeyBuilderTests
{
    [DataRow(null, "-1:")]
    [DataRow("", "0:")]
    [DataRow("a:b\u001f", "4:a:b\u001f")]
    [TestMethod]
    public void AppendLengthPrefixedComponent_EncodesComponent(string? component, string expected)
    {
        var builder = new StringBuilder();

        IdentityKeyBuilder.AppendLengthPrefixedComponent(builder, component);

        Assert.AreEqual(expected, builder.ToString());
    }

    [TestMethod]
    public void AppendLengthPrefixedComponent_PreventsAmbiguousConcatenation()
    {
        string first = BuildIdentity("A", "B:C");
        string second = BuildIdentity("A:B", "C");

        Assert.AreNotEqual(first, second);
    }

    private static string BuildIdentity(params string?[] components)
    {
        var builder = new StringBuilder();
        foreach (string? component in components)
        {
            IdentityKeyBuilder.AppendLengthPrefixedComponent(builder, component);
        }

        return builder.ToString();
    }
}
