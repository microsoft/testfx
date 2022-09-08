// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Moq;

[TestClass]
public class AssemblyEnumeratorWrapperTests
{
    private AssemblyEnumeratorWrapper _testableAssemblyEnumeratorWrapper;
    private ICollection<string> _warnings;
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        _testableAssemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        _warnings = new List<string>();

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfAssemblyNameIsNull()
    {
        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(null, null, out _warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty()
    {
        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(string.Empty, null, out _warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileDoesNotExistInContext()
    {
        var assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings));

        // Also validate that we give a warning when this happens.
        Assert.IsNotNull(_warnings);
        var innerMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_FileDoesNotExist,
            assemblyName);
        var message = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_AssemblyDiscoveryFailure,
            assemblyName,
            innerMessage);
        CollectionAssert.Contains(_warnings.ToList(), message);
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        var assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnTestElements()
    {
        var assemblyName = Assembly.GetExecutingAssembly().FullName;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        var tests = _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings);

        Assert.IsNotNull(tests);

        // Validate if the current test is enumerated in this list.
        Assert.IsTrue(tests.Any(t => t.TestMethod.Name == "ValidTestMethod"));
    }

    [TestMethod]
    public void GetTestsShouldCreateAnIsolatedInstanceOfAssemblyEnumerator()
    {
        var assemblyName = Assembly.GetExecutingAssembly().FullName;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings);

        _testablePlatformServiceProvider.MockTestSourceHost.Verify(ih => ih.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()), Times.Once);
    }

    #region Exception handling tests.

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileCannotBeLoaded()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(false);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(true);

        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileLoadThrowsABadImageFormatException()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new BadImageFormatException());

        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfThereIsAReflectionTypeLoadException()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new ReflectionTypeLoadException(null, null));

        Assert.IsNull(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings));
    }

    #endregion

    #region private helpers

    private void SetupMocks(string assemblyName, bool doesFileExist, bool isAssemblyReferenced)
    {
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(doesFileExist);
        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), assemblyName)).Returns(isAssemblyReferenced);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());
    }

    #endregion

    #region dummy implementations.

    [UTF.TestClass]
    public class ValidTestClass
    {
        // This is just a dummy method for test validation.
        [UTF.TestMethod]
        public void ValidTestMethod()
        {
        }
    }

    #endregion
}
