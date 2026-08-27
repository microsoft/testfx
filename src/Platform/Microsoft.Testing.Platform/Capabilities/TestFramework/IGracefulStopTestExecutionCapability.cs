// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// Stops the test execution gracefully.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopTestExecutionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A graceful-stop capability that reports whether a stop request was accepted.
/// </summary>
/// <remarks>
/// Test frameworks should implement this capability when a successful stop request can be a no-op because
/// execution has already completed.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IGracefulStopTestExecutionResultCapability : IGracefulStopTestExecutionCapability
{
    /// <summary>
    /// Attempts to stop the test execution gracefully.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when a new stop request was accepted; otherwise,
    /// <see langword="false"/> when execution had already completed or a stop had already been requested.
    /// </returns>
    Task<bool> TryStopTestExecutionAsync(CancellationToken cancellationToken);
}
