// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MTPOTel;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder(args);

        // The host owns the OpenTelemetry providers, just like an Aspire ServiceDefaults project or any other
        // application composition root. The focused MTP resource helpers add test/CI identity without replacing
        // the service.name, host, OS, or process identity already configured by the application.
        hostBuilder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("MTPOTel")
                .AddTestingPlatformTestResource()
                .AddTestingPlatformCiResource())
            .WithTracing(tracing => tracing
                .AddTestingPlatformInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddTestingPlatformInstrumentation()
                .AddConsoleExporter());

        using IHost host = hostBuilder.Build();
        await host.StartAsync();

        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

        // Register our simple test framework
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (capabilities, serviceProvider) => new SimpleTestFramework(serviceProvider));

        // Activate only MTP's ActivitySource and Meter. The application host above remains responsible for creating,
        // flushing, and disposing the providers that subscribe to those diagnostics.
        testApplicationBuilder.AddTestingPlatformDiagnostics();

        using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
        int exitCode = await testApplication.RunAsync();

        // Keep the host alive until after the test application has shut down so final spans and metrics can be exported.
        await host.StopAsync();
        return exitCode;
    }
}
