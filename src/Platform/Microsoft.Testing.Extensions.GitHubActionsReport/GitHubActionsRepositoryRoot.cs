// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Resolves the repository root used to turn an absolute source-file path from a stack trace into a
/// workspace-relative path for a GitHub Actions annotation. On a runner this is the
/// <c>GITHUB_WORKSPACE</c> directory; off a runner (e.g. local runs and unit tests) it walks up from the
/// application base directory looking for a <c>.git</c> directory or file.
/// </summary>
internal static class GitHubActionsRepositoryRoot
{
    // Process-lifetime cache of the discovered git root. Written without synchronization on purpose: the value
    // is idempotent (always derived from the constant AppContext.BaseDirectory), so a benign race can at most
    // recompute the same path. It is intentionally never reset for the lifetime of the process.
    private static string? s_cachedGitRoot;

    public static string? Resolve(IEnvironment environment)
    {
        string? workspace = environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        return !RoslynString.IsNullOrWhiteSpace(workspace)
            ? EnsureTrailingSeparator(workspace!)
            : FindGitRoot();
    }

    internal static /* for testing */ string? FindGitRoot()
    {
        // This intentionally mirrors the test-infrastructure RootFinder.Find() walk (from AppContext.BaseDirectory
        // up to the drive root, looking for a '.git' directory or worktree file) but returns null instead of
        // throwing when nothing is found, so a reporter running outside a git checkout degrades to "no source
        // location" rather than failing. RootFinder also lives in test utilities, so it is deliberately not reused.
        if (s_cachedGitRoot is not null)
        {
            return s_cachedGitRoot;
        }

        string currentDirectory = AppContext.BaseDirectory;
        string rootDriveDirectory = Directory.GetDirectoryRoot(currentDirectory);
        while (!string.Equals(rootDriveDirectory, currentDirectory, StringComparison.Ordinal))
        {
            string gitPath = Path.Combine(currentDirectory, ".git");

            // When working with git worktrees, the .git is a file not a folder.
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                s_cachedGitRoot = currentDirectory + Path.DirectorySeparatorChar;
                return s_cachedGitRoot;
            }

            currentDirectory = Directory.GetParent(currentDirectory)!.ToString();
        }

        return null;
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}
