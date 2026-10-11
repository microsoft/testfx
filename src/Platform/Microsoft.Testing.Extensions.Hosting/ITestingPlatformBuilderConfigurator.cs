// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Configures a Microsoft Testing Platform application from services owned by an application host.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestingPlatformBuilderConfigurator
{
    /// <summary>
    /// Configures the test application builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    void Configure(ITestApplicationBuilder testApplicationBuilder);
}
