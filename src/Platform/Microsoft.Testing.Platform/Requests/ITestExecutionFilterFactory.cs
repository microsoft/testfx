// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Factory for creating test execution filters in console mode.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public interface ITestExecutionFilterFactory : IExtension
{
    /// <summary>
    /// Attempts to create a test execution filter.
    /// </summary>
    /// <returns>A task containing a tuple with success status and the created filter if successful.</returns>
    Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync();
}
