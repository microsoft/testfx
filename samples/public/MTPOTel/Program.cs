// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MTPOTel;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var testActivitySource = new ActivitySource("MTPOTel.Tests");
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder(args);
        hostBuilder.Configuration["MTPOTel:Composition"] = "HostApplicationBuilder";

        // The host owns the OpenTelemetry providers, just like an Aspire ServiceDefaults project or any other
        // application composition root. The focused MTP resource helpers add test/CI identity without replacing
        // the service.name, host, OS, or process identity already configured by the application.
        hostBuilder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("MTPOTel")
                .AddTestingPlatformTestResource()
                .AddTestingPlatformCIResource())
            .WithTracing(tracing => tracing
                .AddSource(testActivitySource.Name)
                .AddTestingPlatformInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddTestingPlatformInstrumentation()
                .AddConsoleExporter());

        using IHost host = hostBuilder.Build();
        return await host.RunTestingPlatformAsync(args, testApplicationBuilder =>
        {
            testApplicationBuilder.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, serviceProvider) => new SimpleTestFramework(serviceProvider, testActivitySource));

            // Activate only MTP's ActivitySource and Meter. The host remains responsible for creating,
            // flushing, and disposing the providers that subscribe to those diagnostics.
            testApplicationBuilder.AddTestingPlatformDiagnostics();
        });
    }
}
