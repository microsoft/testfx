// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// Outcome of a test.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public enum UnitTestOutcome : int
{
    /// <summary>
    /// There was a system error while we were trying to execute a test.
    /// </summary>
    Error,

    /// <summary>
    /// Test was executed, but there were issues.
    /// Issues may involve exceptions or failed assertions.
    /// </summary>
    Failed,

    /// <summary>
    /// The test timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Test has completed, but we can't say if it passed or failed.
    /// (Used in Assert.InConclusive scenario).
    /// </summary>
    Inconclusive,

    /// <summary>
    /// Test had it chance for been executed but was not, as Ignore == true.
    /// </summary>
    Ignored,

    /// <summary>
    /// Test cannot be executed.
    /// </summary>
    NotRunnable,

    /// <summary>
    /// Test was executed w/o any issues.
    /// </summary>
    Passed,

    /// <summary>
    /// The specific test cannot be found.
    /// </summary>
    NotFound,

    /// <summary>
    /// When test is handed over to runner for execution, it goes into progress state.
    /// It is added so that the right status can be set in TestContext.
    /// </summary>
    InProgress,
}
