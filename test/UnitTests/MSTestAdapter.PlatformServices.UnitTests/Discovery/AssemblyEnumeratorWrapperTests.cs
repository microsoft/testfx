// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class AssemblyEnumeratorWrapperTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler;

    public AssemblyEnumeratorWrapperTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        _mockTestSourceHandler = new();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void GetTestsShouldReturnNullIfAssemblyNameIsNull()
        => AssemblyEnumeratorWrapper.GetTests(null, null, _mockTestSourceHandler.Object, out _).Should().BeNull();

    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty()
        => AssemblyEnumeratorWrapper.GetTests(string.Empty, null, _mockTestSourceHandler.Object, out _).Should().BeNull();

    public void GetTestsShoulThrowIfSourceFileDoesNotExistInContext()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, out _));
        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, assemblyName));
    }

    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, out _).Should().BeNull();
    }

    public void GetTestsShouldReturnTestElements()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, out _);

        tests.Should().NotBeNull();

        // Validate if the current test is enumerated in this list.
        tests.Any(t => t.TestMethod.Name == "ValidTestMethod").Should().BeTrue();
    }

    public void GetTestsShouldCreateAnIsolatedInstanceOfAssemblyEnumerator()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, out _);

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

        FileNotFoundException fileNotFoundException = Assert.Throws<FileNotFoundException>(() => AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, out _));
        fileNotFoundException.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, fullFilePath));
    }

    #endregion

    #region private helpers

    private void SetupMocks(string assemblyName, bool doesFileExist, bool isAssemblyReferenced)
    {
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(doesFileExist);
        _mockTestSourceHandler.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), assemblyName)).Returns(isAssemblyReferenced);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName))
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
