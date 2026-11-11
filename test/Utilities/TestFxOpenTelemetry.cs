// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NATIVE_AOT
using System.Text.Json;

using Microsoft.Testing.Platform.Builder;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Testing.Extensions;

internal static class TestFxOpenTelemetry
{
    internal const string TraceDirectoryVariable = "TESTFX_OTEL_TRACE_DIRECTORY";

    internal static IDisposable? AddTestFxOpenTelemetry(this ITestApplicationBuilder builder)
    {
        if (IsDisabled(Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED")))
        {
            return null;
        }

        string? directory = Environment.GetEnvironmentVariable(TraceDirectoryVariable);
        Resource? resource = null;
        TraceFileExporter? exporter = null;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            resource = ResourceBuilder.CreateEmpty()
                .AddTestingPlatformResource()
                .AddAttributes(GetExecutionAttributes(Environment.GetEnvironmentVariable))
                .Build();
            exporter = new TraceFileExporter(directory, resource);
        }

        try
        {
            builder.AddOpenTelemetryProvider(
                tracing =>
                {
                    tracing.AddTestingPlatformInstrumentation();
                    if (resource is not null && exporter is not null)
                    {
                        tracing
                            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddAttributes(resource.Attributes))
                            .SetSampler(new AlwaysOnSampler())
                            .AddProcessor(new SimpleActivityExportProcessor(exporter));
                    }
                },
                metrics => metrics.AddTestingPlatformInstrumentation());
            return exporter;
        }
        catch
        {
            exporter?.Dispose();
            throw;
        }
    }

    internal static bool IsDisabled(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<KeyValuePair<string, object>> GetExecutionAttributes(Func<string, string?> getEnvironmentVariable)
    {
        Assembly assembly = typeof(TestFxOpenTelemetry).Assembly;
        yield return new("testfx.build.configuration", assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? string.Empty);
        yield return new("testfx.module.version", GetVersion(assembly));
        yield return new("testfx.module.mvid", assembly.ManifestModule.ModuleVersionId.ToString("D"));
        yield return new("testfx.target.framework", assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? string.Empty);
        if (assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "TestFx.NETCoreSdkVersion")?.Value is { Length: > 0 } sdkVersion)
        {
            yield return new("testfx.sdk.version", sdkVersion);
        }

        yield return new("testfx.platform.version", GetVersion(typeof(ITestApplicationBuilder).Assembly));
        yield return new("testfx.opentelemetry.version", GetVersion(typeof(Sdk).Assembly));
        yield return new("process.arch", RuntimeInformation.ProcessArchitecture.ToString());
        yield return new("host.cpu.logical_count", Environment.ProcessorCount);
        yield return new("testfx.trace.collection", "synchronous-jsonl-v1");
        yield return new("testfx.trace.capture_test_output", false);

        foreach ((string attribute, string variable) in new (string, string)[]
        {
            ("testfx.ci.job.name", "SYSTEM_JOBNAME"),
            ("testfx.ci.job.attempt", "SYSTEM_JOBATTEMPT"),
            ("testfx.ci.stage.attempt", "SYSTEM_STAGEATTEMPT"),
            ("testfx.ci.agent.name", "AGENT_NAME"),
            ("testfx.ci.agent.os", "AGENT_OS"),
            ("testfx.ci.image.name", "IMAGEOS"),
            ("testfx.ci.image.version", "IMAGEVERSION"),
            ("testfx.vcs.ref", "BUILD_SOURCEBRANCH"),
            ("testfx.run.logical_id", "TESTINGPLATFORM_LOGICAL_RUN_ID"),
            ("testfx.run.execution_id", "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"),
            ("testfx.run.attempt", "TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"),
        })
        {
            if (getEnvironmentVariable(variable) is { Length: > 0 } value)
            {
                yield return new(attribute, value);
            }
        }
    }

    private static string GetVersion(Assembly assembly)
        => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version!.ToString();

    internal sealed class TraceFileExporter : BaseExporter<Activity>
    {
#if NET9_0_OR_GREATER
        private readonly System.Threading.Lock _gate = new();
#else
        private readonly object _gate = new();
#endif
        private readonly TextWriter _writer;
        private readonly TextWriter _errors;
        private long _spanCount;
        private long _failedExports;
        private bool _disposed;

        internal TraceFileExporter(string directory, Resource resource)
            : this(CreateWriter(directory), resource, Console.Error)
        {
        }

        internal TraceFileExporter(TextWriter writer, Resource resource, TextWriter errors)
        {
            _writer = writer;
            _errors = errors;
            try
            {
                WriteRecord(json =>
                {
                    json.WriteString("recordType", "resource");
                    json.WriteString("format", "testfx-otel-jsonl");
                    json.WriteNumber("schemaVersion", 1);
                    json.WriteString("runId", Guid.NewGuid().ToString("N"));
                    json.WriteString("createdUtc", DateTime.UtcNow);
                    WriteAttributes(json, "attributes", resource.Attributes);
                });
            }
            catch
            {
                _writer.Dispose();
                throw;
            }
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    _errors.WriteLine("TestFx OpenTelemetry export attempted after disposal.");
                    return ExportResult.Failure;
                }

