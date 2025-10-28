// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MTPOTel;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

        // Register our simple test framework
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (capabilities, serviceProvider) => new SimpleTestFramework(serviceProvider));

        // Enable OpenTelemetry with console exporter
        testApplicationBuilder.AddOpenTelemetryProvider(
            tracing =>
            {
                tracing.AddTestingPlatformInstrumentation();
                tracing.AddConsoleExporter();
            },
            metrics =>
            {
                metrics.AddTestingPlatformInstrumentation();
                metrics.AddConsoleExporter();
            });

        using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
        return await testApplication.RunAsync();
    }
}
