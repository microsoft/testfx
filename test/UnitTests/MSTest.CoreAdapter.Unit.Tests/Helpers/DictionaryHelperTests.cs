﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Moq;

using TestableImplementations;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class DictionaryHelperTests : TestContainer
{
    public void ConcatenatingDictionariesReturnsEmptyDictionaryWhenBothSidesAreNullOrEmpty()
    {
        Dictionary<string, string> source = null;

        var overwrite = new Dictionary<string, string>();

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));
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

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));

        var sortedActual = from entry in actual orderby entry.Key ascending select entry;
        var sortedSource = from entry in source orderby entry.Key ascending select entry;
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

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));

        var sortedActual = from entry in actual orderby entry.Key ascending select entry;
        var sortedOverwrite = from entry in overwrite orderby entry.Key ascending select entry;
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

        var actual = source.ConcatWithOverwrites(overwrite, nameof(source), nameof(overwrite));
        var expected = new Dictionary<string, string>
        {
            // this is only present in source, take it
            ["aaa"] = "source",

            // this is present only in overwrite, take it from overwrite
            ["ccc"] = "overwrite",

            // this is present in source and overwrite, take it from overwrite
            ["bbb"] = "overwrite",

        };

        var sortedActual = from entry in actual orderby entry.Key ascending select entry;
        var sortedExpected = from entry in expected orderby entry.Key ascending select entry;
        Verify(sortedActual.SequenceEqual(sortedExpected));
    }
}
