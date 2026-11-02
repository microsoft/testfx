// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    private static void AppendSkipReasonsMarkdown(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        int headingLevel)
    {
        long skippedTests = modules.Sum(static module => module.SkippedTests);
        if (skippedTests == 0)
        {
            return;
        }

        (CiRunSummaryModule Module, CiRunSummarySkipReason Reason)[] reasons =
        [
            .. modules
                .Where(static module => module.SkippedTests > 0)
                .SelectMany(static module => (module.SkipReasons
                    ?? [new CiRunSummarySkipReason { Reason = "Skip reason unavailable (older fragment)", Count = module.SkippedTests }])
                    .Select(reason => (Module: module, Reason: reason)))
                .OrderByDescending(static item => item.Reason.Count)
                .ThenBy(static item => item.Reason.Reason, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.AssemblyName, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.TargetFramework, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.Architecture, StringComparer.Ordinal)
                .ThenBy(static item => item.Module.AttemptNumber)
                .ThenBy(static item => item.Module.SessionUid, StringComparer.Ordinal)
                .Take(MaxSkipReasonRows),
        ];

        builder.Append(new string('#', headingLevel)).Append(" Skipped tests by reason\n\n");
        builder.Append("| Skipped | Reason | Platform | Test module |\n");
        builder.Append("| ---: | --- | --- | --- |\n");
        foreach ((CiRunSummaryModule module, CiRunSummarySkipReason reason) in reasons)
        {
            builder.Append("| ").Append(reason.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(EscapeCell(reason.Reason))
                .Append(" | ").Append(EscapeCell(module.OperatingSystem ?? "Unavailable"))
                .Append(" | ");
            AppendModuleIdentity(builder, modules, module);
            builder.Append(" |\n");
        }

        builder.Append('\n');
        long omittedTests = skippedTests - reasons.Sum(static item => item.Reason.Count);
        if (omittedTests > 0)
        {
            builder.Append("> ").Append(omittedTests.ToString(CultureInfo.InvariantCulture))
                .Append(" additional skipped tests are not shown.\n\n");
        }
    }
}
