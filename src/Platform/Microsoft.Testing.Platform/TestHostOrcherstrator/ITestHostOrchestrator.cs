// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

/// <summary>
/// Represents an extension that orchestrates test host execution.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostOrchestrator : IExtension
{
    /// <summary>
    /// Orchestrates test host execution.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the test host exit code.</returns>
    Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken);
}
