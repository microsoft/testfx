// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.OpenTelemetry;

/// <summary>
/// The subset of the standard OpenTelemetry SDK environment variables honored by the turnkey configuration.
/// </summary>
/// <remarks>
/// These names are defined by the OpenTelemetry specification rather than by Microsoft.Testing.Platform, which is
/// why they live in the extension rather than in the platform's own environment variable constants.
/// </remarks>
internal static class OpenTelemetryEnvironmentVariables
{
    internal const string ServiceName = "OTEL_SERVICE_NAME";
    internal const string ExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    internal const string TracesExporter = "OTEL_TRACES_EXPORTER";
    internal const string MetricsExporter = "OTEL_METRICS_EXPORTER";
    internal const string SdkDisabled = "OTEL_SDK_DISABLED";

    internal static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? value)
    {
        if (value is null)
        {
            return true;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
