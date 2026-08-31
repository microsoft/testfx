// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

public sealed partial class Assert
{
    // Match the runtime's default static Regex cache size while bounding retained user-provided pattern text.
    private const int RegexCacheSize = 15;
    private const int MaximumCachedRegexPatternLength = 512;

    private static readonly BoundedRegexCache RegexCache = new();

    #region MatchesRegex

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not match <paramref name="pattern"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="patternExpression">
    /// The syntactic expression of pattern as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void MatchesRegex([NotNull] Regex? pattern, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(pattern))] string patternExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.MatchesRegex");

        CheckParameterNotNull(value, "Assert.MatchesRegex", "value");
        CheckParameterNotNull(pattern, "Assert.MatchesRegex", "pattern");

        if (!pattern.IsMatch(value))
        {
            ReportAssertMatchesRegexFailed(pattern, value, message, patternExpression, valueExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertMatchesRegexFailed(Regex pattern, string value, string? userMessage, string patternExpression, string valueExpression)
    {
        string patternText = AssertionValueRenderer.RenderValue(pattern.ToString());
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected pattern:", patternText)
            .AddLine("actual:", actualText);

        StructuredAssertionMessage structured = new(FrameworkMessages.MatchesRegexFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(patternText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.MatchesRegex", patternExpression, valueExpression, "<pattern>", "<value>"));

        ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not match <paramref name="pattern"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="patternExpression">
    /// The syntactic expression of pattern as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void MatchesRegex([NotNull] string? pattern, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(pattern))] string patternExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => MatchesRegex(ToRegex(pattern), value, message, patternExpression, valueExpression);

    #endregion // MatchesRegex

    #region DoesNotMatchRegex

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="patternExpression">
    /// The syntactic expression of pattern as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] Regex? pattern, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(pattern))] string patternExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotMatchRegex");

        CheckParameterNotNull(value, "Assert.DoesNotMatchRegex", "value");
        CheckParameterNotNull(pattern, "Assert.DoesNotMatchRegex", "pattern");

        if (pattern.IsMatch(value))
        {
            ReportAssertDoesNotMatchRegexFailed(pattern, value, message, patternExpression, valueExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertDoesNotMatchRegexFailed(Regex pattern, string value, string? userMessage, string patternExpression, string valueExpression)
    {
        string patternText = AssertionValueRenderer.RenderValue(pattern.ToString());
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("unexpected pattern:", patternText)
            .AddLine("actual:", actualText);

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotMatchRegexFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(patternText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotMatchRegex", patternExpression, valueExpression, "<pattern>", "<value>"));

        ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="patternExpression">
    /// The syntactic expression of pattern as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] string? pattern, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(pattern))] string patternExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => DoesNotMatchRegex(ToRegex(pattern), value, message, patternExpression, valueExpression);

    #endregion // DoesNotMatchRegex

    private static Regex ToRegex([NotNull] string? pattern)
    {
        CheckParameterNotNull(pattern, "Assert.MatchesRegex", "pattern");
        if (pattern.Length > MaximumCachedRegexPatternLength)
        {
            return new Regex(pattern);
        }

        string cultureName = CultureInfo.CurrentCulture.Name;
        if (RegexCache.TryGet(pattern, cultureName, out Regex cachedRegex))
        {
            return cachedRegex;
        }

        Regex regex = new(pattern);
        return RegexCache.AddOrGetExisting(pattern, cultureName, regex);
    }

    private sealed class BoundedRegexCache
    {
#if NET9_0_OR_GREATER
        private readonly Lock _lock = new();
#else
        private readonly object _lock = new();
#endif

        private readonly RegexCacheEntry?[] _entries = new RegexCacheEntry[RegexCacheSize];

        private int _nextInsertionIndex;

        public bool TryGet(string pattern, string cultureName, out Regex regex)
        {
            int nextInsertionIndex = Volatile.Read(ref _nextInsertionIndex);
            for (int offset = 1; offset <= RegexCacheSize; offset++)
            {
                int index = (nextInsertionIndex - offset + RegexCacheSize) % RegexCacheSize;
                RegexCacheEntry? entry = Volatile.Read(ref _entries[index]);
                if (entry is not null
                    && string.Equals(entry.Pattern, pattern, StringComparison.Ordinal)
                    && string.Equals(entry.CultureName, cultureName, StringComparison.Ordinal))
                {
                    regex = entry.Regex;
                    return true;
                }
            }

            regex = null!;
            return false;
        }

        public Regex AddOrGetExisting(string pattern, string cultureName, Regex regex)
        {
            lock (_lock)
            {
                if (TryGet(pattern, cultureName, out Regex cachedRegex))
                {
                    return cachedRegex;
                }

                int insertionIndex = _nextInsertionIndex;
                Volatile.Write(ref _entries[insertionIndex], new RegexCacheEntry(pattern, cultureName, regex));
                Volatile.Write(ref _nextInsertionIndex, (insertionIndex + 1) % RegexCacheSize);
                return regex;
            }
        }
    }

    private sealed class RegexCacheEntry(string pattern, string cultureName, Regex regex)
    {
        public string Pattern { get; } = pattern;

        public string CultureName { get; } = cultureName;

        public Regex Regex { get; } = regex;
    }
}
