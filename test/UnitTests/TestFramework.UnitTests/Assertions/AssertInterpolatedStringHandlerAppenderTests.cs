// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

public sealed class AssertInterpolatedStringHandlerAppenderTests : TestContainer
{
    public void AppendLiteral_AndGenericFormattedValue_AppendExpectedText()
    {
        var builder = new StringBuilder();

        AssertInterpolatedStringHandlerAppender.AppendLiteral(builder, "Value:");
        AssertInterpolatedStringHandlerAppender.AppendFormatted(builder, 123, "D4");

        builder.ToString().Should().Be("Value:0123");
    }

    public void AppendFormatted_StringAndObjectOverloads_AppendExpectedText()
    {
        var builder = new StringBuilder();

        AssertInterpolatedStringHandlerAppender.AppendFormatted(builder, "Hi", 4, null);
        AssertInterpolatedStringHandlerAppender.AppendFormatted(builder, (object)7, 0, "D2");

        builder.ToString().Should().Be("  Hi07");
    }

#if NETCOREAPP3_1_OR_GREATER
    public void AppendFormatted_ReadOnlySpan_AppendsExpectedText()
    {
        var builder = new StringBuilder();

        AssertInterpolatedStringHandlerAppender.AppendFormatted(builder, "SpanText".AsSpan());

        builder.ToString().Should().Be("SpanText");
    }
#endif
}
