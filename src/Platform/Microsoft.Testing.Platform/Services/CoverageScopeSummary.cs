// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// All coverage metrics correlated for a single scope. This is the primary summary-layer surface for
/// report generators: one entry per scope, with every metric that scope reported.
/// </summary>
public sealed class CoverageScopeSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageScopeSummary"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this summary belongs to.</param>
    /// <param name="scope">The scope this summary describes.</param>
    /// <param name="metrics">The metrics reported for the scope.</param>
    public CoverageScopeSummary(SessionUid sessionUid, CoverageScope scope, IReadOnlyList<CoverageMetricResult> metrics)
    {
        SessionUid = sessionUid;
        Scope = scope;
        Metrics = Array.AsReadOnly((metrics ?? throw new ArgumentNullException(nameof(metrics))).ToArray());
    }

    /// <summary>Gets the session this summary belongs to.</summary>
    public SessionUid SessionUid { get; }

    /// <summary>Gets the scope this summary describes.</summary>
    public CoverageScope Scope { get; }

    /// <summary>Gets the metrics reported for the scope.</summary>
    public IReadOnlyList<CoverageMetricResult> Metrics { get; }

    /// <summary>
    /// Gets a convenience lookup for a well-known metric, e.g. <c>summary[CoverageMetric.Line]</c>; null
    /// if absent. Throws for <see cref="CoverageMetric.Custom"/>, because every custom metric shares
    /// that enum value and is distinguished only by its name — use <see cref="GetCustom(string)"/>
    /// instead. If a scope carries the same metric from multiple producers, this returns the first; use
    /// <see cref="Metrics"/> to disambiguate by <see cref="CoverageMetricResult.ProducerId"/>.
    /// </summary>
    /// <param name="metric">The well-known metric to look up.</param>
    /// <returns>The matching metric result, or null if absent.</returns>
    public CoverageMetricResult? this[CoverageMetric metric]
    {
        get
        {
            if (metric == CoverageMetric.Custom)
            {
                throw new ArgumentException("Use GetCustom(name) to look up a custom metric.", nameof(metric));
            }

            foreach (CoverageMetricResult result in Metrics)
            {
                if (result.Metric == metric)
                {
                    return result;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Looks up a custom (proprietary) metric by its name; null if absent. If multiple producers report
    /// the same custom metric name, this returns the first in stable first-seen order; use
    /// <see cref="Metrics"/> to disambiguate by <see cref="CoverageMetricResult.ProducerId"/>.
    /// </summary>
    /// <param name="customMetricName">The custom metric name.</param>
    /// <returns>The matching metric result, or null if absent.</returns>
    public CoverageMetricResult? GetCustom(string customMetricName)
    {
        foreach (CoverageMetricResult result in Metrics)
        {
            if (result.Metric == CoverageMetric.Custom
                && string.Equals(result.CustomMetricName, customMetricName, StringComparison.Ordinal))
            {
                return result;
            }
        }

        return null;
    }
}
