// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

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
        void a() => _fileOperations.LoadAssembly(filePath, false);

        Type expectedException;
#if NETCOREAPP
        expectedException = typeof(FileNotFoundException);
#else
        expectedException = typeof(ArgumentException);
#endif

        var ex = VerifyThrows(a);
        Verify(ex.GetType() == expectedException);
    }

    public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
    {
        var filePath = "temptxt";
        void a() => _fileOperations.LoadAssembly(filePath, false);
        var ex = VerifyThrows(a);
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
        Verify("assemblyFileName" == _fileOperations.GetFullFilePath("assemblyFileName"));
    }
}
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

