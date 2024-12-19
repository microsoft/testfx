// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// A capability to support stopping test execution gracefully, without cancelling/aborting everything.
/// This is used to support '--maximum-failed-tests'.
/// </summary>
/// <remarks>
/// Test frameworks can choose to run any needed cleanup when cancellation is requested.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IGracefulStopTestExecutionCapability : ITestFrameworkCapability
{
    /// <summary>
    /// This requests the test framework to stop test execution gracefully.
    /// </summary>
    /// <param name="cancellationToken">
    /// If stopping gracefully is taking long, the user may press Ctrl+C to request
    /// a hard abort. In that case, test frameworks should respect the cancellation token and finish execution as soon as possible.
    /// </param>
    /// <remarks>
    /// Stopping gracefully is currently used for the --maximum-failed-tests feature.
    /// Test frameworks may decide that a graceful stop should run any remaining class/assembly cleanups, if needed.
    /// </remarks>
    Task StopTestExecutionAsync(CancellationToken cancellationToken);
}
