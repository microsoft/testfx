// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class DictionaryHelperTests : TestContainer
{
    public void ConcatenatingDictionariesReturnsEmptyDictionaryWhenBothSidesAreNullOrEmpty()
    {
        Dictionary<string, string>? source = null;

        var overwrite = new Dictionary<string, string>();

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);
        var expected = new Dictionary<string, string>();

        actual.ToList().Sort();
        expected.ToList().Sort();
        actual.SequenceEqual(expected).Should().BeTrue();
    }

    public void ConcatenatingDictionariesReturnsSourceSideWhenOverwriteIsNullOrEmpty()
    {
        var source = new Dictionary<string, string>
        {
            ["bbb"] = "source",
            ["aaa"] = "source",
        };

        Dictionary<string, string>? overwrite = null;

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);

        IOrderedEnumerable<KeyValuePair<string, string>> sortedActual = from entry in actual orderby entry.Key select entry;
        IOrderedEnumerable<KeyValuePair<string, string>> sortedSource = from entry in source orderby entry.Key select entry;
        sortedActual.SequenceEqual(sortedSource).Should().BeTrue();
    }

    public void ConcatenatingDictionariesReturnsOverwriteSideWhenSourceIsNullOrEmpty()
    {
        Dictionary<string, string>? source = null;

        var overwrite = new Dictionary<string, string>
        {
            ["ccc"] = "overwrite",
            ["bbb"] = "overwrite",
        };

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);

        IOrderedEnumerable<KeyValuePair<string, string>> sortedActual = from entry in actual orderby entry.Key select entry;
        IOrderedEnumerable<KeyValuePair<string, string>> sortedOverwrite = from entry in overwrite orderby entry.Key select entry;
        sortedActual.SequenceEqual(sortedOverwrite).Should().BeTrue();
    }

    public void ConcatenatingDictionariesShouldMergeThemAndTakeDuplicateKeysFromOverwrite()
    {
        var source = new Dictionary<string, string>
        {
            ["aaa"] = "source",
            ["bbb"] = "source",
        };

        var overwrite = new Dictionary<string, string>
        {
            ["bbb"] = "overwrite",
            ["ccc"] = "overwrite",
        };

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);
        var expected = new Dictionary<string, string>
        {
            // this is only present in source, take it
            ["aaa"] = "source",

            // this is present only in overwrite, take it from overwrite
            ["ccc"] = "overwrite",

            // this is present in source and overwrite, take it from overwrite
            ["bbb"] = "overwrite",
        };

        IOrderedEnumerable<KeyValuePair<string, string>> sortedActual = from entry in actual orderby entry.Key select entry;
        IOrderedEnumerable<KeyValuePair<string, string>> sortedExpected = from entry in expected orderby entry.Key select entry;
        sortedActual.SequenceEqual(sortedExpected).Should().BeTrue();
    }

    public void MergeCopiesEveryInputPartitionAndPreservesNullValuesWithTracingEnabled()
    {
        Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.IPlatformServiceProvider previous =
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance;
        var services = new TestableImplementations.TestablePlatformServiceProvider();
        services.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);

        try
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = services;
            Dictionary<string, string?> source = new() { ["retained"] = "source", ["collision"] = "old" };
            Dictionary<string, string?> overwrite = new() { ["collision"] = null, ["added"] = "new" };

            IDictionary<string, string?> merged = source.ConcatWithOverwrites(overwrite);
            merged.Should().BeEquivalentTo(new Dictionary<string, string?> { ["retained"] = "source", ["collision"] = null, ["added"] = "new" });
            merged["retained"] = "changed";
            source["retained"].Should().Be("source");
            source["collision"].Should().Be("old");
            overwrite["collision"].Should().BeNull();

            IDictionary<string, string?> fromSource = source.ConcatWithOverwrites(null);
            fromSource["retained"] = "copy";
            source["retained"].Should().Be("source");
            IDictionary<string, string?> fromOverwrite = ((Dictionary<string, string?>?)null).ConcatWithOverwrites(overwrite);
            fromOverwrite["added"] = "copy";
            overwrite["added"].Should().Be("new");
            ((Dictionary<string, string?>?)null).ConcatWithOverwrites(null).Should().BeEmpty();
        }
        finally
        {
            Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider.Instance = previous;
        }
    }
}
