// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

/// <summary>
/// Activates Microsoft Testing Platform diagnostics for an application-owned OpenTelemetry provider.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Activates the Microsoft Testing Platform diagnostics producer.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command-line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddTestingPlatformDiagnostics();
}
