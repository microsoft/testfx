// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class XmlReaderUtilitiesPass2MutationTests : TestContainer
{
    public void SkipToNextElement_SkipsTheNonElementNodesThatFollowTheSkippedSubtree()
    {
        using StringReader stringReader = new("<Root><A><Child /></A><!--between--> <B /></Root>");
        using var reader = XmlReader.Create(stringReader);
        reader.ReadToFollowing("A").Should().BeTrue();

        reader.SkipToNextElement();

        reader.NodeType.Should().Be(XmlNodeType.Element, because: "the comment and the whitespace that follow the skipped subtree are not elements");
        reader.Name.Should().Be("B");
    }

    public void SkipToNextElement_ReachesTheEndOfTheDocumentWhenNoElementFollowsTheSkippedSubtree()
    {
        using StringReader stringReader = new("<Root><A /></Root>");
        using var reader = XmlReader.Create(stringReader);
        reader.ReadToFollowing("A").Should().BeTrue();

        reader.SkipToNextElement();

        reader.EOF.Should().BeTrue(because: "only the closing tag of the root follows the skipped element");
        reader.NodeType.Should().NotBe(XmlNodeType.Element);
    }
}
