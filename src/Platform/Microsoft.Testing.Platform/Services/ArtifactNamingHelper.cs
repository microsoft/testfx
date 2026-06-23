// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.Services;

[Embedded]
internal static partial class ArtifactNamingHelper
{
#if NET
    private static readonly Regex TemplateFieldRegex = GetTemplateFieldRegex();

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex GetTemplateFieldRegex();
#else
    private static readonly Regex TemplateFieldRegex = new(@"\{([^}]+)\}", RegexOptions.Compiled);
#endif

    /// <summary>
    /// Builds the standard set of placeholder replacements for artifact naming.
    /// Consumers pass process-specific values; the helper resolves the rest (asm, tfm, arch).
    /// </summary>
    /// <param name="processName">The name of the process (resolves <c>{pname}</c>).</param>
    /// <param name="processId">The process ID (resolves <c>{pid}</c>).</param>
    /// <param name="timestamp">The timestamp to use (resolves <c>{time}</c>).</param>
    public static Dictionary<string, string> GetStandardReplacements(string processName, string processId, DateTimeOffset timestamp)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pname"] = processName,
            ["pid"] = processId,
            ["time"] = timestamp.ToString("yyyy-MM-dd_HH-mm-ss.fffffff", CultureInfo.InvariantCulture),
            ["arch"] = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };

        string? asmName = Assembly.GetEntryAssembly()?.GetName().Name;
        replacements["asm"] = asmName ?? "unknown";

        string? tfm = TargetFrameworkParser.GetShortTargetFrameworkIncludingPlatform(Assembly.GetEntryAssembly());
        replacements["tfm"] = tfm ?? "unknown";

        return replacements;
    }

    /// <summary>
    /// Resolves a template pattern by replacing {placeholder} tokens with values from the provided dictionary.
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
            return ordinalReplacements.TryGetValue(fieldName, out string? value) ? value : match.Value;
        });
    }
}
