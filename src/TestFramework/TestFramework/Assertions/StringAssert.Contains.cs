// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class StringAssert
{
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
}
