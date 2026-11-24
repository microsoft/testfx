// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private static void AccumulateCounters(XElement? counters, List<string> attributeOrder, Dictionary<string, long> sums)
    {
        if (counters is null)
        {
            return;
        }

        foreach (XAttribute attribute in counters.Attributes())
        {
            string name = attribute.Name.LocalName;
            if (!sums.ContainsKey(name))
            {
                attributeOrder.Add(name);
                sums[name] = 0;
            }

            if (long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                sums[name] += value;
            }
        }
    }

    private static XElement BuildTimes(DateTimeOffset? earliestCreation, DateTimeOffset? earliestQueuing, DateTimeOffset? earliestStart, DateTimeOffset? latestFinish)
    {
        var times = new XElement(NamespaceUri + "Times");
        if (earliestCreation is { } creation)
        {
            times.SetAttributeValue("creation", creation);
        }

        if (earliestQueuing is { } queuing)
        {
            times.SetAttributeValue("queuing", queuing);
        }

        if (earliestStart is { } start)
        {
            times.SetAttributeValue("start", start);
        }

        if (latestFinish is { } finish)
        {
            times.SetAttributeValue("finish", finish);
        }

        return times;
    }

    private static XElement BuildTestSettings(Guid runId, string runName)
    {
        var testSettings = new XElement(
            NamespaceUri + "TestSettings",
            new XAttribute("name", "default"),
            new XAttribute("id", CreateDeterministicSettingsId(runId)));
        testSettings.Add(new XElement(NamespaceUri + "Deployment", new XAttribute("runDeploymentRoot", GetConfinedDeploymentRootLeaf(runId, runName))));
        return testSettings;
    }

    // Fixed namespace used to derive the (deterministic) TestSettings id from the run id, so the settings
    // id is stable across retries yet distinct from the run id itself.
    private static readonly Guid TestSettingsIdNamespace = new("b3f8f9d1-2e4a-4c6b-9f0d-7a1c2e5b8d40");

    /// <summary>
    /// Derives a stable <c>TestSettings</c> id from <paramref name="runId"/> by XoR-ing it with a fixed
    /// namespace, so identical inputs produce identical merged XML (RFC 018 idempotency) without emitting
    /// the run id verbatim as the settings id.
    /// </summary>
    private static Guid CreateDeterministicSettingsId(Guid runId)
    {
        byte[] runBytes = runId.ToByteArray();
        byte[] namespaceBytes = TestSettingsIdNamespace.ToByteArray();
        byte[] result = new byte[16];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)(runBytes[i] ^ namespaceBytes[i]);
        }

        return new Guid(result);
    }

    /// <summary>
    /// Produces a single, confined, per-run-unique deployment-root leaf from <paramref name="runName"/> and
    /// <paramref name="runId"/> for use both in the emitted <c>TestSettings/Deployment/@runDeploymentRoot</c>
    /// and in attachment relocation, so the two always agree and neither can escape the output directory. The
    /// file-name sanitizer already replaces path separators and reserved names, so the only residual escape
    /// values are <c>.</c> and <c>..</c>, which are prefixed to keep the leaf confined. The run id (which is
    /// deterministic for a given logical run, preserving RFC 018 idempotency) is appended so a merge never
    /// reuses the deployment tree of a previously committed report at the same output path — relocation then
    /// cannot mutate that prior report's referenced files, and a failed merge leaves it consistent.
    /// </summary>
    private static string GetConfinedDeploymentRootLeaf(Guid runId, string runName)
    {
        string leaf = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(runName);
        leaf = leaf is "." or ".." ? "_" + leaf : leaf;
        return leaf + "." + runId.ToString("N", CultureInfo.InvariantCulture);
    }

    private static XElement BuildResultSummary(string outcome, List<string> counterAttributeOrder, Dictionary<string, long> counterSums, XElement? output, XElement runInfos, XElement collectorDataEntries, XElement resultFiles)
    {
        var counters = new XElement(NamespaceUri + "Counters");
        foreach (string name in counterAttributeOrder)
        {
            counters.SetAttributeValue(name, counterSums[name].ToString(CultureInfo.InvariantCulture));
        }

        var resultSummary = new XElement(
            NamespaceUri + "ResultSummary",
            new XAttribute("outcome", outcome),
            counters);

        // Emit the optional children in TRX schema order (Counters, Output, RunInfos, CollectorDataEntries,
        // ResultFiles), and only when they carry content, matching the single-run producer's shape.
        if (output is not null)
        {
            resultSummary.Add(output);
        }

        if (runInfos.HasElements)
        {
            resultSummary.Add(runInfos);
        }

        if (collectorDataEntries.HasElements)
        {
            resultSummary.Add(collectorDataEntries);
        }

        if (resultFiles.HasElements)
        {
            resultSummary.Add(resultFiles);
        }

        return resultSummary;
    }

    /// <summary>
    /// Merges the run-level <c>&lt;Output&gt;</c> elements of every input <c>ResultSummary</c> into a single
    /// one so run-level std streams and informational/skipped <c>TextMessages</c> survive the merge. The
    /// scalar text children (<c>StdOut</c>/<c>StdErr</c>/<c>DebugTrace</c>) are concatenated and the
    /// <c>Message</c> entries under <c>TextMessages</c> are unioned. Returns <see langword="null"/> when
    /// nothing is present.
    /// </summary>
    private static XElement? MergeResultSummaryOutputs(List<XElement> outputs)
    {
        if (outputs.Count == 0)
        {
            return null;
        }

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var debugTrace = new StringBuilder();
        var messages = new List<XElement>();

        foreach (XElement output in outputs)
        {
            AppendOutputText(stdOut, FindChild(output, "StdOut"));
            AppendOutputText(stdErr, FindChild(output, "StdErr"));
            AppendOutputText(debugTrace, FindChild(output, "DebugTrace"));

            if (FindChild(output, "TextMessages") is { } textMessages)
            {
                foreach (XElement message in textMessages.Elements().Where(e => string.Equals(e.Name.LocalName, "Message", StringComparison.Ordinal)))
                {
                    messages.Add(new XElement(message));
                }
            }
        }

        var merged = new XElement(NamespaceUri + "Output");
        if (stdOut.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "StdOut", stdOut.ToString()));
        }

        if (stdErr.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "StdErr", stdErr.ToString()));
        }

        if (debugTrace.Length > 0)
        {
            merged.Add(new XElement(NamespaceUri + "DebugTrace", debugTrace.ToString()));
        }

        if (messages.Count > 0)
        {
            var textMessages = new XElement(NamespaceUri + "TextMessages");
            textMessages.Add(messages);
            merged.Add(textMessages);
        }

        return merged.HasElements ? merged : null;
    }

    private static void AppendOutputText(StringBuilder builder, XElement? element)
    {
        if (element is null || RoslynString.IsNullOrEmpty(element.Value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(element.Value);
    }

    private static bool TryParseDateTimeOffset(string? value, out DateTimeOffset result)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            result = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }
}
