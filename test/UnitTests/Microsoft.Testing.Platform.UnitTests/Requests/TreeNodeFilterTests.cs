﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.UnitTests.Helpers;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TreeNodeFilterTests
{
    [TestMethod]
    public void MatchAllFilter_MatchesAnyPath()
    {
        TreeNodeFilter filter = BuildFilter("/**");
        Assert.IsTrue(filter.MatchesFilter("/Any/Path", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Path/Of/The/Test", new PropertyBag()));
    }

    [TestMethod]
    public void MatchAllFilter_MatchesSubpaths()
    {
        TreeNodeFilter filter = BuildFilter("/Path/**");
        Assert.IsTrue(filter.MatchesFilter("/Path/Of/The/Test", new PropertyBag()));
    }

    [TestMethod]
    public void MatchAllFilter_Invalid() => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/A(&B)"));

    [TestMethod]
    public void MatchAllFilter_DoNotAllowInMiddleOfFilter() => Assert.ThrowsException<ArgumentException>(() => _ = BuildFilter("/**/Path"));

    [TestMethod]
    public void MatchWildcard_MatchesSubstrings()
    {
        TreeNodeFilter filter = BuildFilter("/*.UnitTests");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.FunctionalTests", new PropertyBag()));
    }

    [TestMethod]
    public void EscapeSequences_SupportsWildcard()
    {
        TreeNodeFilter filter = BuildFilter("/*.\\*UnitTests");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.*UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.*UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.AUnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void EscapeSequences_SupportsParentheses()
    {
        TreeNodeFilter filter = BuildFilter("/*.\\(UnitTests\\)");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.(UnitTests)", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.(UnitTests)", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.(UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void EscapeSequences_ThrowsIfLastCharIsAnEscapeChar() => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/*.\\(UnitTests\\)\\"));

    [TestMethod]
    public void OrExpression_WorksForLiteralStrings()
    {
        TreeNodeFilter filter = BuildFilter("/A|B");
        Assert.IsTrue(filter.MatchesFilter("/A", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/B", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/C", new PropertyBag()));
    }

    [TestMethod]
    public void AndExpression()
    {
        TreeNodeFilter filter = BuildFilter("/(*.UnitTests)&(*ProjectB*)");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Hello.ProjectB.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectC.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectC.UnitTests.SomeExtension", new PropertyBag()));
    }

    [TestMethod]
    public void Parentheses_EnsuresOrdering()
    {
        TreeNodeFilter filter = BuildFilter("/((*.UnitTests)&(*ProjectB*))|C");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Hello.ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/C", new PropertyBag()));

        // Would be `true` if the expr would be interpreted as `*.UnitTests & (*.ProjectB* | C)`
        Assert.IsFalse(filter.MatchesFilter("/C.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parenthesis_DisallowSeparatorInside()
        => Assert.ThrowsException<InvalidOperationException>(() => BuildFilter("/(A/B)"));

    [TestMethod]
    public void Parameters_PropertyCheck()
    {
        TreeNodeFilter filter = BuildFilter("/*.UnitTests[Tag=Fast]");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parameters_NegatedPropertyCheck()
    {
        TreeNodeFilter filter = BuildFilter("/*.UnitTests[Tag!=Fast]");
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parameters_NegatedPropertyCheckWithMatchAllFilter()
    {
        TreeNodeFilter filter = BuildFilter("/**[Tag!=Fast]");
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parameters_NegatedPropertyCheckCombinedWithAnd()
    {
        TreeNodeFilter filter = BuildFilter("/*.UnitTests[(Tag!=Fast)&(Tag!=Slow)]");
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parameters_NegatedPropertyCheckCombinedWithOr()
    {
        TreeNodeFilter filter = BuildFilter("/*.UnitTests[(Tag!=Fast)|(Tag!=Slow)]");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    [TestMethod]
    public void Parameters_DisallowAtStart()
        => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/[Tag=Fast]"));

    [TestMethod]
    public void Parameters_DisallowEmpty()
        => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/Path[]"));

    [TestMethod]
    public void Parameters_DisallowMultiple()
        => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/Path[Prop=2][Prop=B]"));

    [TestMethod]
    public void Parameters_DisallowNested()
        => Assert.ThrowsException<InvalidOperationException>(() => _ = BuildFilter("/Path[X=[Y=1]]"));

    [DataRow("/A/B", "/A/B", true)]
    [DataRow("/A/B", "/A%2FB", false)]
    [DataRow("/A%2FB", "/A/B", false)]
    [DataRow("/A%2FB", "/A%2FB", true)]
    [TestMethod]
    public void TestNodeFilterNeedsUrlEncodingOfSlashes(string filter, string nodePath, bool isMatched)
    {
        TreeNodeFilter filterInstance = BuildFilter(filter);
        PropertyBag nodeProperties = new();

        if (isMatched)
        {
            Assert.IsTrue(filterInstance.MatchesFilter(nodePath, nodeProperties));
        }
        else
        {
            Assert.IsFalse(filterInstance.MatchesFilter(nodePath, nodeProperties));
        }
    }

    [DataRow("/A/B[ValueWithSlash=Some/thing]", "/A/B", true)]
    [DataRow("/A/B[ValueWithSlash=Some%2Fthing]", "/A/B", false)]
    [DataRow("/A/B[Other/thing=KeyWithSlash]", "/A/B", true)]
    [DataRow("/A/B[Other%2Fthing=KeyWithSlash]", "/A/B", false)]
    [DataRow("/A%2FB[ValueWithSlash=Some/thing]", "/A%2FB", true)]
    [DataRow("/A%2FB[ValueWithSlash=Some%2Fthing]", "/A%2FB", false)]
    [DataRow("/A%2FB[Other/thing=KeyWithSlash]", "/A%2FB", true)]
    [DataRow("/A%2FB[Other%2Fthing=KeyWithSlash]", "/A%2FB", false)]
    [TestMethod]
    public void PropertiesDoNotNeedUrlEncodingOfSlashes(string filter, string nodePath, bool isMatched)
    {
        TreeNodeFilter filterInstance = BuildFilter(filter);
        PropertyBag nodeProperties = new(
            new KeyValuePairStringProperty("Tag", "Fast"),
            new KeyValuePairStringProperty("ValueWithSlash", "Some/thing"),
            new KeyValuePairStringProperty("Other/thing", "KeyWithSlash"));

        if (isMatched)
        {
            Assert.IsTrue(filterInstance.MatchesFilter(nodePath, nodeProperties));
        }
        else
        {
            Assert.IsFalse(filterInstance.MatchesFilter(nodePath, nodeProperties));
        }
    }

    [TestMethod]
    public void MatchAllFilterWithPropertyExpression()
    {
        TreeNodeFilter filter = BuildFilter("/**[A=B]");
        Assert.IsTrue(filter.MatchesFilter("/A/B/C/D", new PropertyBag(new KeyValuePairStringProperty("A", "B"))));
        Assert.IsFalse(filter.MatchesFilter("/A/B/C/D", new PropertyBag(new KeyValuePairStringProperty("A", "C"))));
    }

    [TestMethod]
    public void MatchAllFilterSubpathWithPropertyExpression()
    {
        TreeNodeFilter filter = BuildFilter("/A/**[A=B]");
        Assert.IsTrue(filter.MatchesFilter("/A/B/C/D", new PropertyBag(new KeyValuePairStringProperty("A", "B"))));
        Assert.IsFalse(filter.MatchesFilter("/B/A/C/D", new PropertyBag(new KeyValuePairStringProperty("A", "B"))));
    }

    [TestMethod]
    public void MatchAllFilterWithPropertyExpression_DoNotAllowInMiddleOfFilter() => Assert.ThrowsException<ArgumentException>(() => _ = BuildFilter("/**/Path[A=B]"));

    private TreeNodeFilter BuildFilter(string filterQuery) => new(new TestCommandLineOptions(new Dictionary<string, string[]>
    {
        [TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter] = [filterQuery],
    }));
}
