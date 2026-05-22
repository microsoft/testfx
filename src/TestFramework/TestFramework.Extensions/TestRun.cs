// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides ambient access to information about the currently-executing test run.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Current"/> always returns a non-<see langword="null"/> <see cref="ITestRunInfo"/>;
/// before tests start executing it exposes empty collections.
/// </para>
/// <para>
/// The information is scoped to the current process and (on .NET Framework) the current
/// AppDomain. Cross-process or cross-AppDomain test runs each have their own snapshot.
/// </para>
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public static class TestRun
{
    /// <summary>
    /// Gets information about the currently-executing test run. Never returns <see langword="null"/>.
    /// </summary>
    public static ITestRunInfo Current { get; private set; } = EmptyTestRunInfo.Instance;

    /// <summary>
    /// Set by the platform before executing tests for an assembly. Pass <see langword="null"/> to
    /// reset to the empty implementation.
    /// </summary>
    internal static void SetCurrent(ITestRunInfo? info)
        => Current = info ?? EmptyTestRunInfo.Instance;

    private sealed class EmptyTestRunInfo : ITestRunInfo
    {
        public static EmptyTestRunInfo Instance { get; } = new();

        public IReadOnlyCollection<PlannedTest> PlannedTests { get; } = [];
    }
}
