// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// Retry-collapsing half of the merger: everything behind <see cref="CtrfMergeMode.CollapseRetryAttempts"/>,
/// which folds successive attempts of the same logical test into a single row carrying its own history.
/// </summary>
internal static partial class CtrfReportMerger
{
    // Fields a CTRF retry attempt object (section 11) shares with a test object and that carry over verbatim when
    // a non-final attempt is folded into 'retryAttempts[]'. 'attempt' and 'status' are handled separately because
    // they are required, and 'attemptId' is listed here because an input that assigned one keeps it.
    private static readonly string[] RetryAttemptFields =
    [
        "attemptId",
        "duration",
        "message",
        "trace",
        "line",
        "snippet",
        "stdout",
        "stderr",
        "start",
        "stop",
        "attachments",
    ];

    /// <summary>
    /// Folds successive attempts of the same logical test — the tests[] rows contributed, in attempt order, by
    /// the per-attempt documents of one orchestrated retry run — into a single row per test.
    /// </summary>
    /// <remarks>
    /// The retry model confirmed in ctrf-io/ctrf#58: the last attempt's outcome IS the test object, and
    /// <c>retryAttempts[]</c> holds attempts <c>1..N-1</c> (the initial execution plus any earlier retries),
    /// numbered from 1, so <c>retries == retryAttempts.length</c> and the final attempt is <c>retries + 1</c>.
    /// The test object therefore keeps the FINAL attempt's <c>duration</c> (and <c>start</c>/<c>stop</c>), rather
    /// than a sum across attempts. Attempts an input already recorded in its own <c>retryAttempts[]</c> (in-process
    /// retries within a single attempt process) are flattened into the same history so no execution is lost.
    /// </remarks>
    private static JsonArray CollapseRetryAttempts(JsonArray tests)
    {
        var slots = new List<(JsonObject Final, List<JsonObject> Priors)>();
        var byIdentity = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (JsonNode? test in tests)
        {
            // Non-objects were already rejected during ingestion; this only re-establishes the type.
            if (test is not JsonObject testObject)
            {
                continue;
            }

            // A row we cannot identify gets its own slot: fusing unrelated rows would lose results, whereas an
            // uncollapsed duplicate is merely redundant.
            if (GetTestIdentity(testObject) is not string identity)
            {
                slots.Add((testObject, []));
                continue;
            }

            if (byIdentity.TryGetValue(identity, out int index))
            {
                (JsonObject previousFinal, List<JsonObject> priors) = slots[index];
                priors.Add(previousFinal);
                slots[index] = (testObject, priors);
            }
            else
            {
                byIdentity.Add(identity, slots.Count);
                slots.Add((testObject, []));
            }
        }

        var collapsed = new JsonArray();
        foreach ((JsonObject final, List<JsonObject> priors) in slots)
        {
            collapsed.Add(BuildCollapsedTest(final, priors));
        }

        return collapsed;
    }

