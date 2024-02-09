// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an interface for handling the lifetime of the test host process.
/// </summary>
public interface ITestHostProcessLifetimeHandler : ITestHostControllersExtension
{
    /// <summary>
    /// Executes before the test host process starts.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes when the test host process has started.
    /// </summary>
    /// <param name="testHostProcessInformation">Information about the test host process.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);

    /// <summary>
    /// Executes when the test host process has exited.
    /// </summary>
    /// <param name="testHostProcessInformation">Information about the test host process.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);
}
