// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Telemetry;
using Microsoft.Testing.Extensions.Telemetry.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

#if NETCOREAPP
using System.Runtime.CompilerServices;
#endif

namespace Microsoft.Testing.Extensions;

public static class AppInsightsTelemetryProviderExtensions
{
    public static void AddAppInsightsTelemetryProvider(this ITestApplicationBuilder builder)
    {
#if NETCOREAPP
        // AppInsights is not supported on platforms that do not support dynamic code generation.
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return;
        }
#endif
#pragma warning disable IDE0022 // Use expression body for method

        if (builder is not TestApplicationBuilder testApplicationBuilder)
        {
            throw new ArgumentException(ExtensionResources.AddAppInsightsTelemetryProviderInvalidBuilder);
        }

        testApplicationBuilder.TelemetryManager.AddTelemetryCollectorProvider(services =>
        {
            IEnvironment environment = services.GetRequiredService<IEnvironment>();

            // Session ID that is inherited across processes.
            string sessionId = environment.GetEnvironmentVariable(AppInsightsProvider.SessionIdEnvVar) ?? Guid.NewGuid().ToString();

            // We want to flow down the processes the same session id for correlation purposes.
            environment.SetEnvironmentVariable(AppInsightsProvider.SessionIdEnvVar, sessionId);
            return new AppInsightsProvider(
                services.GetRequiredService<IEnvironment>(),
                services.GetTestApplicationCancellationTokenSource(),
                services.GetTask(),
                services.GetLoggerFactory(),
                services.GetClock(),
                services.GetConfiguration(),
                services.GetRequiredService<ITelemetryInformation>(),
                new AppInsightTelemetryClientFactory(),
                sessionId);
        });
#pragma warning restore IDE0022 // Use expression body for method
    }
}
