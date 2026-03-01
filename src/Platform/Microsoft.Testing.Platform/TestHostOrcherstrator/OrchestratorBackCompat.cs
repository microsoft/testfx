// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// These types provide binary backward compatibility for extensions compiled
// against older platform versions where the orchestrator manager type lived in the
// Microsoft.Testing.Platform.Extensions.TestHostOrchestrator namespace.
namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal interface ITestHostOrchestratorManager
{
    void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory);
}

// Kept for binary backward compatibility with extensions that cast to this concrete type.
internal sealed class TestHostOrchestratorManager : global::Microsoft.Testing.Platform.TestHostOrchestrator.TestHostOrchestratorManager;
