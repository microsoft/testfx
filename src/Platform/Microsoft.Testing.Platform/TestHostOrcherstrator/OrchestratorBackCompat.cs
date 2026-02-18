// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// These types provide binary backward compatibility for extensions compiled
// against older platform versions where the orchestrator types lived in the
// Microsoft.Testing.Platform.Extensions.TestHostOrchestrator namespace.
namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

// Extends the new public interface so internal code can use this type everywhere.
internal interface ITestHostOrchestrator : global::Microsoft.Testing.Platform.TestHostOrchestrator.ITestHostOrchestrator;

internal interface ITestHostOrchestratorManager
{
    void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory);
}

// Kept for binary backward compatibility with extensions that cast to this concrete type.
internal sealed class TestHostOrchestratorManager : global::Microsoft.Testing.Platform.TestHostOrchestrator.TestHostOrchestratorManager
{
    internal void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory)
        => ((ITestHostOrchestratorManager)this).AddTestHostOrchestrator(factory);
}
