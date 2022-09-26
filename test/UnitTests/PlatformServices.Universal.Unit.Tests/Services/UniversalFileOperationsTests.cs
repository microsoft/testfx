// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UWP.UnitTests;

using System;
using System.IO;
using System.Reflection;

using TestFramework.ForTestingMSTest;

/// <summary>
/// The universal file operations tests.
/// </summary>
public class UniversalFileOperationsTests : TestContainer
{
    private readonly FileOperations _fileOperations;

    /// <summary>
    /// The test initialization.
    /// </summary>
    public UniversalFileOperationsTests()
    {
        _fileOperations = new FileOperations();
    }

    /// <summary>
    /// The load assembly should throw exception if the file name has invalid characters.
    /// </summary>
    public void LoadAssemblyShouldThrowExceptionIfTheFileNameHasInvalidCharacters()
    {
        var filePath = "temp<>txt";
        void a() => _fileOperations.LoadAssembly(filePath, false);
        var ex = VerifyThrows(a);
        Verify(ex is ArgumentException);
    }

    /// <summary>
    /// The load assembly should throw exception if file is not found.
    /// </summary>
    public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
    {
        var filePath = "temptxt";
        void a() => _fileOperations.LoadAssembly(filePath, false);
        var ex = VerifyThrows(a);
        Verify(ex is FileNotFoundException);
    }

    /// <summary>
    /// The load assembly should load assembly in current context.
    /// </summary>
    public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
    {
        var filePath = Assembly.GetExecutingAssembly().Location;

        // This should not throw.
        _fileOperations.LoadAssembly(filePath, false);
    }

    /// <summary>
    /// The does file exist returns false if file name has invalid characters.
    /// </summary>
    public void DoesFileExistReturnsFalseIfFileNameHasInvalidCharacters()
    {
        var filePath = "temp<>txt";
        Verify(!_fileOperations.DoesFileExist(filePath));
    }

    /// This Test is not yet validated. Will validate with new adapter.
    /// <summary>
    /// The does file exist returns false if file is not found.
    /// </summary>
    // [TestMethod]
    public void DoesFileExistReturnsFalseIfFileIsNotFound()
    {
        var filePath = "C:\\footemp.txt";
        void a() => _fileOperations.DoesFileExist(filePath);
        var ex = VerifyThrows(a);
        Verify(ex is FileNotFoundException);
    }

    /// This Test is not yet validated. Will validate with new adapter.
    /// <summary>
    /// The does file exist returns true when file exists.
    /// </summary>
    // [TestMethod]
    public void DoesFileExistReturnsTrueWhenFileExists()
    {
        var filePath = Assembly.GetExecutingAssembly().Location;
        Verify(_fileOperations.DoesFileExist(filePath));
    }

    /// <summary>
    /// The create navigation session should return null for all sources.
    /// </summary>
    // TODO: Re-enable this tests when we have merged projects (make public to re-include)
    private void CreateNavigationSessionShouldReturnNullForAllSources()
    {
        Verify(_fileOperations.CreateNavigationSession(null) is null);
        Verify(_fileOperations.CreateNavigationSession("foobar") is null);
    }

    public void GetNavigationDataShouldReturnNullFileName()
    {
        _fileOperations.GetNavigationData(null, null, null, out var minLineNumber, out var fileName);
        Verify(fileName is null);
        Verify(-1 == minLineNumber);
    }

    // Enable these tests when we take dependency on TpV2 object model
    // In Tpv1 UWP Object model these below methods are not defined.
    /*
    [TestMethod]
    public void CreateNavigationSessionShouldReturnDiaSession()
    {
        var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        Assert.IsNotNull(diaSession);
    }

    [TestMethod]
    public void GetNavigationDataShouldReturnDataFromNavigationSession()
    {
        var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
        int minLineNumber;
        string fileName;
        this.fileOperations.GetNavigationData(
            diaSession,
            typeof(UniversalFileOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out minLineNumber,
            out fileName);

        Assert.AreNotEqual(-1, minLineNumber);
        Assert.IsNotNull(fileName);
    }

    [TestMethod]
    public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
    {
        int minLineNumber;
        string fileName;
        this.fileOperations.GetNavigationData(
            null,
            typeof(UniversalFileOperationsTests).FullName,
            "GetNavigationDataShouldNotThrowOnNullNavigationSession",
            out minLineNumber,
            out fileName);

        Assert.AreEqual(-1, minLineNumber);
        Assert.IsNull(fileName);
    }

    [TestMethod]
    public void DisposeNavigationSessionShouldNotThrowOnNullNavigationSession()
    {
        // This should not throw.
        this.fileOperations.DisposeNavigationSession(null);
    }
    */

    /// <summary>
    /// The get full file path should return assembly file name.
    /// </summary>
    public void GetFullFilePathShouldReturnAssemblyFileName()
    {
        Verify(_fileOperations.GetFullFilePath(null) is null);
        Verify("assemblyFileName" == _fileOperations.GetFullFilePath("assemblyFileName"));
    }
}
