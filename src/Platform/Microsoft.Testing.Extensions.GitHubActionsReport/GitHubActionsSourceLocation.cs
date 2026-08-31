// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// A workspace-relative source location used to pin a GitHub Actions annotation to a file (and, when known,
/// a line) so it renders on the pull request's "Files changed" diff gutter.
/// </summary>
/// <remarks>
/// <see cref="LineNumber"/> is <c>0</c> when the producing test framework reported a file but no usable line
/// (it uses a <c>-1</c> sentinel, or none at all). GitHub accepts a <c>file</c>-only annotation, so callers
/// emit the annotation without a <c>line</c> property in that case rather than fabricating one.
/// </remarks>
internal readonly struct GitHubActionsSourceLocation
{
    public GitHubActionsSourceLocation(string relativeNormalizedPath, int lineNumber)
    {
        RelativeNormalizedPath = relativeNormalizedPath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Gets the workspace-relative, forward-slash separated path of the source file.
    /// </summary>
    public string RelativeNormalizedPath { get; }

    /// <summary>
    /// Gets the 1-based line number, or <c>0</c> when the line is unknown.
    /// </summary>
    public int LineNumber { get; }
}
