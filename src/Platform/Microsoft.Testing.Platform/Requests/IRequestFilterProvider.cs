// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides a filter for server-mode test execution requests based on request arguments.
/// </summary>
internal interface IRequestFilterProvider : IExtension
{
    /// <summary>
    /// Determines whether this provider can handle the given request arguments.
    /// </summary>
    /// <param name="args">The request arguments.</param>
    /// <returns>true if this provider can create a filter for the given arguments; otherwise, false.</returns>
    bool CanHandle(RequestArgsBase args);

    /// <summary>
    /// Creates a test execution filter for the given request arguments.
    /// </summary>
    /// <param name="args">The request arguments.</param>
    /// <returns>A test execution filter.</returns>
    Task<ITestExecutionFilter> CreateFilterAsync(RequestArgsBase args);
}
