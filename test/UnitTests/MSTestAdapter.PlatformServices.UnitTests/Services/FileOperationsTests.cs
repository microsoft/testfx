// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class FileOperationsTests : TestContainer
{
    private readonly FileOperations _fileOperations;

    public FileOperationsTests()
    {
        _fileOperations = new FileOperations();
    }

    public void LoadAssemblyShouldThrowExceptionIfTheFileNameHasInvalidCharacters()
    {
        var filePath = "temp<>txt";
        void A() => _fileOperations.LoadAssembly(filePath, false);

        Type expectedException;
#if NETCOREAPP
        expectedException = typeof(FileNotFoundException);
#else
        expectedException = typeof(ArgumentException);
#endif

        var ex = VerifyThrows(A);
        Verify(ex.GetType() == expectedException);
    }

    public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
    {
        var filePath = "temptxt";
        void A() => _fileOperations.LoadAssembly(filePath, false);
        var ex = VerifyThrows(A);
        Verify(ex is FileNotFoundException);
    }

    public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
    {
        var filePath = typeof(FileOperationsTests).GetTypeInfo().Assembly.Location;

        // This should not throw.
        _fileOperations.LoadAssembly(filePath, false);
    }

#if !WIN_UI
    public void DoesFileExistReturnsTrueForAllFiles()
    {
        Verify(_fileOperations.DoesFileExist(null));
        Verify(_fileOperations.DoesFileExist("foobar"));
    }
#endif

    public void GetFullFilePathShouldReturnAssemblyFileName()
    {
        Verify(_fileOperations.GetFullFilePath(null) is null);
        Verify(_fileOperations.GetFullFilePath("assemblyFileName") == "assemblyFileName");
    }
}
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

#endif
