// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// Runs once before and once after all the tests of one invocation, rather than once per test host process.
/// </summary>
/// <remarks>
/// <para>
/// The hooks run in the outermost process of the invocation: the test host orchestrator (for example retry) when
/// one is active, otherwise the test host controller when one is active, otherwise the test host. Processes launched
/// by the owner never run the hooks again.
/// </para>
/// <para>
/// The hooks never run for <c>--help</c>, <c>--info</c>, tools, test discovery, or server mode.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IInvocationLifetimeHandler : IExtension
{
    /// <summary>
    /// Called before any test of the invocation runs.
    /// </summary>
    /// <param name="context">The context used to publish values to the test processes.</param>
    /// <param name="cancellationToken">The cancellation token of the invocation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Throwing fails the invocation: the remaining setups and the tests are skipped, and the cleanup of every
    /// handler whose setup was entered still runs.
    /// </remarks>
    Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Called after all the tests of the invocation completed, failed, or were canceled.
    /// </summary>
    /// <param name="context">The context that describes the outcome of the invocation.</param>
    /// <param name="cancellationToken">A cancellation token. It is not canceled when the invocation is canceled, so cleanup can complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Only called when <see cref="OnInvocationStartingAsync"/> was entered, including when it threw.
    /// Handlers are called in the reverse order of their registration.
    /// </remarks>
    Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken);
}
