// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Service for generating consistent artifact names and paths using template patterns.
/// Supports placeholders like &lt;process-name&gt;, &lt;pid&gt;, &lt;id&gt;, &lt;os&gt;, &lt;assembly&gt;, &lt;tfm&gt;, &lt;time&gt;, &lt;root&gt;.
/// </summary>
internal interface IArtifactNamingService
{
    /// <summary>
    /// Resolves a template pattern with available field replacements.
    /// </summary>
    /// <param name="template">Template pattern with placeholders like '&lt;process-name&gt;_&lt;pid&gt;_hang.dmp'.</param>
    /// <param name="customReplacements">Optional custom field replacements to override default values.</param>
    /// <returns>Resolved string with placeholders replaced by actual values.</returns>
    string ResolveTemplate(string template, IDictionary<string, string>? customReplacements = null);

    /// <summary>
    /// Resolves a template pattern with backward compatibility for legacy patterns.
    /// </summary>
    /// <param name="template">Template pattern that may contain legacy patterns like '%p'.</param>
    /// <param name="customReplacements">Optional custom field replacements to override default values.</param>
    /// <param name="legacyReplacements">Legacy pattern replacements for backward compatibility.</param>
    /// <returns>Resolved string with both new and legacy placeholders replaced.</returns>
    string ResolveTemplateWithLegacySupport(string template, IDictionary<string, string>? customReplacements = null, IDictionary<string, string>? legacyReplacements = null);
}