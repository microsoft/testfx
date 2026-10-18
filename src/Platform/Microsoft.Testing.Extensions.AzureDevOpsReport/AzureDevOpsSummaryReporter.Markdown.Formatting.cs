// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0:D2}:{1:D2}", "{0}:{1:D2}:{2:D2}");

    private static void AppendStatusStrip(
        StringBuilder builder,
        long total,
        long passed,
        long failed,
        long skipped,
        long flaky,
        TimeSpan? duration,
        int? exitCode)
    {
        builder.Append("**").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(total == 1 ? " test**" : " tests**")
            .Append(" · ✅ **").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" passed**")
            .Append(" · ❌ **").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" failed**")
            .Append(" · ⏭ **").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped**");
        if (flaky > 0)
        {
            builder.Append(" · ⚠ **").Append(flaky.ToString(CultureInfo.InvariantCulture)).Append(" flaky**");
        }

        builder.Append(" · ⏱ **").Append(duration is { } value ? FormatDuration(value) : "Unavailable").Append("**");
        if (exitCode is int processExitCode && processExitCode != 0)
        {
            builder.Append(" · exit code **").Append(processExitCode.ToString(CultureInfo.InvariantCulture)).Append("** (")
                .Append(CiRunSummaryExitCode.GetName(processExitCode)).Append(" — ")
                .Append(CiRunSummaryExitCode.GetReason(processExitCode, AzureDevOpsResources.ResourceManager)).Append(')');
        }

        builder.Append("\n\n");
    }

    private static string GetQualifiedTestLabel(
        IReadOnlyList<CiRunSummaryModule> modules,
        CiRunSummaryModule module,
        string testName)
    {
        if (modules.Count == 1)
        {
            return testName;
        }

        string discriminator = $"{module.AssemblyName} ({module.TargetFramework}, {module.Architecture}";
        if (HasDuplicateModuleIdentity(modules, module))
        {
            discriminator += $", attempt {module.AttemptNumber.ToString(CultureInfo.InvariantCulture)}, session {module.SessionUid}";
        }

        return $"{discriminator}): {testName}";
    }

    private static void AppendBlockQuote(StringBuilder builder, string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            builder.Append("> ").Append(EscapeCell(line)).Append('\n');
        }

        builder.Append('\n');
    }

    private static string FormatTestName(string value)
    {
        string flattenedValue = value
            .Replace("\r", " ")
            .Replace("\n", " ");
        if (flattenedValue.Length == 0)
        {
            return string.Empty;
        }

        int maxBacktickRun = 0;
        int currentBacktickRun = 0;
        foreach (char character in flattenedValue)
        {
            if (character == '`')
            {
                currentBacktickRun++;
                maxBacktickRun = Math.Max(maxBacktickRun, currentBacktickRun);
            }
            else
            {
                currentBacktickRun = 0;
            }
        }

        string delimiter = new('`', maxBacktickRun + 1);
        bool requiresPadding = flattenedValue.StartsWith("`", StringComparison.Ordinal)
            || flattenedValue.EndsWith("`", StringComparison.Ordinal)
            || flattenedValue.StartsWith(" ", StringComparison.Ordinal)
            || flattenedValue.EndsWith(" ", StringComparison.Ordinal);
        return requiresPadding
            ? $"{delimiter} {flattenedValue} {delimiter}"
            : $"{delimiter}{flattenedValue}{delimiter}";
    }

    private static void AppendModuleIdentity(
        StringBuilder builder,
        IReadOnlyList<CiRunSummaryModule> modules,
        CiRunSummaryModule module)
    {
        builder.Append(EscapeCell(module.AssemblyName))
            .Append(" (").Append(EscapeCell(module.TargetFramework)).Append(", ")
            .Append(EscapeCell(module.Architecture)).Append(')');
        if (HasDuplicateModuleIdentity(modules, module))
        {
            builder.Append(" — attempt ").Append(module.AttemptNumber.ToString(CultureInfo.InvariantCulture))
                .Append(", session ").Append(EscapeCell(module.SessionUid));
        }
    }

    private static string EscapeCell(string value)
        => RoslynString.IsNullOrEmpty(value)
            ? value
            : HtmlEncode(value)
                .Replace("|", "\\|")
                .Replace("`", "\\`")
                .Replace("\r", string.Empty)
                .Replace("\n", "<br>");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.AssemblyName, module.AssemblyName, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}
