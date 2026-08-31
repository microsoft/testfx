// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The helper is a no-op on .NET (the runtime handles long paths itself), so there is nothing to assert
// there and the whole fixture only exists for the .NET Framework targets.
#if !NETCOREAPP

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Guards the long-path handling that TRX attachment copies depend on.
/// </summary>
/// <remarks>
/// <see cref="Directory.SetCurrentDirectory(string)"/> is process-global, so both tests declare
/// <see cref="WellKnownResources.CurrentDirectory"/> to serialize against readers and writers that
/// declare the same resource. Each test restores the original value in a
/// <see langword="finally"/> block, so a class-wide <see cref="ResourceLockAttribute"/> (reacquired per
/// test under method-level parallelization) is sufficient; no state survives across tests.
/// </remarks>
[TestClass]
[ResourceLock(WellKnownResources.CurrentDirectory)]
public class TrxLongPathHelperTests
{
    /// <summary>
    /// Win32 <c>MAX_PATH</c>. Duplicated from the helper because the helper's copy is private.
    /// </summary>
    private const int MaxShortPathLength = 260;

    [TestMethod]
    public void GetPathForFileSystemAccess_WhenRelativePathResolvesBeyondMaxPath_ReturnsExtendedLengthPath()
    {
        // This pins the ORDER of operations inside GetPathForFileSystemAccess: Path.GetFullPath must run
        // BEFORE the MAX_PATH comparison. A relative path is short as written and only crosses the limit
        // once resolved, so measuring the unresolved string silently skips the prefix and the caller
        // loses the attachment with nothing but a warning. An earlier version of the helper did exactly
        // that. Do not "simplify" it by testing path.Length first.
        //
        // The deep directory is built here rather than relying on how deep the repository happens to be
        // checked out: the acceptance test for this feature passes an already-absolute results directory,
        // so it cannot distinguish the two orderings, and the TRX unit tests only distinguish them when
        // the repository path is long. Without this test a refactor could drop the resolution step and
        // every other test would stay green on a short-path agent.
        string originalCurrentDirectory = Directory.GetCurrentDirectory();
        string root = Path.Combine(Path.GetTempPath(), $"trxlp{Guid.NewGuid():N}");

        try
        {
            // Windows refuses to set a current directory at or beyond MAX_PATH even with the
            // extended-length prefix, so the working directory has to stay just under the limit and the
            // relative segment supplies the rest.
            const int TargetCurrentDirectoryLength = 230;
            int padding = Math.Max(1, TargetCurrentDirectoryLength - root.Length - 1);
            string deepDirectory = Path.Combine(root, new string('d', padding));

            Directory.CreateDirectory(deepDirectory);
            Directory.SetCurrentDirectory(deepDirectory);

            string relativePath = new('r', 40);
            string expectedResolvedPath = Path.GetFullPath(relativePath);

            // Preconditions, asserted so the test fails loudly rather than silently proving nothing if
            // the temp path on some machine makes the arithmetic degenerate.
            Assert.IsLessThan(MaxShortPathLength, relativePath.Length, "The unresolved path must be short enough that a length check on it would skip the prefix.");
            Assert.IsGreaterThanOrEqualTo(MaxShortPathLength, expectedResolvedPath.Length, "The resolved path must reach MAX_PATH for the prefix to be required.");

            string result = TrxLongPathHelper.GetPathForFileSystemAccess(relativePath);

            Assert.AreEqual(@"\\?\" + expectedResolvedPath, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void GetPathForFileSystemAccess_WhenPathIsShort_ReturnsPathUnchanged()
    {
        // Deliberately a *relative* path. For a short path the helper must hand back exactly what it
        // was given rather than the resolved form, and an absolute path cannot distinguish those two
        // because it is equal to its own Path.GetFullPath result - so a helper that wrongly returned
        // the resolved path for short inputs would still pass.
        const string RelativePath = "short.txt";

        string originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            // Anchor the working directory somewhere short so the resolved form stays under MAX_PATH
            // however deeply the repository happens to be cloned.
            Directory.SetCurrentDirectory(Path.GetTempPath());
            Assert.IsLessThan(MaxShortPathLength, Path.GetFullPath(RelativePath).Length);

            Assert.AreEqual(RelativePath, TrxLongPathHelper.GetPathForFileSystemAccess(RelativePath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }
}

#endif
