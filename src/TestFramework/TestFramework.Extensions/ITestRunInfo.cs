// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Read-only ambient information about the current test run. Accessed via <see cref="TestRun.Current"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="TestContext"/>, which describes the test currently executing, an
/// <see cref="ITestRunInfo"/> describes the run as a whole and is queryable from any code reachable
/// during a test run (for example helpers, fixtures, or <c>[AssemblyInitialize]</c> methods).
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public interface ITestRunInfo
{
    /// <summary>
    /// Gets the tests that have been discovered and have passed the active filter for the
    /// currently-executing assembly. The collection is empty until the platform begins executing
    /// tests for an assembly.
    /// </summary>
    /// <remarks>
    /// Data-driven tests whose rows are unfolded only at execution time may appear here as a
    /// single entry rather than one entry per row.
    /// </remarks>
    IReadOnlyCollection<PlannedTest> PlannedTests { get; }
}
