// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class AssemblyEnumeratorWrapperTests : TestContainer
{
    private readonly AssemblyEnumeratorWrapper _testableAssemblyEnumeratorWrapper;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private List<string> _warnings;

    public AssemblyEnumeratorWrapperTests()
    {
        _testableAssemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        _warnings = [];

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

    public void GetTestsShouldReturnNullIfAssemblyNameIsNull() => _testableAssemblyEnumeratorWrapper.GetTests(null, null, out _warnings).Should().BeNull();

    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty() => _testableAssemblyEnumeratorWrapper.GetTests(string.Empty, null, out _warnings).Should().BeNull();

    public void GetTestsShouldReturnNullIfSourceFileDoesNotExistInContext()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings).Should().BeNull();

        // Also validate that we give a warning when this happens.
        _warnings.Should().NotBeNull();
        string innerMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_FileDoesNotExist,
            assemblyName);
        string message = string.Format(
            CultureInfo.CurrentCulture,
            Resource.TestAssembly_AssemblyDiscoveryFailure,
            assemblyName,
            innerMessage);
        _warnings.ToList().Contains(message).Should().BeTrue();
    }

    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings).Should().BeNull();
    }

    public void GetTestsShouldReturnTestElements()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings);

        tests.Should().NotBeNull();

        // Validate if the current test is enumerated in this list.
        tests.Any(t => t.TestMethod.Name == "ValidTestMethod").Should().BeTrue();
    }

    public void GetTestsShouldCreateAnIsolatedInstanceOfAssemblyEnumerator()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

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

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings).Should().BeNull();
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

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings).Should().BeNull();
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

        _testableAssemblyEnumeratorWrapper.GetTests(assemblyName, null, out _warnings).Should().BeNull();
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

    [TestClass]
    public class ValidTestClass
    {
        // This is just a dummy method for test validation.
        [TestMethod]
        public void ValidTestMethod()
        {
        }
    }

    #endregion
}
