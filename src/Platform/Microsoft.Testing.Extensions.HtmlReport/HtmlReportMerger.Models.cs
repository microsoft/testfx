// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal static partial class HtmlReportMerger
{
    private sealed record HtmlReportMergeInput(
        string Html,
        string? ProducingTestModule,
        string? TargetFramework,
        string? Architecture,
        string? ExecutionId);

    private sealed record ParsedHtmlReport(
        HtmlReportMergeInput Input,
        JsonObject Report,
        DateTimeOffset StartTime,
        int OriginalIndex);

    private sealed record MergedTest(
        JsonObject Test,
        string? ProducingTestModule,
        string? TargetFramework,
        string? Architecture,
        string? ExecutionId,
        DateTimeOffset SourceReportStartTime,
        int OriginalReportIndex,
        int OriginalTestIndex);

    private readonly record struct TestIdentity(
        string Uid,
        string ProducingTestModule,
        string TargetFramework,
        string Architecture);

    private readonly record struct RetryIdentity(TestIdentity Test, string DisplayName);

    private readonly record struct RetrySlotIdentity(
        RetryIdentity BaseIdentity,
        int? OriginalReportIndex,
        int? OriginalTestIndex);
}
