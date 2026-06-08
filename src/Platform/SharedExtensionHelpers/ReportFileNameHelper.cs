// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared helper for resolving artifact naming template placeholders in a report file name
/// and sanitizing the resulting leaf file name.
/// </summary>
internal static class ReportFileNameHelper
{
    /// <summary>
    /// Resolves template placeholders in <paramref name="template"/> using the standard artifact naming
    /// replacements, then sanitizes the leaf file name portion of the result.
    /// The directory portion (if any) is returned verbatim so that absolute or relative paths
    /// containing path separators are preserved; invalid characters in the directory portion
    /// are deferred to the OS and will surface as an <see cref="IOException"/> at file-creation time.
    /// </summary>
    /// <param name="template">The file name template that may contain <c>{pname}</c>, <c>{pid}</c>, <c>{time}</c>, etc. placeholders.</param>
    /// <param name="processName">The process name (resolves <c>{pname}</c>).</param>
    /// <param name="processId">The process ID (resolves <c>{pid}</c>).</param>
    /// <param name="now">The timestamp to use (resolves <c>{time}</c>).</param>
    /// <returns>
    /// The resolved and sanitized file name (or path, if the template contained a directory component).
    /// </returns>
    public static string ResolveAndSanitize(string template, string processName, string processId, DateTimeOffset now)
    {
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(processName, processId, now);
        string resolved = ArtifactNamingHelper.ResolveTemplate(template, replacements);
        string directoryPart = Path.GetDirectoryName(resolved) ?? string.Empty;
        string sanitizedFileName = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(Path.GetFileName(resolved));
        return directoryPart.Length == 0
            ? sanitizedFileName
            : Path.Combine(directoryPart, sanitizedFileName);
    }
}
