// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Hosts;

/// <summary>
/// This represents a host (i.e, a process). It could be a "test host" (the process that actually runs the tests),
/// a "test host controller" (runs out-of-process extensions like dump extensions which observes the test host), or
/// a "test host orchestrator" (like Retry extension, which coordinates how test hosts should be run).
/// </summary>
internal interface IHost
{
    Task<int> RunAsync();
}
