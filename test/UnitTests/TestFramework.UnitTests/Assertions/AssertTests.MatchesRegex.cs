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

    public void MatchesRegex_WithCaseDistinctStringPatterns_DoesNotReuseRegex()
    {
        string patternPrefix = Guid.NewGuid().ToString("N");
        string lowercasePattern = $"^{patternPrefix}-a$";
        string uppercasePattern = $"^{patternPrefix}-A$";

        ToRegex(lowercasePattern).Should().NotBeSameAs(ToRegex(uppercasePattern));
    }

    public void MatchesRegex_WhenOldestRegexIsReusedBeforeCapacityIsExceeded_StillEvictsOldestRegex()
    {
        string patternPrefix = Guid.NewGuid().ToString("N");
        string oldestPattern = $"^{patternPrefix}-oldest$";
        Regex oldestRegex = ToRegex(oldestPattern);

        for (int i = 0; i < RegexCacheSize - 1; i++)
        {
            _ = ToRegex($"^{patternPrefix}-{i}$");
        }

        ToRegex(oldestPattern).Should().BeSameAs(oldestRegex);
        _ = ToRegex($"^{patternPrefix}-newest$");

        ToRegex(oldestPattern).Should().NotBeSameAs(oldestRegex);
    }

    public void MatchesRegex_WithMaximumLengthStringPattern_ReusesRegex()
    {
        string pattern = new('a', MaximumCachedRegexPatternLength);

        ToRegex(pattern).Should().BeSameAs(ToRegex(pattern));
    }

    public void MatchesRegex_WithOverMaximumLengthStringPattern_DoesNotCacheRegex()
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

    public void MatchesRegex_WithConcurrentCandidateRegexes_ConvergesOnSingleCachedRegex()
    {
        string pattern = $"^{Guid.NewGuid():N}$";
        string cultureName = CultureInfo.CurrentCulture.Name;
        const int ThreadCount = 8;
        var regexes = new Regex[ThreadCount];
        var exceptions = new Exception?[ThreadCount];
        using var ready = new CountdownEvent(ThreadCount);
        using var start = new ManualResetEventSlim();
        Type cacheType = typeof(Assert).GetNestedType("BoundedRegexCache", BindingFlags.NonPublic)!;
        object cache = Activator.CreateInstance(cacheType, nonPublic: true)!;
        var addOrGetExisting = (Func<string, string, Regex, Regex>)cacheType
            .GetMethod("AddOrGetExisting", BindingFlags.Instance | BindingFlags.Public)!
            .CreateDelegate(typeof(Func<string, string, Regex, Regex>), cache);
        var threads = new Thread[ThreadCount];

        for (int i = 0; i < threads.Length; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    var candidate = new Regex(pattern);
                    ready.Signal();
                    start.Wait();
                    regexes[index] = addOrGetExisting(pattern, cultureName, candidate);
                }
                catch (Exception ex)
                {
                    exceptions[index] = ex;
                }
            });
            threads[i].Start();
        }

        ready.Wait();
        start.Set();

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        exceptions.Should().BeEquivalentTo(new Exception?[ThreadCount]);
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
