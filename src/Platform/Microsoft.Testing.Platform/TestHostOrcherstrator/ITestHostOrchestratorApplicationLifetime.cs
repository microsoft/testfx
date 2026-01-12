// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

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
