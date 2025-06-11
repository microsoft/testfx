// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

// NOTE: The equivalent of this for "test host" is ITestApplicationLifecycleCallbacks, which is an unfortunate naming.
// If we ever open orchestration before MTP v2 (https://github.com/microsoft/testfx/issues/5733), we should
// consider if we are okay with this kinda inconsistent naming between test host and test host orchestrator.
internal interface ITestHostOrchestratorApplicationLifetime : ITestHostOrchestratorExtension
{
    /// <summary>
    /// Executes before the orchestrator runs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeRunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes after the orchestrator runs.
    /// </summary>
    /// <param name="exitCode">The exit code of the orchestrator.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterRunAsync(int exitCode, CancellationToken cancellation);
}
