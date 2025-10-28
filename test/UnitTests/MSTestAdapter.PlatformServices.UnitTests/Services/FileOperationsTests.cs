// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class FileOperationsTests : TestContainer
{
    private readonly FileOperations _fileOperations;

    public FileOperationsTests() => _fileOperations = new FileOperations();

    public void LoadAssemblyShouldThrowExceptionIfTheFileNameHasInvalidCharacters()
    {
        string filePath = "temp<>txt";
        Action action = () => _fileOperations.LoadAssembly(filePath, false);

#if NETCOREAPP
        action.Should().Throw<FileNotFoundException>();
#else
        action.Should().Throw<ArgumentException>();
#endif
    }

    public void LoadAssemblyShouldNotThrowFileLoadExceptionIfTheFileNameHasValidFileCharacterButInvalidFullAssemblyNameCharacter()
    {
#if NETCOREAPP
        // = (for example) is a valid file name character, but not a valid character in an full assembly name.
        // If we construct assembly name by calling new AssemblyName(filePath), it will throw FileLoadException for a correct file name.
        // This test is checking that. It still fails with FileNotFoundException, because the file does not exist, but it should not throw FileLoadException.
        // (The FileLoadException used for the unparseable name is weird choice, and confusing to me, but that is what the runtime decided to do. No dll is being loaded.)
        string filePath = "temp=txt";
        Action action = () => _fileOperations.LoadAssembly(filePath, false);

        action.Should().Throw<FileNotFoundException>();
#endif
    }

    public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound() =>
        new Action(() => _fileOperations.LoadAssembly("temptxt", false)).Should().Throw<FileNotFoundException>();

    public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
    {
        string filePath = typeof(FileOperationsTests).Assembly.Location;

        // This should not throw.
        _fileOperations.LoadAssembly(filePath);
    }

#if !WIN_UI
    public void DoesFileExistReturnsTrueForAllFiles()
    {
        _fileOperations.DoesFileExist(null!).Should().BeTrue();
        _fileOperations.DoesFileExist("foobar").Should().BeTrue();
    }
#endif

    public void GetFullFilePathShouldReturnAssemblyFileName()
    {
        _fileOperations.GetFullFilePath(null!).Should().BeNull();
        _fileOperations.GetFullFilePath("assemblyFileName").Should().Be("assemblyFileName");
    }
}
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

#endif
