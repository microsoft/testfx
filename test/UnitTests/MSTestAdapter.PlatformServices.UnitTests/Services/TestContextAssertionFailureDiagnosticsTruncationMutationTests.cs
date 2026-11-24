// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextAssertionFailureDiagnosticsTruncationMutationTests : TestContainer
{
    private const string Suffix = "... <truncated>";
    private const string Pair = "\uD83D\uDE00";
    private const string ReplacementCharacter = "\uFFFD";

    private static readonly MethodInfo TruncateMethod = typeof(TestContextImplementation).GetMethod("Truncate", BindingFlags.NonPublic | BindingFlags.Static)!;

    public void NullIsReturnedAsNull()
        => Truncate(null, 10).Should().BeNull();

    public void ValuesWithinTheLimitAreReturnedUnchanged()
    {
        AssertTruncated(string.Empty, 0, string.Empty);
        AssertTruncated("abc", 3, "abc");
        AssertTruncated("abc", 4, "abc");
        AssertTruncated(new string('a', 19), 20, new string('a', 19));
        AssertTruncated(new string('a', 20), 20, new string('a', 20));
        AssertTruncated(new string('a', 20), 21, new string('a', 20));
    }

    public void ValuesAboveTheLimitAreCutAndMarkedAsTruncated()
    {
        AssertTruncated(new string('a', 21), 20, "aaaaa" + Suffix);
        AssertTruncated(new string('a', 40), 20, "aaaaa" + Suffix);
        AssertTruncated(new string('a', 100), 30, new string('a', 15) + Suffix);
        AssertTruncated("abcdefghijklmnopqrstuvwxyz", 20, "abcde" + Suffix);
        AssertTruncated("abcdefghijklmnopqrstuvwxyz", 16, "a" + Suffix);
    }

    public void LimitsNotLargerThanTheSuffixKeepOnlyTheSuffix()
    {
        AssertTruncated("0123456789012345", 15, Suffix);
        AssertTruncated("0123456789012345", 14, Suffix);
        AssertTruncated("abc", 2, Suffix);
        AssertTruncated("abc", 0, Suffix);
    }

    public void TheLimitIsNotUsedToPreallocateTheResult()
    {
        // A mutated capacity of int.MaxValue throws OutOfMemoryException for a 3-character value.
        AssertTruncated("abc", int.MaxValue, "abc");
        AssertTruncated(new string('a', 40), int.MaxValue, new string('a', 40));
    }

    public void SurrogatePairsThatFitAreKeptIntact()
    {
        AssertTruncated(Pair, 10, Pair);
        AssertTruncated("a" + Pair + "b", 10, "a" + Pair + "b");
        AssertTruncated(Pair + Pair, 10, Pair + Pair);
        AssertTruncated(Pair, 2, Pair);
        AssertTruncated(Pair + "b", 3, Pair + "b");
        AssertTruncated("a" + Pair, 3, "a" + Pair);
    }

    public void SurrogatePairsAreNeverSplitWhenTheLimitIsReached()
    {
        string tail = new('x', 20);

        // Content limit is the maximum length minus the 15-character suffix.
        AssertTruncated(Pair + tail, 17, Pair + Suffix);
        AssertTruncated("a" + Pair + tail, 18, "a" + Pair + Suffix);
        AssertTruncated("ab" + Pair + tail, 18, "ab" + Suffix);
        AssertTruncated(Pair + Pair + tail, 16, Suffix);
        AssertTruncated(Pair + Pair + tail, 18, Pair + Suffix);
        AssertTruncated(Pair + Pair + tail, 19, Pair + Pair + Suffix);
        AssertTruncated(Pair + Pair + Pair + tail, 20, Pair + Pair + Suffix);
    }

    public void LoneSurrogatesAreReplacedWithTheReplacementCharacter()
    {
        AssertTruncated("\uD83D", 10, ReplacementCharacter);
        AssertTruncated("\uD83Dx", 10, ReplacementCharacter + "x");
        AssertTruncated("a\uD83Db", 10, "a" + ReplacementCharacter + "b");
        AssertTruncated("\uDE00", 10, ReplacementCharacter);
        AssertTruncated("a\uDE00b", 10, "a" + ReplacementCharacter + "b");
        AssertTruncated("\uD83D\uD83D\uDE00", 10, ReplacementCharacter + Pair);
        AssertTruncated(Pair + "\uDE00", 10, Pair + ReplacementCharacter);
    }

    private static string? Truncate(string? value, int maximumLength)
        => (string?)TruncateMethod.Invoke(null, [value, maximumLength]);

    private static void AssertTruncated(string? value, int maximumLength, string? expected)
        => Truncate(value, maximumLength).Should().Be(
            expected,
            because: $"a {value?.Length}-character value truncated to {maximumLength} must produce exactly the expected text");
}
#endif
