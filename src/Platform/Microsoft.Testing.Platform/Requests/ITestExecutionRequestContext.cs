// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides access to test execution request context for filter providers.
/// Available in the per-request service provider in server mode.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestExecutionRequestContext
{
    /// <summary>
    /// Gets the collection of test nodes specified in the request, or null if not filtering by specific nodes.
    /// </summary>
    ICollection<TestNode>? TestNodes { get; }
}
