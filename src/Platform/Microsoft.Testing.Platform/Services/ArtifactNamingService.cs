// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

[Embedded]
internal static class ArtifactNamingHelper
{
    private static readonly Regex TemplateFieldRegex = new(@"<([^>]+)>", RegexOptions.Compiled);

    /// <summary>
    /// Resolves a template pattern by replacing &lt;placeholder&gt; tokens with values from the provided dictionary.
    /// Unknown placeholders are preserved as-is.
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

        return TemplateFieldRegex.Replace(template, match =>
        {
            string fieldName = match.Groups[1].Value;
            return replacements.TryGetValue(fieldName, out string? value) ? value : match.Value;
        });
    }

    public static string GetOperatingSystemName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
            : "unknown";
}
