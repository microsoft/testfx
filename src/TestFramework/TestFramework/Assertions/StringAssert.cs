// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The string assert.
/// </summary>
[StackTraceHidden]
public sealed class StringAssert
{
    #region Singleton constructor

    private StringAssert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the StringAssert functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be <c>public static void ContainsWords(this StringAssert customAssert, string value, ICollection substrings)</c>
    /// and the call-site would be <c>StringAssert.That.ContainsWords(value, substrings);</c>.
    /// </para>
    /// <para>
    /// For new custom assertions, prefer extending <see cref="Assert.That"/> instead, because <see cref="StringAssert"/> is likely to be deprecated in a future release.
    /// For more information, see <see href="https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-assertions#extension-hooks-on-stringassert-and-collectionassert">Extension hooks on StringAssert and CollectionAssert</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example defines a custom <c>ContainsWords</c> assertion as an extension method
    /// on <see cref="StringAssert"/> and invokes it through <c>StringAssert.That</c>:
    /// <code language="csharp">
    /// using System.Collections.Generic;
    /// using Microsoft.VisualStudio.TestTools.UnitTesting;
    ///
    /// public static class CustomStringAssertExtensions
    /// {
    ///     public static void ContainsWords(this StringAssert stringAssert, string value, IEnumerable&lt;string&gt; words)
    ///     {
    ///         foreach (string word in words)
    ///         {
    ///             if (value == null || !value.Contains(word))
    ///             {
    ///                 throw new AssertFailedException($"StringAssert.That.ContainsWords failed. Word &lt;{word}&gt; not found in &lt;{value}&gt;.");
    ///             }
    ///         }
    ///     }
    /// }
    ///
    /// [TestClass]
    /// public class MessageTests
    /// {
    ///     [TestMethod]
    ///     public void Greeting_ContainsExpectedWords()
    ///     {
    ///         StringAssert.That.ContainsWords("Hello, world!", new[] { "Hello", "world" });
    ///     }
    /// }
    /// </code>
    /// </example>
    public static StringAssert That { get; } = new();

    #endregion

    #region Substrings

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring)
        => Contains(value, substring, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType)
        => Contains(value, substring, comparisonType, string.Empty);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, string? message)
        => Contains(value, substring, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType, string? message)
    {
        TelemetryCollector.TrackAssertionCall("StringAssert.Contains");

        Assert.CheckParameterNotNull(value, "StringAssert.Contains", "value");
        Assert.CheckParameterNotNull(substring, "StringAssert.Contains", "substring");
        if (value.IndexOf(substring, comparisonType) < 0)
        {
            ReportContainsFailed(value, substring, comparisonType, Assert.BuildUserMessage(message));
        }
    }

    [DoesNotReturn]
    private static void ReportContainsFailed(string value, string substring, StringComparison comparisonType, string? userMessage)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected substring:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring)
        => StartsWith(value, substring, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType)
        => StartsWith(value, substring, comparisonType, string.Empty);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, string? message)
        => StartsWith(value, substring, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType, string? message)
    {
        TelemetryCollector.TrackAssertionCall("StringAssert.StartsWith");

        Assert.CheckParameterNotNull(value, "StringAssert.StartsWith", "value");
        Assert.CheckParameterNotNull(substring, "StringAssert.StartsWith", "substring");
        if (!value.StartsWith(substring, comparisonType))
        {
            ReportStartsWithFailed(value, substring, comparisonType, Assert.BuildUserMessage(message));
        }
    }

    [DoesNotReturn]
    private static void ReportStartsWithFailed(string value, string substring, StringComparison comparisonType, string? userMessage)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected prefix:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.StartsWithFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring)
        => EndsWith(value, substring, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType)
        => EndsWith(value, substring, comparisonType, string.Empty);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, string? message)
        => EndsWith(value, substring, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType, string? message)
    {
        TelemetryCollector.TrackAssertionCall("StringAssert.EndsWith");

        Assert.CheckParameterNotNull(value, "StringAssert.EndsWith", "value");
        Assert.CheckParameterNotNull(substring, "StringAssert.EndsWith", "substring");
        if (!value.EndsWith(substring, comparisonType))
        {
            ReportEndsWithFailed(value, substring, comparisonType, Assert.BuildUserMessage(message));
        }
    }

    [DoesNotReturn]
    private static void ReportEndsWithFailed(string value, string substring, StringComparison comparisonType, string? userMessage)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected suffix:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.EndsWithFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);

        Assert.ReportAssertFailed(structured);
    }

    #endregion Substrings

    #region Regular Expressions

    /// <summary>
    /// Tests whether the specified string matches a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void Matches([NotNull] string? value, [NotNull] Regex? pattern)
        => Matches(value, pattern, string.Empty);

    /// <summary>
    /// Tests whether the specified string matches a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not match <paramref name="pattern"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void Matches([NotNull] string? value, [NotNull] Regex? pattern, string? message)
    {
        TelemetryCollector.TrackAssertionCall("StringAssert.Matches");

        Assert.CheckParameterNotNull(value, "StringAssert.Matches", "value");
        Assert.CheckParameterNotNull(pattern, "StringAssert.Matches", "pattern");

        if (!pattern.IsMatch(value))
        {
            ReportMatchesFailed(value, pattern, Assert.BuildUserMessage(message));
        }
    }

    [DoesNotReturn]
    private static void ReportMatchesFailed(string value, Regex pattern, string? userMessage)
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

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string matches the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatch([NotNull] string? value, [NotNull] Regex? pattern)
        => DoesNotMatch(value, pattern, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string matches the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// matches <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatch([NotNull] string? value, [NotNull] Regex? pattern, string? message)
    {
        TelemetryCollector.TrackAssertionCall("StringAssert.DoesNotMatch");

        Assert.CheckParameterNotNull(value, "StringAssert.DoesNotMatch", "value");
        Assert.CheckParameterNotNull(pattern, "StringAssert.DoesNotMatch", "pattern");

        if (pattern.IsMatch(value))
        {
            ReportDoesNotMatchFailed(value, pattern, Assert.BuildUserMessage(message));
        }
    }

    [DoesNotReturn]
    private static void ReportDoesNotMatchFailed(string value, Regex pattern, string? userMessage)
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

        Assert.ReportAssertFailed(structured);
    }

    #endregion Regular Expressions

    #region DoNotUse

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for equality.
    /// This method should <b>not</b> be used for comparison of two instances for equality.
    /// Please use StringAssert methods or Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: false,
        DiagnosticId = "MSTEST0102",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: true,
        DiagnosticId = "MSTEST0102",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool Equals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseStringAssertEquals);
        return false;
    }

    /// <summary>
    /// Static ReferenceEquals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// reference equality. Please use StringAssert methods or Assert.AreSame and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: false,
        DiagnosticId = "MSTEST0103",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: true,
        DiagnosticId = "MSTEST0103",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseStringAssertReferenceEquals,
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool ReferenceEquals(object? objA, object? objB)
    {
        Assert.Fail(FrameworkMessages.DoNotUseStringAssertReferenceEquals);
        return false;
    }

    #endregion
}
