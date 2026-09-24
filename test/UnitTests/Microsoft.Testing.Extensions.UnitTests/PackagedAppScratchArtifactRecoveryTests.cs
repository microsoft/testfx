// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppScratchArtifactRecoveryTests
{
    [TestMethod]
    public void Recover_ReparsePointFilesAndDirectories_AreSkipped()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(PackagedAppScratchArtifactRecoveryTests),
            Guid.NewGuid().ToString("N"));
        string scratchDirectory = Path.Combine(testDirectory, "scratch");
        string recoveryDirectory = Path.Combine(testDirectory, "recovery");
        string outsideDirectory = Path.Combine(testDirectory, "outside");
        Directory.CreateDirectory(scratchDirectory);
        Directory.CreateDirectory(outsideDirectory);

        try
        {
            string regularFile = Path.Combine(scratchDirectory, "nested", "regular.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(regularFile)!);
            File.WriteAllText(regularFile, "regular");

            string outsideFile = Path.Combine(outsideDirectory, "outside.txt");
            File.WriteAllText(outsideFile, "outside");

            try
            {
                Directory.CreateSymbolicLink(Path.Combine(scratchDirectory, "directory-link"), outsideDirectory);
                File.CreateSymbolicLink(Path.Combine(scratchDirectory, "file-link.txt"), outsideFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive($"Symbolic link creation is not supported or permitted in this environment: {ex.Message}");
                return;
            }

            PackagedAppScratchArtifactRecovery.Recover(scratchDirectory, recoveryDirectory);

            Assert.AreEqual("regular", File.ReadAllText(Path.Combine(recoveryDirectory, "nested", "regular.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(recoveryDirectory, "directory-link", "outside.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(recoveryDirectory, "file-link.txt")));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}

#endif
