// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    private static readonly int RegexCacheSize =
        (int)typeof(Assert)
            .GetField("RegexCacheSize", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetRawConstantValue()!;

    private static readonly int MaximumCachedRegexPatternLength =
        (int)typeof(Assert)
            .GetField("MaximumCachedRegexPatternLength", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetRawConstantValue()!;

    private static readonly Func<string?, Regex> ToRegex =
        (Func<string?, Regex>)typeof(Assert)
            .GetMethod("ToRegex", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<string?, Regex>));

    public void MatchesRegex_WithRegexPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.MatchesRegex(new Regex("^he"), "hello"))
            .Should().NotThrow();

    public void MatchesRegex_WithStringPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.MatchesRegex("^he", "hello"))
            .Should().NotThrow();

    public void MatchesRegex_WithRepeatedStringPattern_ReusesRegex()
    {
        string pattern = $"^{Guid.NewGuid():N}$";

        ToRegex(pattern).Should().BeSameAs(ToRegex(pattern));
    }

    public void MatchesRegex_WithMoreThanCacheSizeDistinctStringPatterns_EvictsOldestRegex()
    {
        string patternPrefix = Guid.NewGuid().ToString("N");
        string oldestPattern = $"^{patternPrefix}-oldest$";
        Regex oldestRegex = ToRegex(oldestPattern);

        for (int i = 0; i < RegexCacheSize; i++)
        {
            _ = ToRegex($"^{patternPrefix}-{i}$");
        }

        ToRegex(oldestPattern).Should().NotBeSameAs(oldestRegex);
    }

    public void MatchesRegex_WithLongStringPattern_DoesNotCacheRegex()
    {
        string pattern = new('a', MaximumCachedRegexPatternLength + 1);

        ToRegex(pattern).Should().NotBeSameAs(ToRegex(pattern));
    }

    public void MatchesRegex_WithCultureSensitivePattern_DoesNotReuseRegexAcrossCultures()
    {
        const string Pattern = "(?i)^i$";
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Regex enUsRegex = ToRegex(Pattern);
            enUsRegex.IsMatch("I").Should().BeTrue();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Regex trTrRegex = ToRegex(Pattern);
            trTrRegex.Should().NotBeSameAs(enUsRegex);
            trTrRegex.IsMatch("I").Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    public void MatchesRegex_WithSameStringPatternConcurrently_ReusesThreadSafeRegex()
    {
        string pattern = $"^{Guid.NewGuid():N}$";
        var regexes = new Regex[64];

        Parallel.For(0, regexes.Length, i => regexes[i] = ToRegex(pattern));

        regexes.Should().OnlyContain(regex => ReferenceEquals(regexes[0], regex));
    }

    public void MatchesRegex_WithInvalidStringPatternAndNullValue_ThrowsPatternExceptionFirst()
    {
        Action action = () => Assert.MatchesRegex("[", null);

        action.Should().Throw<ArgumentException>();
    }

    public void DoesNotMatchRegex_WithInvalidStringPatternAndNullValue_ThrowsPatternExceptionFirst()
    {
        Action action = () => Assert.DoesNotMatchRegex("[", null);

        action.Should().Throw<ArgumentException>();
    }

    public void MatchesRegex_WithValidStringPatternAndNullValue_ThrowsAssertFailedException()
    {
        Action action = () => Assert.MatchesRegex("valid", null);

        action.Should().Throw<AssertFailedException>();
    }

    public void DoesNotMatchRegex_WithValidStringPatternAndNullValue_ThrowsAssertFailedException()
    {
        Action action = () => Assert.DoesNotMatchRegex("valid", null);

        action.Should().Throw<AssertFailedException>();
    }

    public void MatchesRegex_WithRegexPattern_BypassesStringPatternCache()
    {
        const string Pattern = "^abc$";
        Regex cachedRegex = ToRegex(Pattern);
        var suppliedRegex = new Regex(Pattern, RegexOptions.IgnoreCase);

        Assert.MatchesRegex(suppliedRegex, "ABC");
        ToRegex(Pattern).Should().BeSameAs(cachedRegex);
        suppliedRegex.Should().NotBeSameAs(cachedRegex);
    }

    public void MatchesRegex_WithRegexPattern_OnFailure_UsesStructuredMessageAndPayload()
    {
        Action action = () => Assert.MatchesRegex(new Regex("^foo"), "hello", "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to match the specified pattern.
                User-provided message

                expected pattern: "^foo"
                actual:           "hello"

                Assert.MatchesRegex(new Regex("^foo"), "hello")
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"^foo\"");
        ex.ActualText.Should().Be("\"hello\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void MatchesRegex_WithStringPattern_OnFailure_UsesStructuredMessage()
    {
        string pattern = "^foo";
        string value = "hello";
        Action action = () => Assert.MatchesRegex(pattern, value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to match the specified pattern.

                expected pattern: "^foo"
                actual:           "hello"

                Assert.MatchesRegex(pattern, value)
                """);
    }

    public void DoesNotMatchRegex_WithRegexPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.DoesNotMatchRegex(new Regex("world"), "hello"))
            .Should().NotThrow();

    public void DoesNotMatchRegex_WithStringPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.DoesNotMatchRegex("world", "hello"))
            .Should().NotThrow();

    public void DoesNotMatchRegex_WithRegexPattern_OnFailure_UsesStructuredMessageAndPayload()
    {
        Action action = () => Assert.DoesNotMatchRegex(new Regex("world"), "hello world", "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not match the specified pattern.
                User-provided message

                unexpected pattern: "world"
                actual:             "hello world"

                Assert.DoesNotMatchRegex(new Regex("world"), "hello world")
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"world\"");
        ex.ActualText.Should().Be("\"hello world\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void DoesNotMatchRegex_WithStringPattern_OnFailure_UsesStructuredMessage()
    {
        string pattern = "world";
        string value = "hello world";
        Action action = () => Assert.DoesNotMatchRegex(pattern, value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not match the specified pattern.

                unexpected pattern: "world"
                actual:             "hello world"

                Assert.DoesNotMatchRegex(pattern, value)
                """);
    }
}
