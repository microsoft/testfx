// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class AssemblyEnumeratorWrapperTests : TestContainer
{
    private readonly AssemblyEnumeratorWrapper _testableAssemblyEnumeratorWrapper;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private List<string> _warnings;

    public AssemblyEnumeratorWrapperTests()
    {
        _testableAssemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        _warnings = new List<string>();

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void GetTestsShouldReturnNullIfAssemblyNameIsNull() => Verify(_testableAssemblyEnumeratorWrapper.GetTests(null, null, out _warnings) is null);

    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty() => Verify(_testableAssemblyEnumeratorWrapper.GetTests(string.Empty, null, out _warnings) is null);

    public void GetTestsShouldReturnNullIfSourceFileDoesNotExistInContext()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        Verify(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings) is null);

        // Also validate that we give a warning when this happens.
        Verify(_warnings is not null);
        string innerMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_FileDoesNotExist,
            assemblyName);
        string message = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_AssemblyDiscoveryFailure,
            assemblyName,
            innerMessage);
        Verify(_warnings.ToList().Contains(message));
    }

    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        Verify(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings) is null);
    }

    public void GetTestsShouldReturnTestElements()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings);

        Verify(tests is not null);

        // Validate if the current test is enumerated in this list.
        Verify(tests.Any(t => t.TestMethod.Name == "ValidTestMethod"));
    }

    public void GetTestsShouldCreateAnIsolatedInstanceOfAssemblyEnumerator()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings);

        _testablePlatformServiceProvider.MockTestSourceHost.Verify(ih => ih.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()), Times.Once);
    }

    #region Exception handling tests.

    public void GetTestsShouldReturnNullIfSourceFileCannotBeLoaded()
    {
        string assemblyName = "DummyAssembly.dll";
        string fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(false);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(true);

        Verify(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings) is null);
    }

    public void GetTestsShouldReturnNullIfSourceFileLoadThrowsABadImageFormatException()
    {
        string assemblyName = "DummyAssembly.dll";
        string fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new BadImageFormatException());

        Verify(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings) is null);
    }

    public void GetTestsShouldReturnNullIfThereIsAReflectionTypeLoadException()
    {
        string assemblyName = "DummyAssembly.dll";
        string fullFilePath = Path.Combine(@"C:\temp", assemblyName);

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(fullFilePath);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(fullFilePath))
            .Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName, It.IsAny<bool>()))
            .Throws(new ReflectionTypeLoadException(null, null));

        Verify(_testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings) is null);
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
