// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Specifies where a test execution request originated.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestExecutionRequestOrigin
{
    /// <summary>
    /// A request created by the console test host.
    /// </summary>
    Console,

    /// <summary>
    /// A request received by the JSON-RPC server.
    /// </summary>
    Server,
}
