// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class TreeNodeFilterTests : TestBase
{
    public TreeNodeFilterTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void MatchAllFilter_MatchesAnyPath()
    {
        TreeNodeFilter filter = new("/**");
        Assert.IsTrue(filter.MatchesFilter("/Any/Path", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Path/Of/The/Test", new PropertyBag()));
    }

    public void MatchAllFilter_MatchesSubpaths()
    {
        TreeNodeFilter filter = new("/Path/**");
        Assert.IsTrue(filter.MatchesFilter("/Path/Of/The/Test", new PropertyBag()));
    }

    public void MatchAllFilter_Invalid() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/A(&B)"));

    public void MatchAllFilter_DoNotAllowInMiddleOfFilter() => Assert.Throws<ArgumentException>(() => _ = new TreeNodeFilter("/**/Path"));

    public void MatchWildcard_MatchesSubstrings()
    {
        TreeNodeFilter filter = new("/*.UnitTests");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.FunctionalTests", new PropertyBag()));
    }

    public void EscapeSequences_SupportsWildcard()
    {
        TreeNodeFilter filter = new("/*.\\*UnitTests");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.*UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.*UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.AUnitTests", new PropertyBag()));
    }

    public void EscapeSequences_SupportsParentheses()
    {
        TreeNodeFilter filter = new("/*.\\(UnitTests\\)");
        Assert.IsTrue(filter.MatchesFilter("/ProjectA.(UnitTests)", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.(UnitTests)", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.(UnitTests", new PropertyBag()));
    }

    public void EscapeSequences_ThrowsIfLastCharIsAnEscapeChar() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/*.\\(UnitTests\\)\\"));

    public void OrExpression_WorksForLiteralStrings()
    {
        TreeNodeFilter filter = new("/A|B");
        Assert.IsTrue(filter.MatchesFilter("/A", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/B", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/C", new PropertyBag()));
    }

    public void AndExpression()
    {
        TreeNodeFilter filter = new("/(*.UnitTests)&(*ProjectB*)");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Hello.ProjectB.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectC.UnitTests", new PropertyBag()));
        Assert.IsFalse(filter.MatchesFilter("/ProjectC.UnitTests.SomeExtension", new PropertyBag()));
    }

    public void Parentheses_EnsuresOrdering()
    {
        TreeNodeFilter filter = new("/((*.UnitTests)&(*ProjectB*))|C");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/Hello.ProjectB.UnitTests", new PropertyBag()));
        Assert.IsTrue(filter.MatchesFilter("/C", new PropertyBag()));

        // Would be `true` if the expr would be interpreted as `*.UnitTests & (*.ProjectB* | C)`
        Assert.IsFalse(filter.MatchesFilter("/C.UnitTests", new PropertyBag()));
    }

    public void Parenthesis_DisallowSeparatorInside() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/(A/B)"));

    public void Parameters_PropertyCheck()
    {
        TreeNodeFilter filter = new("/*.UnitTests[Tag=Fast]");
        Assert.IsTrue(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Fast"))));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag(new KeyValuePairStringProperty("Tag", "Slow"))));
        Assert.IsFalse(filter.MatchesFilter("/ProjectB.UnitTests", new PropertyBag()));
    }

    public void Parameters_DisallowAtStart() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/[Tag=Fast]"));

    public void Parameters_DisallowEmpty() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/Path[]"));

    public void Parameters_DisallowMultiple() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/Path[Prop=2][Prop=B]"));

    public void Parameters_DisallowNested() => Assert.Throws<InvalidOperationException>(() => _ = new TreeNodeFilter("/Path[X=[Y=1]]"));

    [Arguments("/A/B", "/A/B", true)]
    [Arguments("/A/B", "/A%2FB", false)]
    [Arguments("/A%2FB", "/A/B", false)]
    [Arguments("/A%2FB", "/A%2FB", true)]
    public void TestNodeFilterNeedsUrlEncodingOfSlashes(string filter, string nodePath, bool isMatched)
    {
        TreeNodeFilter filterInstance = new(filter);
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

    [Arguments("/A/B[ValueWithSlash=Some/thing]", "/A/B", true)]
    [Arguments("/A/B[ValueWithSlash=Some%2Fthing]", "/A/B", false)]
    [Arguments("/A/B[Other/thing=KeyWithSlash]", "/A/B", true)]
    [Arguments("/A/B[Other%2Fthing=KeyWithSlash]", "/A/B", false)]
    [Arguments("/A%2FB[ValueWithSlash=Some/thing]", "/A%2FB", true)]
    [Arguments("/A%2FB[ValueWithSlash=Some%2Fthing]", "/A%2FB", false)]
    [Arguments("/A%2FB[Other/thing=KeyWithSlash]", "/A%2FB", true)]
    [Arguments("/A%2FB[Other%2Fthing=KeyWithSlash]", "/A%2FB", false)]
    public void PropertiesDoNotNeedUrlEncodingOfSlashes(string filter, string nodePath, bool isMatched)
    {
        TreeNodeFilter filterInstance = new(filter);
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
}
