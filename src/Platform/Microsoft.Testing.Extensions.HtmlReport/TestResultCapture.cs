// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    public static CapturedTestResult? TryCapture(TestNode node)
    {
        CapturedTestResultCoreData? coreData = TestResultCaptureHelper.TryCaptureCore(node);
        if (!coreData.HasValue)
        {
            return null;
        }

        CapturedTestResultCoreData core = coreData.GetValueOrDefault();
        var result = new CapturedTestResult
        {
            // HTML does not special-case cancellation — it falls through to the shared
            // helper which maps it to "failed", unlike JUnit which maps it to "cancelled".
            Outcome = TestResultCaptureHelper.ClassifyOutcome(core.State),
        };
        result.PopulateBaseFields(node, core);
        return result;
    }
}
