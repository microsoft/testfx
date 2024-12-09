// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class DictionaryHelperTests : TestContainer
{
    public void ConcatenatingDictionariesReturnsEmptyDictionaryWhenBothSidesAreNullOrEmpty()
    {
        Dictionary<string, string> source = null;

        var overwrite = new Dictionary<string, string>();

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);
        var expected = new Dictionary<string, string>();

        actual.ToList().Sort();
        expected.ToList().Sort();
        Verify(actual.SequenceEqual(expected));
    }

    public void ConcatenatingDictionariesReturnsSourceSideWhenOverwriteIsNullOrEmpty()
    {
        var source = new Dictionary<string, string>
        {
            ["bbb"] = "source",
            ["aaa"] = "source",
        };

        Dictionary<string, string> overwrite = null;

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);

        IOrderedEnumerable<KeyValuePair<string, string>> sortedActual = from entry in actual orderby entry.Key select entry;
        IOrderedEnumerable<KeyValuePair<string, string>> sortedSource = from entry in source orderby entry.Key select entry;
        Verify(sortedActual.SequenceEqual(sortedSource));
    }

    public void ConcatenatingDictionariesReturnsOverwriteSideWhenSourceIsNullOrEmpty()
    {
        Dictionary<string, string> source = null;

        var overwrite = new Dictionary<string, string>
        {
            ["ccc"] = "overwrite",
            ["bbb"] = "overwrite",
        };

        IDictionary<string, string> actual = source.ConcatWithOverwrites(overwrite);

        IOrderedEnumerable<KeyValuePair<string, string>> sortedActual = from entry in actual orderby entry.Key select entry;
        IOrderedEnumerable<KeyValuePair<string, string>> sortedOverwrite = from entry in overwrite orderby entry.Key select entry;
        Verify(sortedActual.SequenceEqual(sortedOverwrite));
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
        Verify(sortedActual.SequenceEqual(sortedExpected));
    }
}
