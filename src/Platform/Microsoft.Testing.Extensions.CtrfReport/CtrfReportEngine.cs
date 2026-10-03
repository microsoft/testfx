// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine : ReportEngineBase
{
    // CTRF spec: https://github.com/ctrf-io/ctrf
    private const string CtrfReportFormat = "CTRF";
    // Pre-1.0 MINOR releases may be incompatible, so review the mapping before updating this pinned version.
    private const string CtrfSpecVersion = "0.1.0";

    public CtrfReportEngine(ReportEngineContext context)
        : base(context)
    {
    }

    public Task<(string FileName, string? Warning)> GenerateReportAsync(CapturedTestResult[] results)
        => GenerateReportCoreAsync(results, _clock.UtcNow);

    private async Task<(string FileName, string? Warning)> GenerateReportCoreAsync(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        (string finalPath, _) = ResolveOutputPath(
            CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName,
            () => BuildDefaultFileName(finishTime));

        byte[] bytes = BuildCtrfJson(results, finishTime);

        return await WriteAsync(finalPath, bytes).ConfigureAwait(false);
    }
}
