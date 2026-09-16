// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport.Resources;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal static partial class HtmlReportMerger
{
    private static JsonObject ParseReport(string html)
    {
        JsonObject report;
        try
        {
            report = JsonNode.Parse(HtmlReportEngine.ExtractReportJson(html)) as JsonObject
                ?? throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html), ex);
        }

        bool isValidReport =
            string.Equals(ReadOptionalString(report, "schemaVersion"), "1", StringComparison.Ordinal)
            && string.Equals(ReadOptionalString(report, "generator"), GeneratorName, StringComparison.Ordinal)
            && report["tests"] is JsonArray tests
            && tests.All(test => test is JsonObject);

        return isValidReport
            ? report
            : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html));
    }

    private static string ReadRequiredString(JsonObject owner, string propertyName)
        => ReadOptionalString(owner, propertyName)
            ?? throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static string? ReadOptionalString(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static double ReadRequiredDouble(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out double number)
            ? number
            : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static DateTimeOffset ReadRequiredTimestamp(JsonObject owner, string propertyName)
        => TryReadTimestamp(owner, propertyName, out DateTimeOffset timestamp)
                ? timestamp
                : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static bool TryReadTimestamp(JsonObject owner, string propertyName, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParse(
            ReadOptionalString(owner, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);

    private static int? ReadOptionalInt(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;

    private static bool TryGetInt(JsonObject owner, string propertyName, out int value)
    {
        if (owner[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static void AddOptionalString(JsonObject owner, string propertyName, string? value)
    {
        if (value is not null)
        {
            owner[propertyName] = value;
        }
    }

    private static string? GetCommonString(IReadOnlyList<JsonObject> reports, string propertyName)
    {
        string? common = ReadOptionalString(reports[0], propertyName);
        for (int i = 1; i < reports.Count; i++)
        {
            if (!string.Equals(common, ReadOptionalString(reports[i], propertyName), StringComparison.Ordinal))
            {
                return null;
            }
        }

        return common;
    }

    private static bool TryGetCommonFramework(
        IReadOnlyList<JsonObject> reports,
        out string framework,
        out string frameworkUid,
        out string frameworkVersion)
    {
        framework = GetCommonString(reports, "framework") ?? string.Empty;
        frameworkUid = GetCommonString(reports, "frameworkUid") ?? string.Empty;
        frameworkVersion = GetCommonString(reports, "frameworkVersion") ?? string.Empty;
        if (framework.Length == 0 || frameworkUid.Length == 0 || frameworkVersion.Length == 0)
        {
            return false;
        }

        foreach (JsonObject report in reports)
        {
            if (!string.Equals(ReadOptionalString(report, "framework"), framework, StringComparison.Ordinal)
                || !string.Equals(ReadOptionalString(report, "frameworkUid"), frameworkUid, StringComparison.Ordinal)
                || !string.Equals(ReadOptionalString(report, "frameworkVersion"), frameworkVersion, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetCommonInt(IReadOnlyList<JsonObject> reports, string propertyName, out int common)
    {
        if (reports[0][propertyName] is not JsonValue first || !first.TryGetValue(out common))
        {
            common = default;
            return false;
        }

        for (int i = 1; i < reports.Count; i++)
        {
            if (reports[i][propertyName] is not JsonValue value
                || !value.TryGetValue(out int candidate)
                || candidate != common)
            {
                common = default;
                return false;
            }
        }

        return true;
    }

    private static bool IsIncomplete(JsonObject report)
        => report["incomplete"] is JsonValue value
            && value.TryGetValue(out bool incomplete)
            && incomplete;
}
