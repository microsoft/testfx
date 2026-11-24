// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal sealed class TestHostOrchestratorConfiguration(
    ITestHostExecutionOrchestrator[] testHostOrchestrators,
    ITestHostExecutionOrchestratorMiddleware[] middleware,
    string[] consumedOrchestratorUids,
    bool isCompositionActive)
{
    // Kept for compatibility with existing (test) call sites that only care about orchestrators, without
    // requiring every caller to also decide on a middleware array.
    internal TestHostOrchestratorConfiguration(ITestHostExecutionOrchestrator[] testHostOrchestrators)
        : this(testHostOrchestrators, [], [], isCompositionActive: false)
    {
    }

    internal TestHostOrchestratorConfiguration(
        ITestHostExecutionOrchestrator[] testHostOrchestrators,
        ITestHostExecutionOrchestratorMiddleware[] middleware)
        : this(testHostOrchestrators, middleware, [], isCompositionActive: false)
    {
    }

    public ITestHostExecutionOrchestrator[] TestHostOrchestrators { get; } = testHostOrchestrators;

    public string[] ConsumedOrchestratorUids { get; } = consumedOrchestratorUids;

    public bool IsCompositionActive { get; } = isCompositionActive;

    public bool IsOutermostProcess => ConsumedOrchestratorUids.Length == 0;

    /// <summary>
    /// Gets the single-invocation middleware to compose around <see cref="TestHostOrchestrators"/>,
    /// outermost-first. Empty when no middleware is registered; the original
    /// <see cref="TestHostOrchestrators"/> array and its authorization-marker/handshake behavior are
    /// unaffected either way.
    /// </summary>
    public ITestHostExecutionOrchestratorMiddleware[] Middleware { get; } = middleware;
}
