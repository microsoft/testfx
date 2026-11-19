// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

/// <summary>
/// Represents the application lifetime for a test host orchestrator.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostOrchestratorApplicationLifetime : ITestHostOrchestratorExtension
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
    /// <remarks>
    /// Also invoked when the orchestration was canceled or failed, so that whatever
    /// <see cref="BeforeRunAsync(CancellationToken)"/> acquired can still be released. Implementations
    /// must therefore tolerate being called when their <see cref="BeforeRunAsync(CancellationToken)"/>
    /// never ran or did not complete, must be safe to call once per run regardless of outcome, and should
    /// not assume <paramref name="cancellationToken"/> is still usable — it may already be canceled.
    /// Implementations should not throw from this method. When the orchestration was canceled or failed,
    /// exceptions thrown from this method are logged and suppressed so they cannot mask the original outcome.
    /// </remarks>
    /// <param name="exitCode">The exit code of the orchestrator.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterRunAsync(int exitCode, CancellationToken cancellationToken);
}
