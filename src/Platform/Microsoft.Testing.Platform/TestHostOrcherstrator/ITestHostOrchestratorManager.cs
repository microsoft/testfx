// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

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

    /// <summary>
    /// Adds a test host orchestrator application lifetime.
    /// </summary>
    /// <param name="testHostOrchestratorApplicationLifetimeFactory">The factory method for creating the test host orchestrator application lifetime.</param>
    void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory);
}
