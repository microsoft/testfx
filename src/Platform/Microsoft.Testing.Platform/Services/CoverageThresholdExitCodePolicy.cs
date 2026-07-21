// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal static class CoverageThresholdExitCodePolicy
{
    /// <summary>
    /// Applies the coverage-threshold verdict to an already-computed exit code. When the run is otherwise
    /// successful but a coverage threshold failed, returns <see cref="ExitCode.CoverageThresholdFailed"/>
    /// routed through the shared ignore-exit-code policy (so <c>--ignore-exit-code 14</c> can suppress it);
    /// otherwise returns <paramref name="exitCode"/> unchanged.
    /// </summary>
    /// <remarks>
    /// Shared by <c>ConsoleTestHost</c> (in-process) and <c>TestHostControllersTestHost</c> (out-of-process)
    /// so the two exit-code paths can't drift apart. Both call this only after their own exit code has been
    /// finalized (and, for the controller, after the child's ignore-exit-code policy has been applied).
    /// </remarks>
    public static int Apply(int exitCode, IServiceProvider serviceProvider)
    {
        if (exitCode != (int)ExitCode.Success)
        {
            return exitCode;
        }

        ITestCoverageResult? coverageResult = serviceProvider.GetService<ITestCoverageResult>();
        return coverageResult?.HasCoverageThresholdFailure == true
            ? ExitCodeIgnorePolicy.Apply((int)ExitCode.CoverageThresholdFailed, serviceProvider.GetCommandLineOptions(), serviceProvider.GetEnvironment())
            : exitCode;
    }
}
