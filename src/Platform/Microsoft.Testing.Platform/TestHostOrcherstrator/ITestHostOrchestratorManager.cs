// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal interface ITestHostOrchestratorManager
{
    void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory);

    // NOTE: In ITestHostManager, we have AddTestApplicationLifecycleCallbacks, which is an unfortunate naming.
    // If we ever open orchestration before MTP v2 (https://github.com/microsoft/testfx/issues/5733), we should
    // consider if we are okay with this kinda inconsistent naming between test host and test host orchestrator.
    void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory);

    Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider);
}
