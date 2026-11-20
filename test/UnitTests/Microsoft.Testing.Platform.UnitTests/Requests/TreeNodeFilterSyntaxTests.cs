// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TreeNodeFilterSyntaxTests
{
    // Every syntax error of the filter grammar is reported with InvalidOperationException. Each row puts an operator,
    // a parenthesis or a property bracket where the grammar does not allow it.
    [TestMethod]

    // A binary operator or a property bracket with nothing before it.
    [DataRow("|A(B)")]
    [DataRow("&A(B)")]
    [DataRow("[x(y)]")]

    // A binary operator that has no right operand.
    [DataRow("/X/(A|)")]
    [DataRow("/X/Y[a|]")]
    [DataRow("!(A&)")]
    [DataRow("A(A|)")]
    [DataRow("A|B&/")]

    // A binary operator, or a property bracket, right after a path separator.
    [DataRow("/X/A|[x=y]")]
    [DataRow("/A/|B")]
    [DataRow("/A/&B")]
    [DataRow("/A/[x=y]")]

    // A property bracket right after an opening or a closing parenthesis.
    [DataRow("/A([x=y])")]
    [DataRow("/(A)[x=y]")]
    [DataRow("(A)[x=y]")]
    [DataRow("/A/(B)[x=y]")]

    // An operator, a closing bracket or an opening bracket right after '['.
    [DataRow("/A/B[|x=y]")]
    [DataRow("/A/B[&x=y]")]
    [DataRow("A/B[]")]
    [DataRow("/A/B[[x=y]]")]

    // '=' or '!=' without a right operand, or '!=' without a property name on its left.
    [DataRow("/A/B[x=]")]
    [DataRow("/A/(x=)")]
    [DataRow("/A/B[x!=]")]
    [DataRow("/A[!=B]")]
    [DataRow("/A[x=y]!=B")]

    // A property bracket right after '=': it is rejected even when a trailing separator hides the dangling '=' operators.
    [DataRow("A=[A=/")]

    // An opening parenthesis that is never closed: the path separator after it does not close it.
    [DataRow("(A|B/C")]
    public void Constructor_WithMalformedFilter_ThrowsInvalidOperationException(string filter)
        => Assert.ThrowsExactly<InvalidOperationException>(() => _ = new TreeNodeFilter(filter));

    [TestMethod]
    public void Constructor_WithMatchAllInAlternativeBeforeTheLastSegment_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => _ = new TreeNodeFilter("/(A|**)/B"));

    // Inside '[' a '/' belongs to the value, so a path separator can only reach a value of the filter when the '[' is
    // never closed. Such a filter is malformed and must not be accepted. Which exception reports it depends on how far
    // the parser gets before it notices the dangling '[' (the validation of the value or the operator stack), so only the
    // family of the exception is asserted: the syntax and value errors of the grammar.
    [TestMethod]
    public void Constructor_WithPathSeparatorInsideAnUnclosedPropertyBracket_IsRejected()
    {
        Exception exception = Assert.Throws<Exception>(() => _ = new TreeNodeFilter("A[/A&/"));

        Assert.IsTrue(exception is ArgumentException or InvalidOperationException, exception.ToString());
    }

    [TestMethod]

    // Operators on the first segment, alone or followed by other segments.
    [DataRow("A|B", "/A", "", true)]
    [DataRow("A|B", "/B", "", true)]
    [DataRow("A|B", "/C", "", false)]
    [DataRow("*A*&*B*", "/xABx", "", true)]
    [DataRow("*A*&*B*", "/xAx", "", false)]
    [DataRow("A|B/C", "/A/C", "", true)]
    [DataRow("A|B/C", "/B/C", "", true)]
    [DataRow("A|B/C", "/C/C", "", false)]
    [DataRow("A|B/C", "/A/D", "", false)]
    [DataRow("*A*&*B*/C", "/AB/C", "", true)]
    [DataRow("*A*&*B*/C", "/A/C", "", false)]

    // An '&' and a '|' in the same segment, followed by another segment: the path separator that ends the segment applies
    // both operators to that segment. It must neither drop the '|' nor turn the operands of the '&' into segments.
    [DataRow("A|B&C/D", "/A/D", "", true)]
    [DataRow("/A|B*&*C/D", "/A/D", "", true)]
    [DataRow("/A|B*&*C/D", "/BXC/D", "", true)]
    [DataRow("/A|B*&*C/D", "/BX/D", "", false)]
    [DataRow("/A|B*&*C/D", "/A/BXC/D", "", false)]
    [DataRow("/A*&*1|B*&*2/C", "/AX1/C", "", true)]
    [DataRow("/A*&*1|B*&*2/C", "/BX2/C", "", true)]
    [DataRow("/A*&*1|B*&*2/C", "/AX2/C", "", false)]
    [DataRow("/A*&*1|B*&*2/C", "/AX1/BX2/C", "", false)]
    [DataRow("/A/B|C*&*D/E", "/A/B/E", "", true)]
    [DataRow("/A/B|C*&*D/E", "/A/CXD/E", "", true)]
    [DataRow("/A/B|C*&*D/E", "/A/B/CXD/E", "", false)]

    // Three operands of the same operator are one n-ary expression.
    [DataRow("/(A|B|C)", "/A", "", true)]
    [DataRow("/(A|B|C)", "/B", "", true)]
    [DataRow("/(A|B|C)", "/C", "", true)]
    [DataRow("/(A|B|C)", "/D", "", false)]
    [DataRow("/(*A*&*B*&*C*)", "/xABCx", "", true)]
    [DataRow("/(*A*&*B*&*C*)", "/xABx", "", false)]
    [DataRow("/(*A*&*B*&*C*)", "/xACx", "", false)]
    [DataRow("/(*A*&*B*&*C*)", "/xBCx", "", false)]

    // The match-all value is accepted as an alternative of the last segment.
    [DataRow("/B/(A|**)", "/B/x", "", true)]
    [DataRow("/B/(A|**)", "/B/A", "", true)]
    [DataRow("/B/(A|**)", "/C/x", "", false)]

    // Properties: one per segment, several segments with properties, alternatives, conjunctions and negation.
    [DataRow("/A[Tag=B]/C", "/A/C", "Tag=B", true)]
    [DataRow("/A[Tag=B]/C", "/A/C", "", false)]
    [DataRow("/A[Tag=B]/C", "/A/C", "Tag=X", false)]
    [DataRow("/A[Tag=B]/C", "/A/D", "Tag=B", false)]
    [DataRow("/A[x=y]/B[z=w]", "/A/B", "x=y;z=w", true)]
    [DataRow("/A[x=y]/B[z=w]", "/A/B", "x=y", false)]
    [DataRow("/A[x=y]/B[z=w]", "/A/B", "z=w", false)]
    [DataRow("/A[(x=y)|(z=w)]/B", "/A/B", "z=w", true)]
    [DataRow("/A[(x=y)|(z=w)]/B", "/A/B", "x=y", true)]
    [DataRow("/A[(x=y)|(z=w)]/B", "/A/B", "", false)]
    [DataRow("/A[(x=y)|(z=w)]/B", "/A/B", "x=q", false)]
    [DataRow("/A[(Tag=X)|(Tag=Y)]", "/A", "Tag=X", true)]
    [DataRow("/A[(Tag=X)|(Tag=Y)]", "/A", "Tag=Y", true)]
    [DataRow("/A[(Tag=X)|(Tag=Y)]", "/A", "Tag=Z", false)]
    [DataRow("/A[(Tag=X)|(Tag=Y)]", "/A", "", false)]
    [DataRow("/A[(Tag=X)&(Other=Y)]", "/A", "Tag=X;Other=Y", true)]
    [DataRow("/A[(Tag=X)&(Other=Y)]", "/A", "Tag=X", false)]
    [DataRow("/A[Tag!=B]", "/A", "Tag=B", false)]
    [DataRow("/A[Tag!=B]", "/A", "Tag=X", true)]
    [DataRow("/A[Tag!=B]", "/A", "", true)]

    // '!' is a negation only right after '('; anywhere else it is an ordinary character.
    [DataRow("!", "/!", "", true)]
    [DataRow("!", "/a", "", false)]
    [DataRow("!A", "/!A", "", true)]
    [DataRow("!A", "/A", "", false)]
    [DataRow("/A!", "/A!", "", true)]
    [DataRow("/A!", "/A", "", false)]
    [DataRow("(A!)", "/A!", "", true)]
    [DataRow("(A!)", "/A", "", false)]
    [DataRow("(!A)", "/B", "", true)]
    [DataRow("(!A)", "/A", "", false)]
    [DataRow("/!A", "/!A", "", true)]

    // A path with more segments than the filter only matches when the filter ends with the match-all value.
    [DataRow("/A/B", "/A/B/C", "", false)]
    [DataRow("/A/B[Tag=x]", "/A/B/C", "Tag=x", false)]
    [DataRow("/A/**", "/A/B/C", "", true)]
    [DataRow("/**", "/A/B", "", true)]
    public void MatchesFilter_ForValidSyntax_ReturnsExpectedResult(string filter, string path, string properties, bool expected)
    {
        TreeNodeFilter treeNodeFilter = new(filter);

        Assert.AreEqual(expected, treeNodeFilter.MatchesFilter(path, CreatePropertyBag(properties)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A")]
    [DataRow("A/B")]
    public void MatchesFilter_WithPathNotStartingWithSeparator_ThrowsArgumentException(string path)
    {
        TreeNodeFilter filter = new("/A/B");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => _ = filter.MatchesFilter(path, new PropertyBag()));

        Assert.AreEqual("testNodeFullPath", exception.ParamName);
    }

    // The properties are given as "key=value" pairs separated by ';' (an empty string means no property).
    private static PropertyBag CreatePropertyBag(string properties)
    {
        if (properties.Length == 0)
        {
            return new PropertyBag();
        }

        List<IProperty> items = [];
        foreach (string pair in properties.Split(';'))
        {
            int separatorIndex = pair.IndexOf('=');
            items.Add(new TestMetadataProperty(pair.Substring(0, separatorIndex), pair.Substring(separatorIndex + 1)));
        }

        return new PropertyBag(items);
    }
}
