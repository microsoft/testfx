// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class IndentedStringBuilderTests
{
    [TestMethod]
    public void Append_IndentsOnlyTheFirstWriteOfEachLine()
    {
        var builder = new IndentedStringBuilder(indentationLevel: 1);

        builder.Append("a").Append("b").AppendLine();
        builder.Append("c").AppendLine("d");
        builder.AppendLine("e");

        builder.ToString().Should().Be(
            """
                ab
                cd
                e

            """.NormalizeNewlines());
    }

    [TestMethod]
    public void AppendLine_WithoutValue_WritesAnEmptyLineWithoutIndentation()
    {
        var builder = new IndentedStringBuilder(indentationLevel: 1);

        builder.AppendLine("a").AppendLine().AppendLine("b");

        builder.ToString().Should().Be(
            """
                a

                b

            """.NormalizeNewlines());
    }

    [TestMethod]
    public void Block_WithoutHeader_OpensWithABareBrace()
    {
        var builder = new IndentedStringBuilder();

        using (builder.Block())
        {
            builder.AppendLine("body;");
        }

        builder.ToString().Should().Be(
            """
            {
                body;
            }

            """.NormalizeNewlines());
    }

    [TestMethod]
    public void Block_DisposingTheSameBlockTwice_ClosesItOnlyOnce()
    {
        var builder = new IndentedStringBuilder();

        IDisposable outer = builder.Block("class A");
        IDisposable inner = builder.Block("void M()");
        builder.AppendLine("body;");
        inner.Dispose();
        inner.Dispose();

        builder.IndentationLevel.Should().Be(1);

        outer.Dispose();

        builder.IndentationLevel.Should().Be(0);
        builder.ToString().Should().Be(
            """
            class A
            {
                void M()
                {
                    body;
                }
            }

            """.NormalizeNewlines());
    }
}
