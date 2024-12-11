// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class AssemblyEnumeratorTests : TestContainer
{
    private readonly AssemblyEnumerator _assemblyEnumerator;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private readonly List<string> _warnings;

    public AssemblyEnumeratorTests()
    {
        _assemblyEnumerator = new AssemblyEnumerator();
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

    #region  Constructor tests

    public void ConstructorShouldPopulateSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <ForcedLegacyMode>True</ForcedLegacyMode>
                <SettingsFile>DummyPath\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });
        var mockMessageLogger = new Mock<IMessageLogger>();
        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, mockMessageLogger.Object);

        // Constructor has the side effect of populating the passed settings to MSTestSettings.CurrentSettings
        _ = new AssemblyEnumerator(adapterSettings);

        Verify(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Verify(MSTestSettings.CurrentSettings.TestSettingsFile == "DummyPath\\TestSettings1.testsettings");
    }

    #endregion

    #region GetTypes tests

    public void GetTypesShouldReturnEmptyArrayWhenNoDeclaredTypes()
    {
        Mock<TestableAssembly> mockAssembly = new();

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Returns([]);

        Verify(AssemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings).Length == 0);
    }

    public void GetTypesShouldReturnSetOfDefinedTypes()
    {
        Mock<TestableAssembly> mockAssembly = new();

        TypeInfo[] expectedTypes = [typeof(DummyTestClass).GetTypeInfo(), typeof(DummyTestClass).GetTypeInfo()];

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Returns(expectedTypes);

        IReadOnlyList<Type> types = AssemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);
        Verify(expectedTypes.SequenceEqual(types));
    }

    public void GetTypesShouldHandleReflectionTypeLoadException()
    {
        Mock<TestableAssembly> mockAssembly = new();

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Throws(new ReflectionTypeLoadException(null, null));

        AssemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);
    }

    public void GetTypesShouldReturnReflectionTypeLoadExceptionTypesOnException()
    {
        Mock<TestableAssembly> mockAssembly = new();
        var reflectedTypes = new Type[] { typeof(DummyTestClass) };

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Throws(new ReflectionTypeLoadException(reflectedTypes, null));

        IReadOnlyList<Type> types = AssemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);

        Verify(types is not null);
        Verify(reflectedTypes.Equals(types));
    }

    public void GetTypesShouldLogWarningsWhenReflectionFailsWithLoaderExceptions()
    {
        Mock<TestableAssembly> mockAssembly = new();
        var exceptions = new Exception[] { new("DummyLoaderException") };

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Throws(new ReflectionTypeLoadException(null, exceptions));
        mockAssembly.Setup(a => a.GetTypes()).Throws(new ReflectionTypeLoadException(null, exceptions));

        IReadOnlyList<Type> types = AssemblyEnumerator.GetTypes(mockAssembly.Object, "DummyAssembly", _warnings);

        Verify(_warnings.Count == 1);
        Verify(_warnings.ToList().Contains(
            string.Format(CultureInfo.CurrentCulture, Resource.TypeLoadFailed, "DummyAssembly", "System.Exception: DummyLoaderException\r\n")));

        _testablePlatformServiceProvider.MockTraceLogger.Verify(tl => tl.LogWarning("{0}", exceptions[0]), Times.Once);
    }

    #endregion

    #region GetLoadExceptionDetails tests

    public void GetLoadExceptionDetailsShouldReturnExceptionMessageIfLoaderExceptionsIsNull() => Verify(
            AssemblyEnumerator.GetLoadExceptionDetails(
                new ReflectionTypeLoadException(null, null, "DummyMessage")) ==
            "DummyMessage\r\n");

    public void GetLoadExceptionDetailsShouldReturnLoaderExceptionMessage()
    {
        var loaderException = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(null, [loaderException]);

        Verify(
            string.Concat(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.EnumeratorLoadTypeErrorFormat,
                    loaderException.GetType(),
                    loaderException.Message),
                "\r\n") ==
            AssemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    public void GetLoadExceptionDetailsShouldReturnLoaderExceptionMessagesForMoreThanOneException()
    {
        var loaderException1 = new ArgumentNullException("DummyLoaderExceptionMessage1", (Exception)null);
        var loaderException2 = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(
            null,
            [loaderException1, loaderException2]);
        StringBuilder errorDetails = new();

        errorDetails.AppendFormat(
                CultureInfo.CurrentCulture,
                Resource.EnumeratorLoadTypeErrorFormat,
                loaderException1.GetType(),
                loaderException1.Message).AppendLine();
        errorDetails.AppendFormat(
                CultureInfo.CurrentCulture,
                Resource.EnumeratorLoadTypeErrorFormat,
                loaderException2.GetType(),
                loaderException2.Message).AppendLine();

        Verify(errorDetails.ToString() == AssemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    public void GetLoadExceptionDetailsShouldLogUniqueExceptionsOnly()
    {
        var loaderException = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(null, [loaderException, loaderException]);

        Verify(
            string.Concat(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.EnumeratorLoadTypeErrorFormat,
                    loaderException.GetType(),
                    loaderException.Message),
                "\r\n") ==
            AssemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    #endregion

    #region EnumerateAssembly tests

    public void EnumerateAssemblyShouldReturnEmptyListWhenNoDeclaredTypes()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Returns([]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);

        Verify(_assemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings).Count == 0);
    }

    public void EnumerateAssemblyShouldReturnEmptyListWhenNoTestElementsInAType()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings))
            .Returns((List<UnitTestElement>)null);

        Verify(_assemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings).Count == 0);
    }

    public void EnumerateAssemblyShouldReturnTestElementsForAType()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings))
            .Returns(new List<UnitTestElement> { unitTestElement });

        ICollection<UnitTestElement> testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);

        Verify(new Collection<UnitTestElement> { unitTestElement }.SequenceEqual(testElements));
    }

    public void EnumerateAssemblyShouldReturnMoreThanOneTestElementForAType()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));
        var expectedTestElements = new List<UnitTestElement> { unitTestElement, unitTestElement };

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings))
            .Returns(expectedTestElements);

        ICollection<UnitTestElement> testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);

        Verify(expectedTestElements.SequenceEqual(testElements));
    }

    public void EnumerateAssemblyShouldReturnMoreThanOneTestElementForMoreThanOneType()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));
        var expectedTestElements = new List<UnitTestElement> { unitTestElement, unitTestElement };

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass), typeof(DummyTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(DummyTestClass).GetTypeInfo(), typeof(DummyTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings))
            .Returns(expectedTestElements);

        ICollection<UnitTestElement> testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);

        expectedTestElements.Add(unitTestElement);
        expectedTestElements.Add(unitTestElement);
        Verify(expectedTestElements.SequenceEqual(testElements));
    }

    public void EnumerateAssemblyShouldNotLogWarningsIfNonePresent()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        List<string> warningsFromTypeEnumerator = [];

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(warningsFromTypeEnumerator));

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);
        Verify(_warnings.Count == 0);
    }

    public void EnumerateAssemblyShouldLogWarningsIfPresent()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var warningsFromTypeEnumerator = new List<string>
        {
            "DummyWarning",
        };

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings))
            .Callback(() => _warnings.AddRange(warningsFromTypeEnumerator));

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);

        Verify(warningsFromTypeEnumerator.SequenceEqual(_warnings));
    }

    public void EnumerateAssemblyShouldHandleExceptionsWhileEnumeratingAType()
    {
        Mock<TestableAssembly> mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var exception = new Exception("TypeEnumerationException");

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass)]);
        mockAssembly.Setup(a => a.GetTypes())
            .Returns([typeof(InternalTestClass).GetTypeInfo()]);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(_warnings)).Throws(exception);

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", null, _warnings);

        Verify(_warnings.ToList().Contains(
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.CouldNotInspectTypeDuringDiscovery,
                typeof(InternalTestClass),
                "DummyAssembly",
                exception.Message)));
    }

    private static Mock<TestableAssembly> CreateMockTestableAssembly()
    {
        var mockAssembly = new Mock<TestableAssembly>();

        // The mock must be configured with a return value for GetCustomAttributes for this attribute type, but the
        // actual return value is irrelevant for these tests.
        mockAssembly
            .Setup(a => a.GetCustomAttributes(
                typeof(DiscoverInternalsAttribute),
                true))
            .Returns(Array.Empty<Attribute>());

        mockAssembly
            .Setup(a => a.GetCustomAttributes(
                typeof(TestDataSourceOptionsAttribute),
                true))
            .Returns(Array.Empty<Attribute>());

        mockAssembly
            .Setup(a => a.GetCustomAttributes(
#pragma warning disable CS0618 // Type or member is obsolete
                typeof(TestDataSourceDiscoveryAttribute),
#pragma warning restore CS0618 // Type or member is obsolete
                true))
            .Returns(Array.Empty<Attribute>());

        mockAssembly
            .Setup(a => a.GetCustomAttributes(
                typeof(TestIdGenerationStrategyAttribute),
                true))
            .Returns(Array.Empty<Attribute>());

        return mockAssembly;
    }

    #endregion
}

#region Testable Implementations

public class TestableAssembly : Assembly;

internal sealed class TestableAssemblyEnumerator : AssemblyEnumerator
{
    internal TestableAssemblyEnumerator()
    {
        var reflectHelper = new Mock<ReflectHelper>();
        var typeValidator = new Mock<TypeValidator>(reflectHelper.Object);
        var testMethodValidator = new Mock<TestMethodValidator>(reflectHelper.Object);
        MockTypeEnumerator = new Mock<TypeEnumerator>(
            typeof(DummyTestClass),
            "DummyAssembly",
            reflectHelper.Object,
            typeValidator.Object,
            testMethodValidator.Object,
            TestIdGenerationStrategy.FullyQualified);
    }

    internal Mock<TypeEnumerator> MockTypeEnumerator { get; set; }

    internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals, TestIdGenerationStrategy testIdGenerationStrategy)
        => MockTypeEnumerator.Object;
}

#endregion
