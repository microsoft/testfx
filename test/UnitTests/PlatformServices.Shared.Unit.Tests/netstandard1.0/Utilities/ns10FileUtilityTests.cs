// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Moq;

[TestClass]
public class FileUtilityTests
{
    private Mock<FileUtility> _fileUtility;

    [TestInitialize]
    public void TestInit()
    {
        _fileUtility = new Mock<FileUtility>();
        _fileUtility.CallBase = true;
    }

    [TestMethod]
    public void ReplaceInvalidFileNameCharactersShouldReturnFileNameIfItHasNoInvalidChars()
    {
        var fileName = "galaxy";
        Assert.AreEqual(fileName, _fileUtility.Object.ReplaceInvalidFileNameCharacters(fileName));
    }

    [TestMethod]
    public void ReplaceInvalidFileNameCharactersShouldReplaceInvalidChars()
    {
        var fileName = "galaxy<>far:far?away";
        Assert.AreEqual("galaxy__far_far_away", _fileUtility.Object.ReplaceInvalidFileNameCharacters(fileName));
    }

    #region AddFilesFromDirectory tests

    [TestMethod]
    public void AddFilesInADirectoryShouldReturnAllTopLevelFilesInADirectory()
    {
        var topLevelFiles = new string[] { "tick.txt", "tock.tick.txt" };

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns(topLevelFiles);
        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns(new string[] { });

        var files = _fileUtility.Object.AddFilesFromDirectory("C:\\randomclock", false);

        CollectionAssert.AreEqual(topLevelFiles, files);
    }

    [TestMethod]
    public void AddFilesInADirectoryShouldReturnAllFilesUnderSubFolders()
    {
        var allFiles = new string[]
                           {
                               "MainClock\\tickmain.txt", "MainClock\\tock.tick.txt",
                               "MainClock\\Folder1\\tick.txt", "MainClock\\Folder1\\tock.tick.txt",
                               "MainClock\\Folder2\\newtick.log", "MainClock\\Folder2\\newtock.log",
                               "MainClock\\Folder2\\backup\\newtock.tick.txt",
                           };

        SetupMockFileAPIs(allFiles);

        var files = _fileUtility.Object.AddFilesFromDirectory("MainClock", false);

        CollectionAssert.AreEqual(allFiles, files);
    }

    [TestMethod]
    public void AddFilesInADirectoryShouldReturnAllFilesUnderSubFoldersEvenIfAFolderIsEmpty()
    {
        var allFiles = new string[]
                           {
                               "MainClock\\tickmain.txt", "MainClock\\tock.tick.txt",
                               "MainClock\\Folder1\\tick.txt", "MainClock\\Folder1\\tock.tick.txt",
                               "MainClock\\Folder2\\newtick.log", "MainClock\\Folder2\\newtock.log",
                               "MainClock\\Folder2\\backup\\",
                           };

        SetupMockFileAPIs(allFiles);

        var files = _fileUtility.Object.AddFilesFromDirectory("MainClock", false);

        var expectedFiles = new string[allFiles.Length - 1];
        Array.Copy(allFiles, 0, expectedFiles, 0, 6);

        CollectionAssert.AreEqual(expectedFiles, files);
    }

    [TestMethod]
    public void AddFilesWithIgnoreDirectory()
    {
        // Setup
        var allFiles = new string[]
                           {
                               "c:\\MainClock\\Results\\tickmain.trx", "c:\\MainClock\\Results\\Run1\\tock.tick.txt",
                               "c:\\MainClock\\tickmain.txt", "c:\\MainClock\\tock.tick.txt",
                               "c:\\MainClock\\Folder1\\tick.txt", "c:\\MainClock\\Folder1\\tock.tick.txt",
                               "c:\\MainClock\\Folder2\\backup\\Data.csv",
                           };

        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns<string>((directory) =>
        {
            var directories = allFiles.Where(file => IsFileUnderDirectory(directory, file)).Select((file) => Path.GetDirectoryName(file)).Distinct();
            return directories.ToArray();
        });

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns<string>((directory) => allFiles.Where((file) => Path.GetDirectoryName(file).Equals(directory, StringComparison.OrdinalIgnoreCase)).Distinct().ToArray());

        // Act
        var files = _fileUtility.Object.AddFilesFromDirectory("C:\\MainClock", (directory) => directory.Contains("Results"), false);

        // Validate
        foreach (var sourceFile in allFiles)
        {
            Console.WriteLine($"File to validate {sourceFile}");
            if (sourceFile.Contains("Results"))
            {
                Assert.IsFalse(files.Any((file) => file.Contains("Results")), $"{sourceFile} returned in the list from AddFilesFromDirectory");
            }
            else
            {
                Assert.IsTrue(files.Any((file) => file.Equals(sourceFile, StringComparison.OrdinalIgnoreCase)), $"{sourceFile} not returned in the list from AddFilesFromDirectory");
            }
        }
    }

    [TestMethod]
    public void AddFilesWithNoIgnoreDirectory()
    {
        // Setup
        var allFiles = new string[]
                           {
                               "c:\\MainClock\\Results\\tickmain.trx", "c:\\MainClock\\Results\\Run1\\tock.tick.txt",
                               "c:\\MainClock\\tickmain.txt", "c:\\MainClock\\tock.tick.txt",
                               "c:\\MainClock\\Folder1\\tick.txt", "c:\\MainClock\\Folder1\\tock.tick.txt",
                               "c:\\MainClock\\Folder2\\backup\\Data.csv",
                           };

        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns<string>((directory) =>
        {
            var directories = allFiles.Where(file => IsFileUnderDirectory(directory, file)).Select((file) => Path.GetDirectoryName(file)).Distinct();
            return directories.ToArray();
        });

        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns<string>((directory) => allFiles.Where((file) => Path.GetDirectoryName(file).Equals(directory, StringComparison.OrdinalIgnoreCase)).Distinct().ToArray());

        // Act
        var files = _fileUtility.Object.AddFilesFromDirectory("C:\\MainClock", false);

        // Validate
        foreach (var sourceFile in allFiles)
        {
            Assert.IsTrue(files.Any((file) => file.Equals(sourceFile, StringComparison.OrdinalIgnoreCase)), $"{sourceFile} not returned in the list from AddFilesFromDirectory");
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
        _fileUtility.Setup(fu => fu.GetFilesInADirectory(It.IsAny<string>())).Returns((string dp) => files.Where(f => f.Contains(dp) && f.LastIndexOf("\\") == (f.IndexOf(dp) + dp.Length) && !f.EndsWith("\\"))
                    .ToArray());
        _fileUtility.Setup(fu => fu.GetDirectoriesInADirectory(It.IsAny<string>())).Returns((string dp) => files.Where(f => f.Contains(dp) && f.LastIndexOf("\\") > (f.IndexOf(dp) + dp.Length))
                    .Select(f =>
                    {
                        var val = f.Substring(
                            f.IndexOf(dp) + dp.Length + 1,
                            f.Length - (f.IndexOf(dp) + dp.Length + 1));
                        return f.Substring(0, dp.Length + 1 + val.IndexOf("\\"));
                    })
                    .Distinct()
                    .ToArray());
    }

    #endregion
}
