// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform;

internal enum TestProcessRole
{
    /// <summary>
    /// Indicates that the currently running process is the test host.
    /// </summary>
    TestHost,

    /// <summary>
    /// Indicates that the currently running process is the test host controller.
    /// </summary>
    TestHostController,

    /// <summary>
    /// Indicates that the currently running process is the test host orchestrator.
    /// </summary>
    TestHostOrchestrator,
}
