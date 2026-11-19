// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

/// <summary>
/// Represents a single-invocation middleware that composes with other middleware around exactly one
/// existing <see cref="ITestHostExecutionOrchestrator"/> invocation.
/// </summary>
/// <remarks>
/// <para>
/// This is a v1, initial composition boundary for test host execution orchestration. Exactly one
/// <see cref="ITestHostExecutionOrchestrator"/> is still active per run; middleware wraps that single
/// invocation and can only observe or short-circuit it, not replace it with a typed multi-run engine.
/// In particular, this version of the API does not provide repeat/retry-style re-invocation, sharding,
/// phases, protocol metadata, or the ability to mutate identifiers such as attempt number, execution id,
/// or instance id. The existing <c>--retry-failed-tests</c> orchestrator is not reentrant and is not
/// extracted or reimplemented as middleware by this API. A typed, multi-run orchestration model (for
/// example a first-class retry/stress/shard engine) is intentionally out of scope for this version and
/// may be introduced later as a separate, additive capability.
/// </para>
/// <para>
/// Middleware is composed outermost-first in registration order (see
/// <see cref="Microsoft.Testing.Platform.TestHostOrchestrator.ITestHostExecutionOrchestratorMiddlewareManager.AddTestHostExecutionOrchestratorMiddleware(Func{IServiceProvider, ITestHostExecutionOrchestratorMiddleware})"/>).
/// This is an explicit, order-dependent composition: there is no numeric ordering key, and middleware must
/// not assume its effect on the pipeline is commutative with other middleware.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostExecutionOrchestratorMiddleware : ITestHostOrchestratorExtension
{
    /// <summary>
    /// Orchestrates test host execution by optionally invoking <paramref name="next"/> and returning its
    /// result unchanged, or by short-circuiting without invoking <paramref name="next"/> at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="next"/> can be invoked at most once, and the call to <paramref name="next"/> must
    /// begin before this method returns or throws. The platform rejects a second invocation, as well as
    /// an invocation deferred until after this method has returned or thrown. Middleware may start the
    /// call synchronously and then await its returned task asynchronously, but it cannot defer the call
    /// itself to another thread or callback after returning.
    /// </para>
    /// <para>
    /// When this method invokes <paramref name="next"/> and lets it complete (whether or not it also
    /// awaits it directly), the value this method returns must be exactly the downstream exit code: this
    /// version of the pipeline does not allow middleware to rewrite the downstream verdict. If the
    /// downstream execution faults or is canceled, the exception or cancellation must propagate unchanged;
    /// converting it to any exit code, including a non-success exit code, is unsupported and rejected by the
    /// platform. A downstream failure must not be swallowed into an apparently successful return from this
    /// method.
    /// When cancellation is signaled anywhere in the linked cancellation chain, the pipeline rethrows the
    /// resulting cancellation and does not allow middleware to replace it with a different exit code.
    /// </para>
    /// <para>
    /// When this method never invokes <paramref name="next"/> at all (short-circuiting the pipeline), the
    /// returned exit code must not be a success exit code, so that skipped execution can never be reported
    /// as if tests actually ran and passed.
    /// </para>
    /// <para>
    /// The platform links whatever <see cref="CancellationToken"/> is passed to <paramref name="next"/>
    /// with the root cancellation token, so passing <see cref="CancellationToken.None"/> does not detach
    /// the downstream execution from root cancellation.
    /// </para>
    /// <para>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </para>
    /// </remarks>
    /// <param name="next">
    /// A delegate that invokes the remainder of the pipeline (the next middleware, or the wrapped
    /// orchestrator) with the given cancellation token, and returns its exit code.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the test host exit code.</returns>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken cancellationToken);
}
