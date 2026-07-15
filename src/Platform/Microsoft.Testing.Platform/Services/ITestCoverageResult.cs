// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// A correlated, session-scoped view of all coverage data for consumers. The platform correlates the
/// raw coverage messages once and exposes this read model so consumers (the terminal, HTML/UI report
/// generators, PR-comment bots, dashboards) never re-parse artifacts or re-implement aggregation.
/// </summary>
public interface ITestCoverageResult
{
    /// <summary>Gets the overall (whole-run) summary, if reported.</summary>
    CoverageScopeSummary? Overall { get; }

    /// <summary>Gets the per-scope summaries with all metrics correlated per scope.</summary>
    IReadOnlyList<CoverageScopeSummary> Scopes { get; }

    /// <summary>Gets all threshold evaluations (passed and failed).</summary>
    IReadOnlyList<TestCoverageThresholdMessage> Thresholds { get; }

    /// <summary>Gets pointers to rich report artifacts for deep parsing.</summary>
    IReadOnlyList<CoverageReportReference> Reports { get; }

    /// <summary>Gets a value indicating whether any threshold failed (drives the exit code).</summary>
    bool HasThresholdFailure { get; }
}
