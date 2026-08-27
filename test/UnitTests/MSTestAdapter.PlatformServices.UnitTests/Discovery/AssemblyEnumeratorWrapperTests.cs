// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
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
        => AssemblyEnumeratorWrapper.GetTests(null, null, _mockTestSourceHandler.Object, false, out _).Should().BeNull();

    public void GetTestsShouldReturnNullIfAssemblyNameIsEmpty()
        => AssemblyEnumeratorWrapper.GetTests(string.Empty, null, _mockTestSourceHandler.Object, false, out _).Should().BeNull();

    public void GetTestsShoulThrowIfSourceFileDoesNotExistInContext()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(assemblyName))
            .Returns(assemblyName);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(assemblyName))
            .Returns(false);

        Action action = () => AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, false, out _);

        action.Should().Throw<FileNotFoundException>()
            .WithMessage(string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, assemblyName));
    }

    public void GetTestsShouldReturnNullIfSourceDoesNotReferenceUnitTestFrameworkAssembly()
    {
        string assemblyName = "DummyAssembly.dll";

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: false);

        AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, false, out _).Should().BeNull();
    }

    public void GetTestsShouldReturnTestElements()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, false, out _);

        tests.Should().NotBeNull();

        // Validate if the current test is enumerated in this list.
        tests.Any(t => t.TestMethod.Name == "ValidTestMethod").Should().BeTrue();
    }

#if NETCOREAPP
    [DoNotParallelize]
    public void GetTestsShouldSelectGeneratedDescriptorsOnlyForMtp()
    {
        Type testType = typeof(GeneratedDescriptorTestClass);
        MethodInfo method = testType.GetMethod(nameof(GeneratedDescriptorTestClass.GeneratedDescriptorTestMethod))!;
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        _mockTestSourceHandler
            .Setup(handler => handler.IsAssemblyReferenced(It.IsAny<AssemblyName>(), assemblyPath))
            .Returns(true);

        PlatformServiceProvider.Instance = null;
        try
        {
            ReflectionMetadataHook.Register(
                Assembly.GetExecutingAssembly(),
                [testType],
                new Dictionary<Type, MethodInfo[]> { [testType] = [method] },
                new Dictionary<Type, Attribute[]> { [testType] = [new TestClassAttribute()] },
                [],
                new Dictionary<MethodInfo, Attribute[]> { [method] = [new TestMethodAttribute()] },
                new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>(),
                new Dictionary<Type, ConstructorInvokerInfo[]>(),
                new Dictionary<PropertyInfo, Action<object?, object?>>(),
                new Dictionary<Type, MethodInfo[]> { [testType] = [method] },
                [testType]);

            UnitTestElement mtpTest = AssemblyEnumeratorWrapper
                .GetTests(assemblyPath, null, _mockTestSourceHandler.Object, isMTP: true, out _)!
                .Single(test => test.TestMethod.MethodInfo == method);
            UnitTestElement vstestTest = AssemblyEnumeratorWrapper
                .GetTests(assemblyPath, null, _mockTestSourceHandler.Object, isMTP: false, out _)!
                .Single(test => test.TestMethod.MethodInfo == method);

            mtpTest.IsFromGeneratedDescriptor.Should().BeTrue();
            vstestTest.IsFromGeneratedDescriptor.Should().BeFalse();
        }
        finally
        {
            PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        }
    }
#endif

    public void GetTestsShouldCreateAnIsolatedInstanceOfAssemblyEnumerator()
    {
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;

        // Setup mocks.
        SetupMocks(assemblyName, doesFileExist: true, isAssemblyReferenced: true);

        AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, false, out _);

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

        Action act = () => AssemblyEnumeratorWrapper.GetTests(assemblyName, null, _mockTestSourceHandler.Object, false, out _);
        act.Should().Throw<FileNotFoundException>().WithMessage(string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, fullFilePath));
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

    [TestClass]
    public class GeneratedDescriptorTestClass
    {
        [TestMethod]
        public void GeneratedDescriptorTestMethod()
        {
        }
    }

    #endregion
}
