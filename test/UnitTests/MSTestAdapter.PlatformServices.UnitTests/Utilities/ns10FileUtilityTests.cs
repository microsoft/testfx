// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

public class FileUtilityTests : TestContainer
{
    private readonly Mock<FileUtility> _fileUtility;

    public FileUtilityTests()
        => _fileUtility = new Mock<FileUtility>
        {
            CallBase = true,
        };

    public void ReplaceInvalidFileNameCharactersShouldReturnFileNameIfItHasNoInvalidChars()
    {
        string fileName = "galaxy";
        Verify(fileName == FileUtility.ReplaceInvalidFileNameCharacters(fileName));
    }

    public void ReplaceInvalidFileNameCharactersShouldReplaceInvalidChars()
    {
        string fileName = "galaxy<>far:far?away";
        Verify(FileUtility.ReplaceInvalidFileNameCharacters(fileName) == "galaxy__far_far_away");
    }

    #region AddFilesFromDirectory tests

    public void AddFilesInADirectoryShouldReturnAllTopLevelFilesInADirectory()
    {
        string[] topLevelFiles = ["tick.txt", "tock.tick.txt"];

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns(topLevelFiles);
        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns([]);

        List<string> files = _fileUtility.Object.AddFilesFromDirectory("C:\\randomclock", false);

        Verify(topLevelFiles.SequenceEqual(files));
    }

    public void AddFilesInADirectoryShouldReturnAllFilesUnderSubFolders()
    {
        string[] allFiles =
        [
            "MainClock\\tickmain.txt", "MainClock\\tock.tick.txt",
            "MainClock\\Folder1\\tick.txt", "MainClock\\Folder1\\tock.tick.txt",
            "MainClock\\Folder2\\newtick.log", "MainClock\\Folder2\\newtock.log",
            "MainClock\\Folder2\\backup\\newtock.tick.txt"
        ];

        SetupMockFileAPIs(allFiles);

        List<string> files = _fileUtility.Object.AddFilesFromDirectory("MainClock", false);

        Verify(allFiles.SequenceEqual(files));
    }

    public void AddFilesInADirectoryShouldReturnAllFilesUnderSubFoldersEvenIfAFolderIsEmpty()
    {
        string[] allFiles =
        [
            "MainClock\\tickmain.txt", "MainClock\\tock.tick.txt",
            "MainClock\\Folder1\\tick.txt", "MainClock\\Folder1\\tock.tick.txt",
            "MainClock\\Folder2\\newtick.log", "MainClock\\Folder2\\newtock.log",
            "MainClock\\Folder2\\backup\\"
        ];

        SetupMockFileAPIs(allFiles);

        List<string> files = _fileUtility.Object.AddFilesFromDirectory("MainClock", false);

        string[] expectedFiles = new string[allFiles.Length - 1];
        Array.Copy(allFiles, 0, expectedFiles, 0, 6);

        Verify(expectedFiles.SequenceEqual(files));
    }

    public void AddFilesWithIgnoreDirectory()
    {
        // Setup
        string[] allFiles =
        [
            "c:\\MainClock\\Results\\tickmain.trx", "c:\\MainClock\\Results\\Run1\\tock.tick.txt",
            "c:\\MainClock\\tickmain.txt", "c:\\MainClock\\tock.tick.txt",
            "c:\\MainClock\\Folder1\\tick.txt", "c:\\MainClock\\Folder1\\tock.tick.txt",
            "c:\\MainClock\\Folder2\\backup\\Data.csv"
        ];

        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns<string>((directory) =>
        {
            IEnumerable<string> directories = allFiles.Where(file => IsFileUnderDirectory(directory, file)).Select((file) => Path.GetDirectoryName(file)).Distinct();
            return directories.ToArray();
        });

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns<string>((directory) => allFiles.Where((file) => Path.GetDirectoryName(file).Equals(directory, StringComparison.OrdinalIgnoreCase)).Distinct().ToArray());

        // Act
        List<string> files = _fileUtility.Object.AddFilesFromDirectory("C:\\MainClock", (directory) => directory.Contains("Results"), false);

        // Validate
        foreach (string sourceFile in allFiles)
        {
            Console.WriteLine($"File to validate {sourceFile}");
            if (sourceFile.Contains("Results"))
            {
                Verify(!files.Any((file) => file.Contains("Results")), $"{sourceFile} returned in the list from AddFilesFromDirectory");
            }
            else
            {
                Verify(files.Any((file) => file.Equals(sourceFile, StringComparison.OrdinalIgnoreCase)), $"{sourceFile} not returned in the list from AddFilesFromDirectory");
            }
        }
    }

    public void AddFilesWithNoIgnoreDirectory()
    {
        // Setup
        string[] allFiles =
        [
            "c:\\MainClock\\Results\\tickmain.trx", "c:\\MainClock\\Results\\Run1\\tock.tick.txt",
            "c:\\MainClock\\tickmain.txt", "c:\\MainClock\\tock.tick.txt",
            "c:\\MainClock\\Folder1\\tick.txt", "c:\\MainClock\\Folder1\\tock.tick.txt",
            "c:\\MainClock\\Folder2\\backup\\Data.csv"
        ];

        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns<string>((directory) =>
        {
            IEnumerable<string> directories = allFiles.Where(file => IsFileUnderDirectory(directory, file)).Select((file) => Path.GetDirectoryName(file)).Distinct();
            return directories.ToArray();
        });

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns<string>((directory) => allFiles.Where((file) => Path.GetDirectoryName(file).Equals(directory, StringComparison.OrdinalIgnoreCase)).Distinct().ToArray());

        // Act
        List<string> files = _fileUtility.Object.AddFilesFromDirectory("C:\\MainClock", false);

        // Validate
        foreach (string sourceFile in allFiles)
        {
            Verify(files.Any((file) => file.Equals(sourceFile, StringComparison.OrdinalIgnoreCase)), $"{sourceFile} not returned in the list from AddFilesFromDirectory");
        }
    }

    private static bool IsFileUnderDirectory(string directory, string fileName)
    {
        string fileDirectory = Path.GetDirectoryName(fileName);
        return fileDirectory.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
               !directory.Equals(fileDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private void SetupMockFileAPIs(string[] files)
    {
        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns((string dp) =>
#pragma warning disable CA1865 // Use char overload
            files.Where(f => f.Contains(dp) && f.LastIndexOf('\\') == (f.IndexOf(dp, StringComparison.Ordinal) + dp.Length) && !f.EndsWith("\\", StringComparison.Ordinal))
                    .ToArray());
        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns((string dp) => files.Where(f => f.Contains(dp) && f.LastIndexOf('\\') > (f.IndexOf(dp, StringComparison.Ordinal) + dp.Length))
                    .Select(f =>
                    {
#pragma warning disable IDE0057 // Use range operator
                        string val = f.Substring(
                            f.IndexOf(dp, StringComparison.Ordinal) + dp.Length + 1,
                            f.Length - (f.IndexOf(dp, StringComparison.Ordinal) + dp.Length + 1));
                        return f.Substring(0, dp.Length + 1 + val.IndexOf('\\'));
#pragma warning restore IDE0057 // Use range operator
                    })
                    .Distinct()
                    .ToArray());
    }

    #endregion
}
