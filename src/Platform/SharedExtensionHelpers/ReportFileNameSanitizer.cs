// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

// This type is SHIPPED InternalAPI across several report extensions (linked source), so it cannot be
// removed or renamed without breaking those extensions. It delegates to the canonical implementation in
// Microsoft.Testing.Platform (ArtifactFileNameSanitizer), which the report extensions can access through
// the InternalsVisibleTo grant, so the sanitization rules live in a single place and cannot drift.
internal static class ReportFileNameSanitizer
{
    internal static string ReplaceInvalidFileNameChars(string fileName)
        => ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars(fileName);
}
