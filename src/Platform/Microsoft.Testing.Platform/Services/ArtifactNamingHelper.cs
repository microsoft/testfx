// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Services;

[Embedded]
internal static class ArtifactNamingHelper
{
    private static readonly Regex TemplateFieldRegex = new(@"<([^>]+)>", RegexOptions.Compiled);

    /// <summary>
    /// Resolves a template pattern by replacing &lt;placeholder&gt; tokens with values from the provided dictionary.
    /// Unknown placeholders are preserved as-is. Placeholder matching is always case-sensitive (ordinal).
    /// </summary>
    public static string ResolveTemplate(string template, IDictionary<string, string>? replacements = null)
    {
        if (RoslynString.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("Template cannot be null, empty, or whitespace.", nameof(template));
        }

        if (replacements is null || replacements.Count == 0)
        {
            return template;
        }

        // Ensure ordinal (case-sensitive) comparison regardless of the caller-provided dictionary comparer.
        Dictionary<string, string> ordinalReplacements = replacements is Dictionary<string, string> dict && dict.Comparer == StringComparer.Ordinal
            ? dict
            : new Dictionary<string, string>(replacements, StringComparer.Ordinal);

        return TemplateFieldRegex.Replace(template, match =>
        {
            string fieldName = match.Groups[1].Value;
            return ordinalReplacements.TryGetValue(fieldName, out string? value) && value is not null ? value : match.Value;
        });
    }

    public static string GetOperatingSystemName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
            : "unknown";
}
