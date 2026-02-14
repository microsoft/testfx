// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

/// <summary>
/// Represents a manager for test host orchestrators.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostOrchestratorManager
{
    /// <summary>
    /// Adds a test host orchestrator.
    /// </summary>
    /// <param name="factory">The factory method for creating the test host orchestrator.</param>
    void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory);
}

internal interface IInternalTestHostOrchestratorManager : ITestHostOrchestratorManager
{
    // NOTE: In ITestHostManager, we have AddTestApplicationLifecycleCallbacks, which is an unfortunate naming.
    // If we ever open orchestration before MTP v2 (https://github.com/microsoft/testfx/issues/5733), we should
    // consider if we are okay with this kinda inconsistent naming between test host and test host orchestrator.
    void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory);

    Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider);
}
