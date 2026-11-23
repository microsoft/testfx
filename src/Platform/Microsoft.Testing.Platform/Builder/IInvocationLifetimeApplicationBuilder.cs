// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.InvocationLifetime;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents a test application builder that supports registering invocation lifetime handlers.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IInvocationLifetimeApplicationBuilder : ITestApplicationBuilder
{
    /// <summary>
    /// Gets the invocation lifetime manager.
    /// </summary>
    IInvocationLifetimeManager InvocationLifetime { get; }
}
