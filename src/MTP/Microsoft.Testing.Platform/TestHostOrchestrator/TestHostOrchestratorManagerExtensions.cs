// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

/// <summary>
/// Extension methods for <see cref="ITestHostOrchestratorManager"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestHostOrchestratorManagerExtensions
{
    /// <summary>
    /// Adds a test host execution orchestrator middleware to <paramref name="manager"/>, when
    /// <paramref name="manager"/> supports it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method exists so middleware can be registered through the existing
    /// <see cref="ITestHostOrchestratorManager"/> surface without adding a required member to that
    /// interface, which would break existing implementations. It casts <paramref name="manager"/> to the
    /// optional <see cref="ITestHostExecutionOrchestratorMiddlewareManager"/> capability and throws a clear,
    /// actionable exception when the concrete manager does not support middleware.
    /// </para>
    /// <para>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </para>
    /// </remarks>
    /// <param name="manager">The test host orchestrator manager.</param>
    /// <param name="factory">The factory method for creating the middleware.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="manager"/> does not implement <see cref="ITestHostExecutionOrchestratorMiddlewareManager"/>.
    /// </exception>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static void AddTestHostExecutionOrchestratorMiddleware(
        this ITestHostOrchestratorManager manager,
        Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware> factory)
    {
        _ = manager ?? throw new ArgumentNullException(nameof(manager));
        _ = factory ?? throw new ArgumentNullException(nameof(factory));

        if (manager is not ITestHostExecutionOrchestratorMiddlewareManager middlewareManager)
        {
            throw new NotSupportedException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostOrchestratorManagerDoesNotSupportMiddlewareErrorMessage, manager.GetType()));
        }

        middlewareManager.AddTestHostExecutionOrchestratorMiddleware(factory);
    }
}
