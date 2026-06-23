// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding packaged Windows (UWP/WinUI) test host deployment support
/// to the test application builder.
/// </summary>
public static class PackagedAppExtensions
{
    /// <summary>
    /// Registers a test host launcher that deploys the packaged Windows (UWP/WinUI) test host into
    /// an isolated directory and launches it from there.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddPackagedAppDeployment(this ITestApplicationBuilder builder)
    {
        _ = builder ?? throw new System.ArgumentNullException(nameof(builder));
        builder.TestHostControllers.AddTestHostLauncher(_ => new PackagedAppTestHostLauncher());
    }
}
