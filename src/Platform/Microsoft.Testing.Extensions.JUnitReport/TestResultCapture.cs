// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.JUnitReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    // The display name of a test node is also captured for non-terminal (Discovered /
    // InProgress) messages so that the engine can reconstruct the parent chain for
    // every terminal test. This DTO is intentionally tiny because every node in the
    // tree contributes one entry to the parent-chain dictionary.
    internal readonly record struct ParentChainEntry(string DisplayName, string? ParentRawUid);

    public static ParentChainEntry GetParentChainEntry(TestNodeUpdateMessage update)
        => new(
            // Display names are test-controlled and may be very long, so cap them to
            // bound the size of the parent-chain dictionary and the generated XML.
            TestResultCaptureHelper.Truncate(update.TestNode.DisplayName, TestResultCaptureHelper.MaxIdentityFieldLength)!,
            // The parent UID is also test-controlled. Cap it to the same identity
            // budget used everywhere else; lookups against `_parentChain` (also
            // keyed by a truncated UID) stay consistent.
            TestResultCaptureHelper.Truncate(update.ParentTestNodeUid?.Value, TestResultCaptureHelper.MaxIdentityFieldLength));

    public static CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
    {
        TestNode node = update.TestNode;
        CapturedTestResultCoreData? coreData = TestResultCaptureHelper.TryCaptureCore(node);
        if (!coreData.HasValue)
        {
            return null;
        }

        CapturedTestResultCoreData core = coreData.GetValueOrDefault();
        var result = new CapturedTestResult
        {
            // RawUid and ParentRawUid are used as keys/edges in the parent-chain
            // dictionary, so they must be capped with the same MaxIdentityFieldLength
            // budget on both sides to keep lookups consistent.
            RawUid = TestResultCaptureHelper.Truncate(node.Uid.Value, TestResultCaptureHelper.MaxIdentityFieldLength)!,
            ParentRawUid = TestResultCaptureHelper.Truncate(update.ParentTestNodeUid?.Value, TestResultCaptureHelper.MaxIdentityFieldLength),
            Outcome = ClassifyOutcome(core.State),
        };
        result.PopulateBaseFields(node, core);
        return result;
    }

    private static string ClassifyOutcome(TestNodeStateProperty state)
    {
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
        // Cancellation is an interruption, not an assertion failure. The RFC maps it
        // to <error> in the generated XML; classifying it as its own bucket here
        // keeps that mapping local to JUnit. Keep the cancellation decision out of
        // the shared helper so each report format preserves its existing behavior.
        if (state is CancelledTestNodeStateProperty)
        {
            return "cancelled";
        }
#pragma warning restore CS0618, MTP0001

        return TestResultCaptureHelper.ClassifyOutcome(state);
    }
}
