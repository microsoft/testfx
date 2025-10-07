// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides a filter for server-mode test execution requests.
/// Providers query request-specific information from the service provider.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IRequestFilterProvider : IExtension
{
    /// <summary>
    /// Determines whether this provider can handle the current request context.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing request-specific services.</param>
    /// <returns>true if this provider can create a filter for the current request; otherwise, false.</returns>
    bool CanHandle(IServiceProvider serviceProvider);

    /// <summary>
    /// Creates a test execution filter for the current request.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing request-specific services.</param>
    /// <returns>A test execution filter.</returns>
    Task<ITestExecutionFilter> CreateFilterAsync(IServiceProvider serviceProvider);
}
