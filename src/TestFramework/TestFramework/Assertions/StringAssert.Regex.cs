// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class StringAssert
{
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
}
