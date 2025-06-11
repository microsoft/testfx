// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal interface ITestHostOrchestratorManager
{
    void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory);

    void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory);

    Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider);
}
