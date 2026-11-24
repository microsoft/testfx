// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// Registers invocation lifetime handlers with a test application.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IInvocationLifetimeManager
{
    /// <summary>
    /// Adds an invocation lifetime handler factory.
    /// </summary>
    /// <param name="factory">The factory used to create the handler.</param>
    /// <remarks>
    /// The factory is only called in the process that owns the invocation.
    /// </remarks>
    void AddInvocationLifetimeHandler(Func<IServiceProvider, IInvocationLifetimeHandler> factory);
}
