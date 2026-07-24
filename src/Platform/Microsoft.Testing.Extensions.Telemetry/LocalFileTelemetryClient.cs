// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.Telemetry;

/// <summary>
/// A local, network-free <see cref="ITelemetryClient"/> that appends every telemetry event to a
/// file as a single JSON line (JSON Lines / NDJSON). It is the "local exporter" equivalent used to
/// verify what telemetry would be collected — without shipping anything to Application Insights.
/// It is selected instead of <see cref="AppInsightTelemetryClient"/> when the
/// <see cref="AppInsightsProvider.LocalExportPathEnvVar"/> environment variable points at a file.
/// </summary>
internal sealed class LocalFileTelemetryClient : ITelemetryClient
{
    private readonly string _filePath;
    private readonly string? _sessionId;
    private readonly string _osVersion;

    public LocalFileTelemetryClient(string filePath, string? currentSessionId, string osVersion)
    {
        _filePath = filePath;
        _sessionId = currentSessionId;
        _osVersion = osVersion;

        string? directory = Path.GetDirectoryName(_filePath);
        if (!RoslynString.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void TrackEvent(string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendJsonString(builder, "eventName", eventName);
        builder.Append(',');
        AppendJsonString(builder, "sessionId", _sessionId ?? string.Empty);
        builder.Append(',');
        AppendJsonString(builder, "osVersion", _osVersion);

        builder.Append(",\"properties\":{");
        bool first = true;
        foreach (KeyValuePair<string, string> property in properties)
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendJsonString(builder, property.Key, property.Value);
            first = false;
        }

        builder.Append("},\"metrics\":{");
        first = true;
        foreach (KeyValuePair<string, double> metric in metrics)
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendJsonKey(builder, metric.Key);
            builder.Append(metric.Value.ToString("R", CultureInfo.InvariantCulture));
            first = false;
        }

        builder.Append("}}");

        // TrackEvent is only ever invoked from the provider's single-consumer ingest loop, so no
        // synchronization is needed here (mirroring AppInsightTelemetryClient).
        File.AppendAllText(_filePath, builder.ToString() + Environment.NewLine);
    }

    // No-op: writes are flushed to disk synchronously as each event is tracked.
    public void Flush()
    {
    }

    private static void AppendJsonString(StringBuilder builder, string key, string value)
    {
        AppendJsonKey(builder, key);
        AppendEscaped(builder, value);
    }

    private static void AppendJsonKey(StringBuilder builder, string key)
    {
        AppendEscaped(builder, key);
        builder.Append(':');
    }

    private static void AppendEscaped(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
