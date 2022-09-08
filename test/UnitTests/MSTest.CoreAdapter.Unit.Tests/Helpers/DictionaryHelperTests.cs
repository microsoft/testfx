// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

using System.Collections.Generic;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

[TestClass]
public class DictionaryHelperTests
{
    [TestMethod]
    public void ConcatenatingDictionariesReturnsEmptyDictionaryWhenBothSidesAreNullOrEmpty()
    {
        Dictionary<string, string> source = null;

        var overwrite = new Dictionary<string, string>();

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));
        var expected = new Dictionary<string, string>();

        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void ConcatenatingDictionariesReturnsSourceSideWhenOverwriteIsNullOrEmpty()
    {
        var source = new Dictionary<string, string>
        {
            ["aaa"] = "source",
            ["bbb"] = "source",
        };

        Dictionary<string, string> overwrite = null;

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));

        actual.Should().BeEquivalentTo(source);
    }

    [TestMethod]
    public void ConcatenatingDictionariesReturnsOverwriteSideWhenSourceIsNullOrEmpty()
    {
        Dictionary<string, string> source = null;

        var overwrite = new Dictionary<string, string>
        {
            ["bbb"] = "overwrite",
            ["ccc"] = "overwrite",
        };

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));

        actual.Should().BeEquivalentTo(overwrite);
    }

    [TestMethod]
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

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));
        var expected = new Dictionary<string, string>
        {
            // this is only present in source, take it
            ["aaa"] = "source",

            // this is present in source and overwrite, take it from overwrite
            ["bbb"] = "overwrite",

            // this is present only in overwrite, take it from overwrite
            ["ccc"] = "overwrite",
        };

        actual.Should().BeEquivalentTo(expected);
    }
}
