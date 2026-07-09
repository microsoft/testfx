// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    private static bool AreEqualFailingFloatingPoint<T>(
        T expected,
        T actual,
        T delta,
        Func<T, bool> isNaN,
        Func<T, bool> isNegativeDelta,
        Func<T, T, T, bool> isAbsoluteDifferenceGreaterThanDelta)
        where T : struct
    {
        if (isNaN(delta) || isNegativeDelta(delta))
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (expected.Equals(actual))
        {
            return false;
        }

        // If both values are NaN, then they were considered equal in the previous check.
        // If only one of them is NaN, then they are not equal regardless of the value of delta.
        // Then, the subtraction comparison to delta isn't involving NaNs.
        return isNaN(expected) || isNaN(actual) ||
                isAbsoluteDifferenceGreaterThanDelta(expected, actual, delta);
    }

    [DoesNotReturn]
    private static void ReportAssertAreEqualFailed<T>(T expected, T actual, T delta, string? userMessage, string expectedExpression, string actualExpression)
        where T : struct, IConvertible
    {
        string expectedRendered = AssertionValueRenderer.RenderValue(expected);
        string actualRendered = AssertionValueRenderer.RenderValue(actual);
        string deltaRendered = AssertionValueRenderer.RenderValue(delta);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedRendered)
            .AddLine("actual:", actualRendered)
            .AddLine("delta:", deltaRendered);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreEqualDeltaFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedRendered, actualRendered);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreEqual", expectedExpression, actualExpression, string.Empty, "<expected>", "<actual>", "<delta>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotEqualFailed<T>(T notExpected, T actual, T delta, string? userMessage, string notExpectedExpression, string actualExpression)
        where T : struct, IConvertible
    {
        string notExpectedRendered = AssertionValueRenderer.RenderValue(notExpected);
        string actualRendered = AssertionValueRenderer.RenderValue(actual);
        string deltaRendered = AssertionValueRenderer.RenderValue(delta);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("not expected:", notExpectedRendered)
            .AddLine("actual:", actualRendered)
            .AddLine("delta:", deltaRendered);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreNotEqualDeltaFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual($"not {notExpectedRendered}", actualRendered);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreNotEqual", notExpectedExpression, actualExpression, string.Empty, "<notExpected>", "<actual>", "<delta>"));

        ReportAssertFailed(structured);
    }
}
