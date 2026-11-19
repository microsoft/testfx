// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    private static bool IsTypeMatchFailing([NotNullWhen(false)] object? value, [NotNullWhen(false)] Type? expectedType, bool exact)
        => expectedType is null
            || value is null
            || (exact
                ? value.GetType() != expectedType
                : !expectedType.IsInstanceOfType(value));

    private static bool IsTypeMismatchFailing(object? value, [NotNullWhen(false)] Type? wrongType, bool exact)
        => wrongType is null
            // Null is not an instance of any type.
            || (value is not null && (exact ? value.GetType() == wrongType : wrongType.IsInstanceOfType(value)));

    [DoesNotReturn]
    private static void ReportAssertTypeMatchFailed(object? value, Type? expectedType, string? userMessage, string valueExpression, bool exact)
    {
        StructuredAssertionMessage msg = expectedType is null
            ? new("Cannot check type because the expected type argument is null.")
            : exact
                ? new($"Expected value to be exactly of type {expectedType.Name}.")
                : new($"Expected value to be of type {expectedType.Name} (or derived).");
        msg.WithUserMessage(userMessage);

        if (expectedType is not null)
        {
            string expectedTypeText = exact ? expectedType.ToString() : $"{expectedType} (or derived)";
            string actualTypeText = value?.GetType().ToString() ?? "null";
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("expected type:", expectedTypeText)
                .AddLine(value is null ? "actual:" : "actual type:", actualTypeText);
            msg.WithEvidence(evidence)
                .WithExpectedAndActual(expectedTypeText, actualTypeText);
        }

        msg.WithCallSiteExpression(FormatCallSiteExpression(
            exact ? "Assert.IsExactInstanceOfType" : "Assert.IsInstanceOfType",
            valueExpression,
            "<value>"));
        ReportAssertFailed(msg);
    }

    [DoesNotReturn]
    private static void ReportAssertTypeMismatchFailed(object? value, Type? wrongType, string? userMessage, string valueExpression, bool exact)
    {
        StructuredAssertionMessage msg = wrongType is null
            ? new("Cannot check type because the not-expected type argument is null.")
            : exact
                ? new($"Expected value to not be exactly of type {wrongType.Name}.")
                : new($"Expected value to not be of type {wrongType.Name} (or derived).");
        msg.WithUserMessage(userMessage);

        if (wrongType is not null)
        {
            string wrongTypeText = exact ? wrongType.ToString() : $"{wrongType} (or derived)";
            string actualTypeText = value?.GetType().ToString() ?? "null";
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("not expected type:", wrongTypeText)
                .AddLine("actual type:", actualTypeText);
            msg.WithEvidence(evidence)
                .WithExpectedAndActual(wrongTypeText, actualTypeText);
        }

        msg.WithCallSiteExpression(FormatCallSiteExpression(
            exact ? "Assert.IsNotExactInstanceOfType" : "Assert.IsNotInstanceOfType",
            valueExpression,
            "<value>"));
        ReportAssertFailed(msg);
    }
}
