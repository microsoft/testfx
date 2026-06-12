// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private static void WriteTest(Utf8JsonWriter writer, CollapsedTestResult c)
    {
        CapturedTestResult r = c.Final;
        writer.WriteStartObject();

        // CTRF spec: tests[i].name MUST be a non-empty string. Fall back to UID
        // (also non-empty) when the framework didn't supply a display name.
        string name = RoslynString.IsNullOrEmpty(r.DisplayName) ? r.Uid : r.DisplayName;
        writer.WriteString("name", name);
        writer.WriteString("status", r.Status);
        writer.WriteNumber("duration", (long)Math.Max(0, r.Duration.TotalMilliseconds));

        if (r.StartTime is { } start)
        {
            writer.WriteNumber("start", start.ToUnixTimeMilliseconds());
        }

        if (r.EndTime is { } end)
        {
            writer.WriteNumber("stop", end.ToUnixTimeMilliseconds());
        }

        if (r.RawStatus is not null)
        {
            writer.WriteString("rawStatus", r.RawStatus);
        }

        // CTRF `suite` is an array of strings (minItems: 1) representing the test
        // hierarchy (e.g. ["MyNamespace", "MyClass"]).
        if (r.Namespace is not null || r.ClassName is not null)
        {
            writer.WritePropertyName("suite");
            writer.WriteStartArray();
            if (r.Namespace is not null)
            {
                writer.WriteStringValue(r.Namespace);
            }

            if (r.ClassName is not null)
            {
                writer.WriteStringValue(r.ClassName);
            }

            writer.WriteEndArray();
        }

        if (r.ErrorMessage is not null)
        {
            writer.WriteString("message", r.ErrorMessage);
        }

        if (r.StackTrace is not null)
        {
            writer.WriteString("trace", r.StackTrace);
        }

        if (r.FilePath is not null)
        {
            writer.WriteString("filePath", r.FilePath);
        }

        if (r.Line is { } lineNumber)
        {
            writer.WriteNumber("line", lineNumber);
        }

        if (c.PriorAttempts.Count > 0)
        {
            writer.WriteNumber("retries", c.PriorAttempts.Count);
            writer.WritePropertyName("retryAttempts");
            writer.WriteStartArray();
            for (int i = 0; i < c.PriorAttempts.Count; i++)
            {
                WriteRetryAttempt(writer, c.PriorAttempts[i], attemptNumber: i + 1);
            }

            writer.WriteEndArray();
        }

        if (c.IsFlaky)
        {
            writer.WriteBoolean("flaky", true);
        }

        WriteOutputLines(writer, "stdout", r.StandardOutput);
        WriteOutputLines(writer, "stderr", r.StandardError);

        // CTRF spec 9.14 (`tags`): top-level string array on the Test object used
        // for keyless classification. We promote MSTest `[TestCategory("…")]` trait
        // values here so CTRF consumers can filter/group by category without having
        // to walk the structured `labels` object. The full set of traits (including
        // TestCategory) is also emitted under `labels` below.
        if (r.Traits is { Count: > 0 })
        {
            bool tagsArrayStarted = false;
            foreach (KeyValuePair<string, string> trait in r.Traits)
            {
                if (string.Equals(trait.Key, "TestCategory", StringComparison.Ordinal))
                {
                    if (!tagsArrayStarted)
                    {
                        writer.WritePropertyName("tags");
                        writer.WriteStartArray();
                        tagsArrayStarted = true;
                    }

                    writer.WriteStringValue(trait.Value);
                }
            }

            if (tagsArrayStarted)
            {
                writer.WriteEndArray();
            }
        }

        // CTRF spec 9.15 (`labels`): top-level object on the Test object for
        // structured key/value test metadata. Per ctrf-io/ctrf#53, this is the
        // intended home for arbitrary framework-defined traits (Option A confirmed
        // by the spec maintainer), and array values are an accepted extension for
        // multi-valued keys (e.g. multiple `[TestCategory]` attributes on the same
        // MSTest method). We emit a scalar string for single-valued keys and an
        // array of strings when the same key appears more than once. Keys appear
        // in first-seen order; values keep their original declaration order.
        if (r.Traits is { Count: > 0 })
        {
            writer.WritePropertyName("labels");
            writer.WriteStartObject();
            for (int i = 0; i < r.Traits.Count; i++)
            {
                string key = r.Traits[i].Key;
                if (HasSameKeyEarlier(r.Traits, i))
                {
                    continue;
                }

                writer.WritePropertyName(key);

                int duplicateCount = 0;
                for (int j = i + 1; j < r.Traits.Count; j++)
                {
                    if (string.Equals(r.Traits[j].Key, key, StringComparison.Ordinal))
                    {
                        duplicateCount++;
                    }
                }

                if (duplicateCount == 0)
                {
                    writer.WriteStringValue(r.Traits[i].Value);
                }
                else
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(r.Traits[i].Value);
                    for (int j = i + 1; j < r.Traits.Count; j++)
                    {
                        if (string.Equals(r.Traits[j].Key, key, StringComparison.Ordinal))
                        {
                            writer.WriteStringValue(r.Traits[j].Value);
                        }
                    }

                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }

        // CTRF `extra` (free-form object) — the CTRF spec doesn't define a
        // dedicated stable identifier so we surface the MTP UID here for
        // cross-tool correlation, alongside other framework metadata.
        writer.WritePropertyName("extra");
        writer.WriteStartObject();
        writer.WriteString("uid", r.Uid);
        if (r.MethodName is not null)
        {
            writer.WriteString("method", r.MethodName);
        }

        if (r.ExceptionType is not null)
        {
            writer.WriteString("exceptionType", r.ExceptionType);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static bool HasSameKeyEarlier(IReadOnlyList<KeyValuePair<string, string>> traits, int index)
    {
        string key = traits[index].Key;
        for (int k = 0; k < index; k++)
        {
            if (string.Equals(traits[k].Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteRetryAttempt(Utf8JsonWriter writer, CapturedTestResult attempt, int attemptNumber)
    {
        writer.WriteStartObject();
        writer.WriteNumber("attempt", attemptNumber);
        writer.WriteString("status", attempt.Status);
        writer.WriteNumber("duration", (long)Math.Max(0, attempt.Duration.TotalMilliseconds));
        if (attempt.StartTime is { } start)
        {
            writer.WriteNumber("start", start.ToUnixTimeMilliseconds());
        }

        if (attempt.EndTime is { } end)
        {
            writer.WriteNumber("stop", end.ToUnixTimeMilliseconds());
        }

        if (attempt.ErrorMessage is not null)
        {
            writer.WriteString("message", attempt.ErrorMessage);
        }

        if (attempt.StackTrace is not null)
        {
            writer.WriteString("trace", attempt.StackTrace);
        }

        if (attempt.Line is { } line)
        {
            writer.WriteNumber("line", line);
        }

        WriteOutputLines(writer, "stdout", attempt.StandardOutput);
        WriteOutputLines(writer, "stderr", attempt.StandardError);

        if (attempt.RawStatus is not null || attempt.ExceptionType is not null)
        {
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            if (attempt.RawStatus is not null)
            {
                writer.WriteString("rawStatus", attempt.RawStatus);
            }

            if (attempt.ExceptionType is not null)
            {
                writer.WriteString("exceptionType", attempt.ExceptionType);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
