// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET462
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
        void A() => _fileOperations.LoadAssembly(filePath, false);

#if NETCOREAPP
        VerifyThrows<FileNotFoundException>(A);
#else
        VerifyThrows<ArgumentException>(A);
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
        void A() => _fileOperations.LoadAssembly(filePath, false);

        VerifyThrows<FileNotFoundException>(A);
#endif
    }

    public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound() =>
        VerifyThrows<FileNotFoundException>(() => _fileOperations.LoadAssembly("temptxt", false));

    public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
    {
        string filePath = typeof(FileOperationsTests).Assembly.Location;

        // This should not throw.
        _fileOperations.LoadAssembly(filePath, false);
    }

#if !WIN_UI
    public void DoesFileExistReturnsTrueForAllFiles()
    {
        Verify(_fileOperations.DoesFileExist(null!));
        Verify(_fileOperations.DoesFileExist("foobar"));
    }
#endif

    public void GetFullFilePathShouldReturnAssemblyFileName()
    {
        Verify(_fileOperations.GetFullFilePath(null!) is null);
        Verify(_fileOperations.GetFullFilePath("assemblyFileName") == "assemblyFileName");
    }
}
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

#endif
