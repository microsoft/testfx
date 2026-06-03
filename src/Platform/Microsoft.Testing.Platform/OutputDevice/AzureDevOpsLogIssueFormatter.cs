// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Helpers for translating output-device messages into Azure Pipelines logging commands
/// (<see href="https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands">##vso[...]</see>)
/// so that errors and warnings surface on the Azure DevOps build / pipeline summary instead of
/// being just colored text in the agent log.
/// See https://github.com/microsoft/testfx/issues/5979.
/// </summary>
internal static class AzureDevOpsLogIssueFormatter
{
    internal const string SeverityError = "error";
    internal const string SeverityWarning = "warning";

    // Opt-out: setting TESTINGPLATFORM_AZDO_OUTPUT to one of these disables automatic
    // ##vso[task.logissue] emission even when TF_BUILD=true.
    private const string OptOutEnvironmentVariableName = "TESTINGPLATFORM_AZDO_OUTPUT";

    /// <summary>
    /// Returns <c>true</c> when the current process is running on an Azure DevOps agent
    /// (TF_BUILD=true) and the user has not opted out via <c>TESTINGPLATFORM_AZDO_OUTPUT=off|false|0</c>.
    /// </summary>
    public static bool IsAzureDevOpsEnvironment(IEnvironment environment)
    {
        if (!bool.TryParse(environment.GetEnvironmentVariable("TF_BUILD"), out bool tfBuild) || !tfBuild)
        {
            return false;
        }

        string? optOut = environment.GetEnvironmentVariable(OptOutEnvironmentVariableName);
        return RoslynString.IsNullOrEmpty(optOut) || !IsOffValue(optOut);
    }

    /// <summary>
    /// Formats a message as a <c>##vso[task.logissue type=&lt;severity&gt;]&lt;message&gt;</c> line
    /// with the standard Azure Pipelines escaping rules applied to the message body.
    /// </summary>
    public static string FormatLogIssue(string severity, string message)
        => $"##vso[task.logissue type={severity}]{Escape(message)}";

    /// <summary>
    /// Escapes a value for inclusion in the message body of an Azure Pipelines logging command
    /// per the official escaping rules
    /// (<see href="https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands#formatting-commands">formatting commands</see>):
    /// <c>%</c> -> <c>%25</c>, <c>;</c> -> <c>%3B</c>, <c>\r</c> -> <c>%0D</c>, <c>\n</c> -> <c>%0A</c>, <c>]</c> -> <c>%5D</c>.
    /// <c>%</c> must be escaped first so the other replacements don't get double-encoded.
    /// </summary>
    internal static string Escape(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        StringBuilder? builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            string? replacement = c switch
            {
                '%' => "%25",
                ';' => "%3B",
                '\r' => "%0D",
                '\n' => "%0A",
                ']' => "%5D",
                _ => null,
            };

            if (replacement is null)
            {
                builder?.Append(c);
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
            builder.Append(replacement);
        }

        return builder?.ToString() ?? value;
    }

    private static bool IsOffValue(string value)
        => value.Equals("off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.Ordinal);
}
