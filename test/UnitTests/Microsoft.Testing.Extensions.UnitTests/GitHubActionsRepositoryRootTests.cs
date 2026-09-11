// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsRepositoryRootTests
{
    [TestMethod]
    public void Resolve_WhenGitHubWorkspaceIsSet_ReturnsItWithTrailingSeparator()
    {
        Mock<IEnvironment> environment = new();
        _ = environment
            .Setup(e => e.GetEnvironmentVariable("GITHUB_WORKSPACE"))
            .Returns($"{Path.DirectorySeparatorChar}home{Path.DirectorySeparatorChar}runner{Path.DirectorySeparatorChar}work{Path.DirectorySeparatorChar}repo");

        string? root = GitHubActionsRepositoryRoot.Resolve(environment.Object);

        Assert.IsNotNull(root);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), root, StringComparison.Ordinal);
        Assert.StartsWith($"{Path.DirectorySeparatorChar}home{Path.DirectorySeparatorChar}runner{Path.DirectorySeparatorChar}work{Path.DirectorySeparatorChar}repo", root, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_WhenGitHubWorkspaceAlreadyHasTrailingSeparator_DoesNotDuplicateIt()
    {
        string workspace = $"{Path.DirectorySeparatorChar}some{Path.DirectorySeparatorChar}workspace{Path.DirectorySeparatorChar}";
        Mock<IEnvironment> environment = new();
        _ = environment
            .Setup(e => e.GetEnvironmentVariable("GITHUB_WORKSPACE"))
            .Returns(workspace);

        string? root = GitHubActionsRepositoryRoot.Resolve(environment.Object);

        Assert.AreEqual(workspace, root);
    }

    [TestMethod]
    public void Resolve_WhenGitHubWorkspaceHasAltDirectorySeparator_DoesNotAddExtraSeparator()
    {
        // Path.AltDirectorySeparatorChar is '/' everywhere (including Windows), which is a valid trailing
        // separator on its own and must not be doubled up with the platform's primary separator.
        string workspace = $"{Path.AltDirectorySeparatorChar}some{Path.AltDirectorySeparatorChar}workspace{Path.AltDirectorySeparatorChar}";
        Mock<IEnvironment> environment = new();
        _ = environment
            .Setup(e => e.GetEnvironmentVariable("GITHUB_WORKSPACE"))
            .Returns(workspace);

        string? root = GitHubActionsRepositoryRoot.Resolve(environment.Object);

        Assert.AreEqual(workspace, root);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Resolve_WhenGitHubWorkspaceIsNotUsable_FallsBackToGitRoot(string? workspaceValue)
    {
        Mock<IEnvironment> environment = new();
        _ = environment
            .Setup(e => e.GetEnvironmentVariable("GITHUB_WORKSPACE"))
            .Returns(workspaceValue);

        // Running inside this repo's own test host, FindGitRoot() succeeds and returns a real path rather
        // than null, so the fallback path taken by Resolve can be asserted end-to-end.
        string? expected = GitHubActionsRepositoryRoot.FindGitRoot();
        string? root = GitHubActionsRepositoryRoot.Resolve(environment.Object);

        Assert.AreEqual(expected, root);
    }

    [TestMethod]
    public void FindGitRoot_ReturnsPathEndingWithDirectorySeparator()
    {
        string? root = GitHubActionsRepositoryRoot.FindGitRoot();

        Assert.IsNotNull(root);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), root, StringComparison.Ordinal);
    }
}
