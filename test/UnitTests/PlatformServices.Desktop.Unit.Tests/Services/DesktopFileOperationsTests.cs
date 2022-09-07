// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services;

extern alias FrameworkV1;

using System;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class DesktopFileOperationsTests
{
    private FileOperations _fileOperations;

    [TestInitialize]
    public void TestInit()
    {
        _fileOperations = new FileOperations();
    }

    [TestMethod]
    public void CreateNavigationSessionShouldReurnNullIfSourceIsNull()
    {
        Assert.IsNull(_fileOperations.CreateNavigationSession(null));
    }

    [TestMethod]
    public void CreateNavigationSessionShouldReturnDiaSession()
    {
        var diaSession = _fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        Assert.IsTrue(diaSession is DiaSession);
    }

    [TestMethod]
    public void GetNavigationDataShouldReturnDataFromNavigationSession()
    {
        var diaSession = _fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        _fileOperations.GetNavigationData(
            diaSession,
            typeof(DesktopFileOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out var minLineNumber,
            out var fileName);

        Assert.AreNotEqual(-1, minLineNumber);
        Assert.IsNotNull(fileName);
    }

    [TestMethod]
    public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
    {
        _fileOperations.GetNavigationData(
            null,
            typeof(DesktopFileOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out var minLineNumber,
            out var fileName);

        Assert.AreEqual(-1, minLineNumber);
        Assert.IsNull(fileName);
    }

    [TestMethod]
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

        Assert.IsTrue(isExceptionThrown);
    }

    [TestMethod]
    public void DisposeNavigationSessionShouldNotThrowOnNullNavigationSession()
    {
        // This should not throw.
        _fileOperations.DisposeNavigationSession(null);
    }

    [TestMethod]
    public void DoesFileExistReturnsFalseIfAssemblyNameIsNull()
    {
        Assert.IsFalse(_fileOperations.DoesFileExist(null));
    }

    [TestMethod]
    public void DoesFileExistReturnsFalseIfFileDoesNotExist()
    {
        Assert.IsFalse(_fileOperations.DoesFileExist("C:\\temp1foobar.txt"));
    }

    [TestMethod]
    public void DoesFileExistReturnsTrueIfFileExists()
    {
        Assert.IsTrue(_fileOperations.DoesFileExist(Assembly.GetExecutingAssembly().Location));
    }

    [TestMethod]
    public void GetFullFilePathShouldReturnAssemblyFileNameOnException()
    {
        var filePath = "temp<>txt";
        Assert.AreEqual(filePath, _fileOperations.GetFullFilePath(filePath));
    }

    [TestMethod]
    public void GetFullFilePathShouldReturnFullFilePathForAFile()
    {
        var filePath = "temp1.txt";
        Assert.AreEqual(Path.GetFullPath(filePath), _fileOperations.GetFullFilePath(filePath));
    }
}
#endif
