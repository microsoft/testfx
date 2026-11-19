// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal static class CoverageThresholdExitCodePolicy
{
    /// <summary>
    /// Applies the coverage-threshold verdict to an already-computed exit code. When the run is otherwise
    /// successful but a coverage threshold failed, returns <see cref="ExitCode.CoverageThresholdFailed"/>
    /// otherwise returns <paramref name="exitCode"/> unchanged. The caller applies the shared ignore-exit-code
    /// policy after all verdicts have been combined.
    /// </summary>
    /// <remarks>
    /// Used by <c>TestHostControllersTestHost</c> for controller-only threshold messages that arrive after the
    /// child test host has finalized its exit code. In-process thresholds are part of
    /// <see cref="ITestApplicationProcessExitCode"/> directly.
    /// </remarks>
    public static int Apply(int exitCode, IServiceProvider serviceProvider)
    {
        if (exitCode != (int)ExitCode.Success)
        {
            return exitCode;
        }

        ITestCoverageResult? coverageResult = serviceProvider.GetService<ITestCoverageResult>();
        return coverageResult?.HasThresholdFailure == true
            ? (int)ExitCode.CoverageThresholdFailed
            : exitCode;
    }
}
