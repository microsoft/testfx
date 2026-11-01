// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class FileSystemTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CopyFile_PreservesBinaryContentAndOverwritesExistingDestination(bool empty)
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(FileSystemTests), Guid.NewGuid().ToString("N"));
        FileSystem fileSystem = new();
        fileSystem.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string destination = Path.Combine(directory, "destination.bin");
        byte[] expected = empty ? [] : [0, 255, 13, 10, 128, 1, 0];
        try
        {
            File.WriteAllBytes(source, expected);
            File.WriteAllText(destination, "Existing data must be truncated, not appended.");

            fileSystem.CopyFile(source, destination);

            Assert.AreSequenceEqual(expected, File.ReadAllBytes(destination));
            Assert.AreSequenceEqual(expected, File.ReadAllBytes(source));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CopyFile_MissingSource_PreservesDestinationAndPropagatesFailure()
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(FileSystemTests), Guid.NewGuid().ToString("N"));
        FileSystem fileSystem = new();
        fileSystem.CreateDirectory(directory);
        string source = Path.Combine(directory, "missing.bin");
        string destination = Path.Combine(directory, "destination.bin");
        try
        {
            File.WriteAllText(destination, "keep existing contents");

            FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() => fileSystem.CopyFile(source, destination));

            Assert.AreEqual(source, exception.FileName);
            Assert.AreEqual("keep existing contents", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
