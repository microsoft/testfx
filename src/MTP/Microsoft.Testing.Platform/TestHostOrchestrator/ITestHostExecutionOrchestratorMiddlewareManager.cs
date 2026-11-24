// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

/// <summary>
/// Optional capability of an <see cref="ITestHostOrchestratorManager"/> that also supports registering
/// single-invocation <see cref="ITestHostExecutionOrchestratorMiddleware"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This is a separate, optional interface rather than a new required member on
/// <see cref="ITestHostOrchestratorManager"/>, so existing implementations of
/// <see cref="ITestHostOrchestratorManager"/> keep compiling and running unchanged. Prefer
/// <see cref="TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware(ITestHostOrchestratorManager, Func{IServiceProvider, ITestHostExecutionOrchestratorMiddleware})"/>
/// to add middleware through an <see cref="ITestHostOrchestratorManager"/> reference without depending on
/// this interface directly.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostExecutionOrchestratorMiddlewareManager
{
    /// <summary>
    /// Adds a test host execution orchestrator middleware.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Middleware is composed outermost-first in registration order: the first middleware registered is
    /// the outermost layer and is the last to observe the pipeline's overall result (after every inner
    /// layer, including the wrapped orchestrator, has run); the last middleware registered is the
    /// innermost layer, closest to the wrapped orchestrator. This ordering is explicit and positional —
    /// there is no numeric ordering key, and middleware must not assume its effect on the pipeline is
    /// commutative with other middleware.
    /// </para>
    /// <para>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </para>
    /// </remarks>
    /// <param name="factory">The factory method for creating the middleware.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestHostExecutionOrchestratorMiddleware(Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware> factory);
}
