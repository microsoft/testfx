// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Allows an extension to control how the out-of-process test host is launched, replacing the
/// platform's default <c>Process.Start</c> behavior.
/// </summary>
/// <remarks>
/// The platform keeps owning argument and environment preparation, connection handshakes, lifecycle
/// callbacks, and exit-code reconciliation, and delegates only the single "create and start the test host"
/// step. A controller launch carries the controller IPC/PID handshake; an orchestrator launch can instead
/// carry extension-specific connection metadata such as Retry. Launchers must forward the supplied context
/// opaquely rather than require one specific handshake. The launcher does not have to start a local OS
/// process as long as it returns an <see cref="ITestHostHandle"/> the platform can monitor.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostLauncher : ITestHostControllersExtension
{
    /// <summary>
    /// Creates and starts the test host. The platform has already prepared the file name,
    /// arguments, and environment variables carried by <paramref name="context"/> for the active controller
    /// or orchestrator mode. The implementation must return a handle the platform can monitor for completion.
    /// </summary>
    /// <param name="context">The fully prepared launch information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A handle the platform monitors for the lifetime of the test host.</returns>
    Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken);
}
