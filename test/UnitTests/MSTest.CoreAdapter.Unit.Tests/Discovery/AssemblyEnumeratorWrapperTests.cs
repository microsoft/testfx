// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

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
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class AssemblyEnumeratorWrapperTests
{
    private AssemblyEnumeratorWrapper testableAssemblyEnumeratorWrapper;

    private ICollection<string> warnings;

    private TestablePlatformServiceProvider testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        testableAssemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        warnings = new List<string>();

        testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfAssemblyNameIsNull()
    {
        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(null, null, out warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty()
    {
        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(string.Empty, null, out warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileDoesNotExistInContext()
    {
        var assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings));

        // Also validate that we give a warning when this happens.
        Assert.IsNotNull(warnings);
        var innerMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_FileDoesNotExist,
            assemblyName);
        var message = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_AssemblyDiscoveryFailure,
            assemblyName,
            innerMessage);
        CollectionAssert.Contains(warnings.ToList(), message);
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        var assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnTestElements()
    {
        var assemblyName = Assembly.GetExecutingAssembly().FullName;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        var tests = testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings);

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

        testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings);

        testablePlatformServiceProvider.MockTestSourceHost.Verify(ih => ih.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()), Times.Once);
    }

    #region Exception handling tests.

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileCannotBeLoaded()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(false);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(true);

        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfSourceFileLoadThrowsABadImageFormatException()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new BadImageFormatException());

        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings));
    }

    [TestMethod]
    public void GetTestsShouldReturnNullIfThereIsAReflectionTypeLoadException()
    {
        var assemblyName = "DummyAssembly.dll";
        var fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new ReflectionTypeLoadException(null, null));

        Assert.IsNull(testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out warnings));
    }

    #endregion

    #region private helpers

    private void SetupMocks(string assemblyName, bool doesFileExist, bool isAssemblyReferenced)
    {
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(doesFileExist);
        testablePlatformServiceProvider.MockTestSourceValidator.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), assemblyName)).Returns(isAssemblyReferenced);
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        testablePlatformServiceProvider.MockTestSourceHost.Setup(
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