                if (_failedExports > 0)
                {
                    _failedExports++;
                    return ExportResult.Failure;
                }

                try
                {
                    foreach (Activity activity in batch)
                    {
                        WriteRecord(json => WriteActivity(json, activity));
                        _spanCount++;
                    }

                    return ExportResult.Success;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ReportFailure(ex);
                    return ExportResult.Failure;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            lock (_gate)
            {
                if (!disposing || _disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    WriteRecord(json =>
                    {
                        json.WriteString("recordType", "completion");
                        json.WriteString("status", _failedExports == 0 ? "complete" : "failed");
                        json.WriteNumber("spanCount", _spanCount);
                        json.WriteNumber("failedExports", _failedExports);
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ReportFailure(ex);
                }
                finally
                {
                    try
                    {
                        _writer.Dispose();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        ReportFailure(ex);
                    }
                }
            }

            base.Dispose(disposing);
        }

        private static StreamWriter CreateWriter(string directory)
        {
            Directory.CreateDirectory(directory);
            string module = typeof(TestFxOpenTelemetry).Assembly.GetName().Name!;
            string path = Path.Combine(directory, $"{module}-{Guid.NewGuid():N}.jsonl");
            return new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        }

        private static void WriteActivity(Utf8JsonWriter json, Activity activity)
        {
            json.WriteString("recordType", "span");
            json.WriteString("traceId", activity.TraceId.ToHexString());
            json.WriteString("spanId", activity.SpanId.ToHexString());
            json.WriteString("parentSpanId", activity.ParentSpanId.ToHexString());
            json.WriteString("traceState", activity.TraceStateString);
            json.WriteNumber("traceFlags", (int)activity.ActivityTraceFlags);
            json.WriteString("name", activity.DisplayName);
            json.WriteString("operationName", activity.OperationName);
            json.WriteString("sourceName", activity.Source.Name);
            json.WriteString("sourceVersion", activity.Source.Version);
            json.WriteString("kind", activity.Kind.ToString());
            json.WriteString("startTimeUtc", activity.StartTimeUtc);
            json.WriteString("endTimeUtc", activity.StartTimeUtc + activity.Duration);
            json.WriteNumber("durationTicks", activity.Duration.Ticks);
            json.WriteString("status", activity.Status.ToString());
            json.WriteString("statusDescription", activity.StatusDescription);
            WriteAttributes(json, "attributes", activity.TagObjects);
            json.WriteStartArray("events");
            foreach (ActivityEvent activityEvent in activity.Events)
            {
                json.WriteStartObject();
                json.WriteString("name", activityEvent.Name);
                json.WriteString("timestampUtc", activityEvent.Timestamp.UtcDateTime);
                WriteAttributes(json, "attributes", activityEvent.Tags);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartArray("links");
            foreach (ActivityLink link in activity.Links)
            {
                json.WriteStartObject();
                json.WriteString("traceId", link.Context.TraceId.ToHexString());
                json.WriteString("spanId", link.Context.SpanId.ToHexString());
                json.WriteString("traceState", link.Context.TraceState);
                json.WriteNumber("traceFlags", (int)link.Context.TraceFlags);
                json.WriteBoolean("isRemote", link.Context.IsRemote);
                WriteAttributes(json, "attributes", link.Tags);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        private static void WriteAttributes<T>(Utf8JsonWriter json, string name, IEnumerable<KeyValuePair<string, T>>? attributes)
        {
            json.WriteStartArray(name);
            if (attributes is not null)
            {
                foreach (KeyValuePair<string, T> attribute in attributes)
                {
                    if (attribute.Key is "test.output.stdout" or "test.output.stderr" or "test.stdout" or "test.stderr")
                    {
                        continue;
                    }

                    json.WriteStartObject();
                    json.WriteString("key", attribute.Key);
                    json.WritePropertyName("value");
                    WriteValue(json, attribute.Value);
                    json.WriteEndObject();
                }
            }

            json.WriteEndArray();
        }

        private static void WriteValue(Utf8JsonWriter json, object? value)
        {
            switch (value)
            {
                case null:
                    json.WriteNullValue();
                    break;
                case string text:
                    json.WriteStringValue(text);
                    break;
                case bool boolean:
                    json.WriteBooleanValue(boolean);
                    break;
                case byte or sbyte or short or ushort or int or uint or long:
                    json.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case ulong unsigned:
                    json.WriteNumberValue(unsigned);
                    break;
                case float or double:
                    double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    if (double.IsNaN(number) || double.IsInfinity(number))
                    {
                        json.WriteStringValue(number.ToString("R", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        json.WriteNumberValue(number);
                    }

                    break;
                case Array array:
                    json.WriteStartArray();
                    foreach (object? item in array)
                    {
                        WriteValue(json, item);
                    }

                    json.WriteEndArray();
                    break;
                default:
                    json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private void WriteRecord(Action<Utf8JsonWriter> write)
        {
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                write(json);
                json.WriteEndObject();
            }

            _writer.WriteLine(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length)));
            _writer.Flush();
        }

        private void ReportFailure(Exception exception)
        {
            _failedExports++;
            _errors.WriteLine($"TestFx OpenTelemetry trace export failed: {exception}");
        }
    }
}
#endif
