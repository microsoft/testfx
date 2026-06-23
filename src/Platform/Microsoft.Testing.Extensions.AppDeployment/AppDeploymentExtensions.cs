// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AppDeployment;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding application deployment support to the test application builder.
/// </summary>
public static class AppDeploymentExtensions
{
    /// <summary>
    /// Registers a test host launcher that deploys the test host into an isolated directory and
    /// launches it from there.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddAppDeployment(this ITestApplicationBuilder builder)
    {
        _ = builder ?? throw new System.ArgumentNullException(nameof(builder));
        builder.TestHostControllers.AddTestHostLauncher(_ => new AppDeploymentLauncher());
    }
}
