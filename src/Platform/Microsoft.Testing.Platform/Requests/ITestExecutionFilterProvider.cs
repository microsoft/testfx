// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Provides an additional constraint for a test execution request.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestExecutionFilterProvider : IExtension
{
    /// <summary>
    /// Gets the filter constraint for the current request.
    /// </summary>
    /// <param name="context">The test execution filter context.</param>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns>
    /// A task whose result is the filter constraint, or <see langword="null"/> when the provider does
    /// not constrain the current request.
    /// </returns>
    /// <remarks>
    /// Provider constraints are not currently applied to JSON-RPC server requests. Return
    /// <see langword="null"/> when <see cref="TestExecutionFilterContext.Origin"/> is
    /// <see cref="TestExecutionRequestOrigin.Server"/>.
    /// </remarks>
    Task<ITestExecutionFilter?> GetFilterAsync(TestExecutionFilterContext context, CancellationToken cancellationToken);
}
