// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopFileOperationsTests : TestContainer
{
    private readonly FileOperations _fileOperations;

    public DesktopFileOperationsTests()
    {
        _fileOperations = new FileOperations();
    }

    public void CreateNavigationSessionShouldReturnNullIfSourceIsNull()
    {
        Verify(_fileOperations.CreateNavigationSession(null) is null);
    }

    public void CreateNavigationSessionShouldReturnDiaSession()
    {
        var diaSession = _fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        Verify(diaSession is DiaSession);
    }

    public void GetNavigationDataShouldReturnDataFromNavigationSession()
    {
        var diaSession = _fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        _fileOperations.GetNavigationData(
            diaSession,
            typeof(DesktopFileOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out var minLineNumber,
            out var fileName);

        Verify(minLineNumber != -1);
        Verify(fileName is not null);
    }

    public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
    {
        _fileOperations.GetNavigationData(
            null,
            typeof(DesktopFileOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out var minLineNumber,
            out var fileName);

        Verify(minLineNumber == -1);
        Verify(fileName is null);
    }

    public void DisposeNavigationSessionShouldDisposeNavigationSessionInstance()
    {
        var session = _fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        _fileOperations.DisposeNavigationSession(session);
        var diaSession = session as DiaSession;
        bool isExceptionThrown = false;

        try
        {
            diaSession.GetNavigationData(
                typeof(DesktopFileOperationsTests).FullName,
                "DisposeNavigationSessionShouldDisposeNavigationSessionInstance");
        }
        catch (NullReferenceException)
        {
            isExceptionThrown = true;
        }

        Verify(isExceptionThrown);
    }

    public void DisposeNavigationSessionShouldNotThrowOnNullNavigationSession()
    {
        // This should not throw.
        _fileOperations.DisposeNavigationSession(null);
    }

    public void DoesFileExistReturnsFalseIfAssemblyNameIsNull()
    {
        Verify(!_fileOperations.DoesFileExist(null));
    }

    public void DoesFileExistReturnsFalseIfFileDoesNotExist()
    {
        Verify(!_fileOperations.DoesFileExist("C:\\temp1foobar.txt"));
    }

    public void DoesFileExistReturnsTrueIfFileExists()
    {
        Verify(_fileOperations.DoesFileExist(Assembly.GetExecutingAssembly().Location));
    }

    public void GetFullFilePathShouldReturnAssemblyFileNameOnException()
    {
        var filePath = "temp<>txt";
        Verify(filePath == _fileOperations.GetFullFilePath(filePath));
    }

    public void GetFullFilePathShouldReturnFullFilePathForAFile()
    {
        var filePath = "temp1.txt";
        Verify(Path.GetFullPath(filePath) == _fileOperations.GetFullFilePath(filePath));
    }
}
#endif
