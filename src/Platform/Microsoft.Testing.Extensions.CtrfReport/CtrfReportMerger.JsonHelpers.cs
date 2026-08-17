// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// Stateless helpers shared by the merge and retry-collapsing halves of the merger: defensive readers for
/// untrusted JSON documents, environment deduplication, and deterministic report id derivation.
/// </summary>
internal static partial class CtrfReportMerger
{
    /// <summary>
    /// Reads a string property, treating a value of any other JSON type as absent. The explicit
    /// <c>(string?)</c> conversion on a <see cref="JsonNode"/> THROWS for a non-string value rather than
    /// yielding <see langword="null"/>, and every document reaching the merger is untrusted.
    /// </summary>
    private static string? ReadString(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>
    /// Reads a CTRF status, normalizing anything outside the vocabulary section 11.3 allows — a wrong-typed
    /// value, or a status this producer invented — to <c>other</c>. Copying such a value verbatim into a retry
    /// attempt would make the merged document schema-invalid, and <c>other</c> is both a legal status and the
    /// bucket the summary already counts it in.
    /// </summary>
    private static string ReadStatus(JsonObject owner)
    {
        string? status = ReadString(owner, "status");
        return status is "passed" or "failed" or "skipped" or "pending" or "other" ? status : "other";
    }

    /// <summary>
    /// Reads an integral property from a JSON node, tolerating anything the node may actually be. The node comes
    /// straight out of an untrusted document — <c>results.summary</c> is not guaranteed to be an object — so a
    /// non-object must read as "absent" rather than throw: indexing a <see cref="JsonValue"/> by property name is
    /// an <see cref="InvalidOperationException"/>.
    /// </summary>
    private static bool TryReadLong(JsonNode node, string propertyName, out long value)
    {
        value = 0;
        if (node is not JsonObject jsonObject || jsonObject[propertyName] is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue(out long longValue))
        {
            value = longValue;
            return true;
        }

        if (jsonValue.TryGetValue(out double doubleValue))
        {
            value = (long)doubleValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a merged environment containing only the fields that every input's environment agrees on
    /// (so a value that differs across CI agents is dropped rather than attributed to all tests). The
    /// module-specific <c>extra.testApplication</c> and <c>extra.exitCode</c> fields are always dropped.
    /// Returns <see langword="null"/> when no environment survives.
    /// </summary>
    private static JsonObject? BuildCommonEnvironment(IReadOnlyList<JsonObject> environments, int reportCount)
    {
        // A field can only be common to all inputs if every accepted report supplied an environment; a
        // report with no environment is a disagreement (the field is absent there).
        if (environments.Count == 0 || environments.Count != reportCount)
        {
            return null;
        }

        var merged = new JsonObject();
        foreach (KeyValuePair<string, JsonNode?> field in environments[0])
        {
            if (field.Key == "extra")
            {
                continue;
            }

            string firstValue = field.Value?.ToJsonString() ?? "null";
            if (environments.All(e => (e[field.Key]?.ToJsonString() ?? "null") == firstValue))
            {
                merged[field.Key] = field.Value?.DeepClone();
            }
        }

        if (environments[0]["extra"] is JsonObject firstExtra)
        {
            var extra = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> field in firstExtra)
            {
                if (field.Key is "testApplication" or "exitCode")
                {
                    continue;
                }

                string firstValue = field.Value?.ToJsonString() ?? "null";
                if (environments.All(e => e["extra"] is JsonObject extraObject && (extraObject[field.Key]?.ToJsonString() ?? "null") == firstValue))
                {
                    extra[field.Key] = field.Value?.DeepClone();
                }
            }

            if (extra.Count > 0)
            {
                merged["extra"] = extra;
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    /// <summary>
    /// Derives a stable <c>reportId</c> from the accepted CTRF input reports and the merge mode, so identical
    /// inputs merged the same way reproduce the same id on every retry (RFC 018 idempotency) without a random
    /// source or reusing an input report's id, while the same inputs merged a DIFFERENT way — which yields a
    /// materially different document — get a distinct id, as CTRF 5.3 requires. Only the payloads that passed
    /// CTRF validation are hashed, so a rejected non-CTRF input cannot alter the merged report's identity. A
    /// non-cryptographic 128-bit FNV-1a fill is sufficient here — the id only needs to be deterministic and
    /// collision-resistant enough to identify a merged report, not secret.
    /// </summary>
    private static string CreateDeterministicReportId(IReadOnlyList<string> acceptedReports, CtrfMergeMode mode)
        => CreateDeterministicId(acceptedReports, (ulong)mode).ToString("D");

    internal static Guid CreateDeterministicId(IReadOnlyList<string> values)
        => CreateDeterministicId(values, discriminator: null);

    private static Guid CreateDeterministicId(IReadOnlyList<string> values, ulong? discriminator)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong hashLow = 14695981039346656037UL;
        ulong hashHigh = 0x9E3779B97F4A7C15UL;

        if (discriminator is { } value)
        {
            // The report merge mode is a discriminator: the same inputs concatenated and collapsed are two
            // materially different documents, and CTRF 5.3 wants a distinct reportId for each.
            hashLow = (hashLow ^ value) * fnvPrime;
            hashHigh = (hashHigh ^ (value + 1UL)) * fnvPrime;
        }

        foreach (string input in values)
        {
            foreach (char c in input)
            {
                hashLow = (hashLow ^ c) * fnvPrime;
                hashHigh = (hashHigh ^ c) * fnvPrime;
            }

            // Fold in each input's length so different chunk boundaries (e.g. ["ab","c"] vs ["a","bc"])
            // never collide.
            hashLow = (hashLow ^ (ulong)input.Length) * fnvPrime;
            hashHigh = (hashHigh ^ ((ulong)input.Length + 1UL)) * fnvPrime;
        }

        byte[] bytes = new byte[16];
        for (int i = 0; i < 8; i++)
        {
            // Serialize explicitly in little-endian order so identical inputs produce the same identifier on
            // every architecture. Guid(byte[]) has a stable interpretation once the byte sequence is fixed.
            bytes[i] = (byte)(hashLow >> (i * 8));
            bytes[i + 8] = (byte)(hashHigh >> (i * 8));
        }

        return new Guid(bytes);
    }

    private static long Min(long? current, long candidate)
        => current is null || candidate < current ? candidate : current.Value;

    private static long Max(long? current, long candidate)
        => current is null || candidate > current ? candidate : current.Value;
}
