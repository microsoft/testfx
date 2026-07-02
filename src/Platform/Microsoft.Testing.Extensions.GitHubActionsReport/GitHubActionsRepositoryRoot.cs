// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Resolves the repository root used to turn an absolute source-file path from a stack trace into a
/// workspace-relative path for a GitHub Actions annotation. On a runner this is the
/// <c>GITHUB_WORKSPACE</c> directory; off a runner (e.g. local runs and unit tests) it walks up from the
/// application base directory looking for a <c>.git</c> directory or file.
/// </summary>
internal static class GitHubActionsRepositoryRoot
{
    public static string? Resolve(IEnvironment environment)
    {
        string? workspace = environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        return !RoslynString.IsNullOrWhiteSpace(workspace)
            ? EnsureTrailingSeparator(workspace!)
            : FindGitRoot();
    }

    internal static /* for testing */ string? FindGitRoot()
    {
        // Reuse the shared RootFinder walk (from AppContext.BaseDirectory up to the drive root, looking for a
        // '.git' directory or worktree file, with a process-lifetime cache) rather than duplicating it here.
        // RootFinder.Find() throws when no repository is found; a reporter running outside a git checkout must
        // instead degrade to "no source location", so translate that into null.
        try
        {
            return RootFinder.Find();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}