    /// <summary>
    /// Computes the key identifying the logical test a row describes, preferring the CTRF <c>testId</c>, then the
    /// legacy <c>id</c>, then the producer-supplied <c>extra.uid</c>, and finally the suite path plus name and the
    /// other stable discriminators the Test object offers. Returns <see langword="null"/> when the row carries
    /// none of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifier order follows CTRF 9.1: <c>id</c> is a stable test-case identifier that consumers treat as
    /// legacy, using <c>testId</c> in preference only when both are present. Ignoring an <c>id</c>-only report
    /// would drop it to the heuristic fallback and risk fusing distinct same-named tests.
    /// </para>
    /// <para>
    /// The fallback length-prefixes every component rather than just separating them. A CTRF <c>name</c> or suite
    /// segment is an arbitrary non-empty string, so it may itself contain the separator: with plain separation,
    /// <c>suite: ["A"], name: "B\u001fC"</c> and <c>suite: ["A", "B"], name: "C"</c> would produce the same key
    /// and collapse two unrelated tests into one, silently dropping a result. Length prefixes make the encoding
    /// unambiguous whatever the components contain.
    /// </para>
    /// <para>
    /// The fallback also folds in <c>filePath</c> (9.19) and <c>parameters</c> (9.30), because suite plus name
    /// alone is not unique: parameterized rows share both while differing only in their parameters, and
    /// same-named tests in different files differ only by path. If a producer were to serialize <c>parameters</c>
    /// inconsistently between attempts, the effect is that a retry is not folded — a duplicated row rather than a
    /// lost result, which is the safe direction to fail in.
    /// </para>
    /// </remarks>
    private static string? GetTestIdentity(JsonObject test)
    {
        if (ReadString(test, "testId") is { Length: > 0 } testId)
        {
            return $"testId\u001f{testId}";
        }

        if (ReadString(test, "id") is { Length: > 0 } id)
        {
            return $"id\u001f{id}";
        }

        // `extra` is free-form, so a foreign producer may well have written a string or an array there; indexing
        // anything but an object by property name throws.
        if (test["extra"] is JsonObject extra && ReadString(extra, "uid") is { Length: > 0 } uid)
        {
            return $"uid\u001f{uid}";
        }

        if (ReadString(test, "name") is not { Length: > 0 } name)
        {
            return null;
        }

        var identity = new StringBuilder("name");
        if (test["suite"] is JsonArray suite)
        {
            foreach (JsonNode? segment in suite)
            {
                // A suite segment is normally a string, but the document is untrusted. Fall back to the
                // segment's JSON text so a non-string segment still contributes a distinct, deterministic part
                // of the key instead of throwing on the string conversion.
                identity.Append('\u001f');
                IdentityKeyBuilder.AppendLengthPrefixedComponent(
                    identity,
                    segment is JsonValue segmentValue && segmentValue.TryGetValue(out string? segmentText)
                        ? segmentText
                        : segment?.ToJsonString());
            }
        }

        identity.Append('\u001f');
        IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, name);
        identity.Append('\u001f');
        IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, ReadString(test, "filePath"));
        identity.Append('\u001f');
        IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, test["parameters"]?.ToJsonString());
        return identity.ToString();
    }

    private static JsonNode BuildCollapsedTest(JsonObject final, List<JsonObject> priors)
    {
        var collapsed = (JsonObject)final.DeepClone();

        // Nothing was merged into this row, so it is passed through rather than synthesized: its own
        // `retryAttempts[]` (from in-process retries the producer already recorded) stays exactly as written.
        // Below, once there ARE priors, the history becomes the merger's own array — it has to be rebuilt and
        // renumbered into a contiguous 1..N-1 — which is why those entries are reshaped and these are not.
        if (priors.Count == 0)
        {
            return collapsed;
        }

        var history = new JsonArray();
        foreach (JsonObject prior in priors)
        {
            AppendAttempts(history, prior);
        }

        // In-process retries observed by the final attempt precede its own outcome in the history.
        if (collapsed["retryAttempts"] is JsonArray finalAttempts)
        {
            AppendNestedAttempts(history, finalAttempts);
        }

        bool anyFailed = false;
        for (int i = 0; i < history.Count; i++)
        {
            if (history[i] is not JsonObject attempt)
            {
                continue;
            }

            attempt["attempt"] = i + 1;
            anyFailed |= ReadString(attempt, "status") == "failed";
        }

        collapsed["retryAttempts"] = history;
        collapsed["retries"] = history.Count;

        // CTRF 9.22: flaky only when the FINAL status is passed after at least one failed attempt. Recomputed
        // (rather than inherited) because an input's own flag only describes the attempt that produced it.
        if (ReadString(collapsed, "status") == "passed" && anyFailed)
        {
            collapsed["flaky"] = true;
        }
        else
        {
            collapsed.Remove("flaky");
        }

        return collapsed;
    }

    /// <summary>
    /// Appends the executions a non-final attempt row represents — the attempts it already nested, then its own
    /// outcome — to the retry history being built.
    /// </summary>
    private static void AppendAttempts(JsonArray history, JsonObject test)
    {
        if (test["retryAttempts"] is JsonArray nested)
        {
            AppendNestedAttempts(history, nested);
        }

        history.Add(ToRetryAttempt(test));
    }

    /// <summary>
    /// Copies the retry attempts an input already recorded into the history being built, projecting each through
    /// the same section 11 shaping as a promoted test row so a foreign producer's nested attempt cannot smuggle a
    /// wrong-typed status or a disallowed field into the merged history. Entries that are not objects are skipped:
    /// they could not describe an execution.
    /// </summary>
    private static void AppendNestedAttempts(JsonArray history, JsonArray nested)
    {
        foreach (JsonNode? attempt in nested)
        {
            if (attempt is JsonObject attemptObject)
            {
                history.Add(ToRetryAttempt(attemptObject));
            }
        }
    }

    /// <summary>
    /// Projects a test row onto the retry attempt object of CTRF section 11, which allows a narrower set of
    /// fields than a test: everything else (name, suite, tags, labels, ...) either belongs to the collapsed test
    /// object or, like <c>rawStatus</c>, moves under <c>extra</c>, the only permitted extension point.
    /// </summary>
    private static JsonNode ToRetryAttempt(JsonObject test)
    {
        // 'attempt' is assigned by the caller, once the position of this execution in the history is known.
        var attempt = new JsonObject
        {
            ["attempt"] = 1,
            ["status"] = ReadStatus(test),
        };

        foreach (string field in RetryAttemptFields)
        {
            if (test[field] is JsonNode value)
            {
                attempt[field] = value.DeepClone();
            }
        }

        JsonObject? extra = test["extra"] is JsonObject testExtra ? (JsonObject)testExtra.DeepClone() : null;
        if (test["rawStatus"] is JsonNode rawStatus)
        {
            extra ??= [];
            extra["rawStatus"] = rawStatus.DeepClone();
        }

        if (extra is { Count: > 0 })
        {
            attempt["extra"] = extra;
        }

        return attempt;
    }
}
