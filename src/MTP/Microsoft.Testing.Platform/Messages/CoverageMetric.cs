// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Identifies a code-coverage metric. This is a closed but append-only set: consumers MUST treat
/// unrecognized values non-exhaustively (always handle a default case). New well-known members may
/// be added in future versions without being a breaking change.
/// </summary>
public enum CoverageMetric
{
    /// <summary>Line coverage.</summary>
    Line = 0,

    /// <summary>Statement coverage.</summary>
    Statement = 1,

    /// <summary>Branch coverage.</summary>
    Branch = 2,

    /// <summary>Method coverage.</summary>
    Method = 3,

    /// <summary>Function coverage.</summary>
    Function = 4,

    /// <summary>Block coverage (Microsoft.CodeCoverage / dotnet-coverage primary metric).</summary>
    Block = 5,

    /// <summary>Instruction coverage (JaCoCo).</summary>
    Instruction = 6,

    /// <summary>Region coverage (llvm-cov).</summary>
    Region = 7,

    /// <summary>Class coverage (JaCoCo / PHPUnit entity counter).</summary>
    Class = 8,

    /// <summary>Condition coverage.</summary>
    Condition = 9,

    /// <summary>Cyclomatic complexity (JaCoCo, count-based rather than a percentage).</summary>
    Complexity = 10,

    /// <summary>
    /// A collector-specific metric not covered by the well-known members. When set,
    /// <see cref="TestCoverageMessage.CustomMetricName"/> carries the metric identifier. This is the
    /// single escape hatch for proprietary / safety-critical metrics (e.g. MC/DC).
    /// </summary>
    Custom = 255,
}
