// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    // CTRF `stdout`/`stderr` are typed as an array of lines (each item is one line
    // of captured output). Splitting on LF (handling optional CR) preserves the
    // original line structure for consumers that present output per-line.
    private static void WriteOutputLines(Utf8JsonWriter writer, string propertyName, string? output)
    {
        if (output is null)
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        int start = 0;
        for (int i = 0; i < output.Length; i++)
        {
            if (output[i] == '\n')
            {
                int end = i;
                if (end > start && output[end - 1] == '\r')
                {
                    end--;
                }

                writer.WriteStringValue(output.AsSpan(start, end - start));
                start = i + 1;
            }
        }

        if (start < output.Length)
        {
            // Emit the trailing segment after the last LF (no trailing entry when
            // the input ends with LF — a trailing newline isn't an additional line).
            int end = output.Length;
            if (end > start && output[end - 1] == '\r')
            {
                end--;
            }

            writer.WriteStringValue(output.AsSpan(start, end - start));
        }

        writer.WriteEndArray();
    }

    private static List<CollapsedTestResult> CollapseAttempts(CapturedTestResult[] results)
    {
        // For each UID, group all captures in arrival order: the latest entry becomes the
        // final test record, earlier entries become `retryAttempts[]`. Preserves the
        // insertion order of first-seen UIDs in the output (stable across runs).
        var byUid = new Dictionary<string, int>(StringComparer.Ordinal);
        var collapsed = new List<CollapsedTestResult>(results.Length);
        foreach (CapturedTestResult r in results)
        {
            if (byUid.TryGetValue(r.Uid, out int existingIndex))
            {
                CollapsedTestResult existing = collapsed[existingIndex];
                existing.PriorAttempts.Add(existing.Final);
                collapsed[existingIndex] = existing with { Final = r };
            }
            else
            {
                byUid.Add(r.Uid, collapsed.Count);
                collapsed.Add(new CollapsedTestResult(r));
            }
        }

        return collapsed;
    }

    private readonly record struct CollapsedTestResult(CapturedTestResult Final)
    {
        public List<CapturedTestResult> PriorAttempts { get; } = [];

        // CTRF "flaky" is true iff the final status is "passed" AND at least one
        // previous attempt failed.
        public bool IsFlaky
        {
            get
            {
                if (Final.Status != "passed" || PriorAttempts.Count == 0)
                {
                    return false;
                }

                foreach (CapturedTestResult attempt in PriorAttempts)
                {
                    if (attempt.Status == "failed")
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
