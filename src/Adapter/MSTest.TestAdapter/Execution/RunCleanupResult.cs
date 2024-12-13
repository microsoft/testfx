// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Result of the run cleanup operation.
/// </summary>
[Serializable]
internal sealed class RunCleanupResult
{
    /// <summary>
    /// Gets or sets the standard out of the cleanup methods.
    /// </summary>
    internal string? StandardOut { get; set; }

    /// <summary>
    /// Gets or sets the standard error of the cleanup methods.
    /// </summary>
    internal string? StandardError { get; set; }

    /// <summary>
    /// Gets or sets the Debug trace of the cleanup methods.
    /// </summary>
    internal string? DebugTrace { get; set; }

    /// <summary>
    /// Gets or sets the Warnings from the RunCleanup method.
    /// </summary>
    internal IList<string>? Warnings { get; set; }
}
