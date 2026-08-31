// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using Microsoft.Testing.Platform.Helpers;

using Moq;

using StackTraceSourceLocationResolver = ghactions::Microsoft.Testing.Extensions.StackTraceSourceLocationResolver;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
[ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
public sealed class StackTraceSourceLocationResolverTests
{
    [TestMethod]
    public void TryMakeWorkspaceRelative_DeterministicRootTraversal_ReturnsNull()
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative("/_/../outside.cs", CreateRepoRoot(), fileSystem.Object);

        Assert.IsNull(relativePath);
        fileSystem.Verify(f => f.ExistFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_RepoRootPrefixedTraversal_ReturnsNull()
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();
        string repoRoot = CreateRepoRoot();
        string outsidePath = repoRoot + ".." + Path.DirectorySeparatorChar + "outside.cs";

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(outsidePath, repoRoot, fileSystem.Object);

        Assert.IsNull(relativePath);
        fileSystem.Verify(f => f.ExistFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_WorkspaceFile_ReturnsForwardSlashRelativePath()
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();
        string repoRoot = CreateRepoRoot();
        string filePath = Path.Combine(repoRoot, "src", "Calc.cs");

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(filePath, repoRoot, fileSystem.Object);

        Assert.AreEqual("src/Calc.cs", relativePath);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_OutsideWorkspace_ReturnsNull()
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();
        string filePath = Path.Combine(Environment.CurrentDirectory, "elsewhere", "outside.cs");

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(filePath, CreateRepoRoot(), fileSystem.Object);

        Assert.IsNull(relativePath);
        fileSystem.Verify(f => f.ExistFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_WorkspaceRelativeFile_ReturnsForwardSlashRelativePath()
    {
        // The VSTest bridge copies TestCase.CodeFilePath verbatim, so a bridged framework may report a path
        // that is already workspace-relative rather than absolute.
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(Path.Combine("src", "Calc.cs"), CreateRepoRoot(), fileSystem.Object);

        Assert.AreEqual("src/Calc.cs", relativePath);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_WorkspaceRelativeTraversal_ReturnsNull()
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(".." + Path.DirectorySeparatorChar + "outside.cs", CreateRepoRoot(), fileSystem.Object);

        Assert.IsNull(relativePath);
        fileSystem.Verify(f => f.ExistFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void TryMakeWorkspaceRelative_CaseDistinctSiblingDirectory_FollowsPlatformPathSemantics()
    {
        // Path containment must match the filesystem. On Windows a case-distinct spelling of the workspace
        // root names the same directory, so the file is genuinely inside the workspace and still resolves.
        // Elsewhere it is a different directory and must be rejected rather than reported as workspace-relative.
        Mock<IFileSystem> fileSystem = CreateFileSystemWhereEveryFileExists();
        string repoRoot = CreateRepoRoot();
        string caseDistinctSibling = Path.Combine(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "REPO")), "Calc.cs");

        string? relativePath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(caseDistinctSibling, repoRoot, fileSystem.Object);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.AreEqual("Calc.cs", relativePath);
        }
        else
        {
            Assert.IsNull(relativePath);
        }
    }

    private static Mock<IFileSystem> CreateFileSystemWhereEveryFileExists()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns(true);
        return fileSystem;
    }

    private static string CreateRepoRoot()
        => EnsureTrailingDirectorySeparator(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "repo")));

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        char lastChar = path[path.Length - 1];
        return lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
