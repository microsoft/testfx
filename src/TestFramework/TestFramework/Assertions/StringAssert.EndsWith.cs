// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class StringAssert
{
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
}
