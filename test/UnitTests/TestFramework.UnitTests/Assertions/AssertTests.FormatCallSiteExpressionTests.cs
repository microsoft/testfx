// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void FormatCallSiteExpression_SingleArg_NormalExpression_ReturnsExpression()
        => Assert.FormatCallSiteExpression("Assert.X", "value").Should().Be("Assert.X(value)");

    public void FormatCallSiteExpression_SingleArg_NullOrWhitespaceExpression_ReturnsNull()
    {
        Assert.FormatCallSiteExpression("Assert.X", string.Empty).Should().BeNull();
        Assert.FormatCallSiteExpression("Assert.X", "   ").Should().BeNull();
    }

    public void FormatCallSiteExpression_SingleArg_MultilineExpression_UsesPlaceholder()
    {
        Assert.FormatCallSiteExpression("Assert.X", "foo\nbar").Should().Be("Assert.X(<value>)");
        Assert.FormatCallSiteExpression("Assert.X", "foo\r\nbar").Should().Be("Assert.X(<value>)");
        Assert.FormatCallSiteExpression("Assert.X", "foo\rbar").Should().Be("Assert.X(<value>)");
    }

    public void FormatCallSiteExpression_TwoArgs_NormalExpressions_ReturnsExpressions()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: "a", expression2: "b").Should().Be("Assert.X(a, b)");

    public void FormatCallSiteExpression_TwoArgs_BothEmpty_ReturnsNull()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: string.Empty, expression2: "   ").Should().BeNull();

    public void FormatCallSiteExpression_TwoArgs_FirstEmpty_UsesPlaceholderForFirst()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: string.Empty, expression2: "b").Should().Be("Assert.X(<arg1>, b)");

    public void FormatCallSiteExpression_TwoArgs_SecondEmpty_UsesPlaceholderForSecond()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: "a", expression2: string.Empty).Should().Be("Assert.X(a, <arg2>)");

    public void FormatCallSiteExpression_TwoArgs_FirstMultiline_UsesPlaceholderForFirst()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: "a\nb", expression2: "c").Should().Be("Assert.X(<arg1>, c)");

    public void FormatCallSiteExpression_TwoArgs_SecondMultiline_UsesPlaceholderForSecond()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: "a", expression2: "b\nc").Should().Be("Assert.X(a, <arg2>)");

    public void FormatCallSiteExpression_TwoArgs_BothMultiline_UsesBothPlaceholders()
        => Assert.FormatCallSiteExpression("Assert.X", expression1: "a\nb", expression2: "c\nd").Should().Be("Assert.X(<arg1>, <arg2>)");
}
