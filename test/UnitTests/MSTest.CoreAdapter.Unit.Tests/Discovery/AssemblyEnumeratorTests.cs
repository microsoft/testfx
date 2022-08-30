// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class AssemblyEnumeratorTests
{
    private AssemblyEnumerator _assemblyEnumerator;
    private ICollection<string> _warnings;
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        _assemblyEnumerator = new AssemblyEnumerator();
        _warnings = new List<string>();

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup() => PlatformServiceProvider.Instance = null;

    #region  Constructor tests

    [TestMethodV1]
    public void ConstructorShouldPopulateSettings()
    {
        string runSettingsXml =
             @"<RunSettings>
                     <MSTest>
                        <ForcedLegacyMode>True</ForcedLegacyMode>
                        <SettingsFile>DummyPath\TestSettings1.testsettings</SettingsFile>
                     </MSTest>
                   </RunSettings>";

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName);
        var assemblyEnumerator = new AssemblyEnumerator(adapterSettings);
        assemblyEnumerator.RunSettingsXml = runSettingsXml;

        Assert.IsTrue(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Assert.AreEqual("DummyPath\\TestSettings1.testsettings", MSTestSettings.CurrentSettings.TestSettingsFile);
    }

    #endregion

    #region GetTypes tests

    [TestMethodV1]
    public void GetTypesShouldReturnEmptyArrayWhenNoDeclaredTypes()
    {
        Mock<TestableAssembly> mockAssembly = new();

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Returns(new List<TypeInfo>());

        Assert.AreEqual(0, _assemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings).Length);
    }

    [TestMethodV1]
    public void GetTypesShouldReturnSetOfDefinedTypes()
    {
        Mock<TestableAssembly> mockAssembly = new();

        var expectedTypes = new List<TypeInfo>() { typeof(DummyTestClass).GetTypeInfo(), typeof(DummyTestClass).GetTypeInfo() };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Returns(expectedTypes);

        var types = _assemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);
        CollectionAssert.AreEqual(expectedTypes, types);
    }

    [TestMethodV1]
    public void GetTypesShouldHandleReflectionTypeLoadException()
    {
        Mock<TestableAssembly> mockAssembly = new();

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Throws(new ReflectionTypeLoadException(null, null));

        _assemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);
    }

    [TestMethodV1]
    public void GetTypesShouldReturnReflectionTypeLoadExceptionTypesOnException()
    {
        Mock<TestableAssembly> mockAssembly = new();
        var reflectedTypes = new Type[] { typeof(DummyTestClass) };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Throws(new ReflectionTypeLoadException(reflectedTypes, null));

        var types = _assemblyEnumerator.GetTypes(mockAssembly.Object, string.Empty, _warnings);

        Assert.IsNotNull(types);
        CollectionAssert.AreEqual(reflectedTypes, types);
    }

    [TestMethodV1]
    public void GetTypesShouldLogWarningsWhenReflectionFailsWithLoaderExceptions()
    {
        Mock<TestableAssembly> mockAssembly = new();
        var exceptions = new Exception[] { new Exception("DummyLoaderException") };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Throws(new ReflectionTypeLoadException(null, exceptions));

        var types = _assemblyEnumerator.GetTypes(mockAssembly.Object, "DummyAssembly", _warnings);

        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(
            _warnings.ToList(),
            string.Format(CultureInfo.CurrentCulture, Resource.TypeLoadFailed, "DummyAssembly", "System.Exception: DummyLoaderException\r\n"));

        _testablePlatformServiceProvider.MockTraceLogger.Verify(tl => tl.LogWarning("{0}", exceptions[0]), Times.Once);
    }

    #endregion

    #region GetLoadExceptionDetails tests

    [TestMethodV1]
    public void GetLoadExceptionDetailsShouldReturnExceptionMessageIfLoaderExceptionsIsNull() => Assert.AreEqual(
            "DummyMessage\r\n",
            _assemblyEnumerator.GetLoadExceptionDetails(
                new ReflectionTypeLoadException(null, null, "DummyMessage")));

    [TestMethodV1]
    public void GetLoadExceptionDetailsShouldReturnLoaderExceptionMessage()
    {
        var loaderException = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(null, new Exception[] { loaderException });

        Assert.AreEqual(
            string.Concat(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.EnumeratorLoadTypeErrorFormat,
                    loaderException.GetType(),
                    loaderException.Message),
                "\r\n"),
            _assemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    [TestMethodV1]
    public void GetLoadExceptionDetailsShouldReturnLoaderExceptionMessagesForMoreThanOneException()
    {
        var loaderException1 = new ArgumentNullException("DummyLoaderExceptionMessage1", (Exception)null);
        var loaderException2 = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(
            null,
            new Exception[] { loaderException1, loaderException2 });
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

        Assert.AreEqual(errorDetails.ToString(), _assemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    [TestMethodV1]
    public void GetLoadExceptionDetailsShouldLogUniqueExceptionsOnly()
    {
        var loaderException = new AccessViolationException("DummyLoaderExceptionMessage2");
        var exceptions = new ReflectionTypeLoadException(null, new Exception[] { loaderException, loaderException });

        Assert.AreEqual(
            string.Concat(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.EnumeratorLoadTypeErrorFormat,
                    loaderException.GetType(),
                    loaderException.Message),
                "\r\n"),
            _assemblyEnumerator.GetLoadExceptionDetails(exceptions));
    }

    #endregion

    #region EnumerateAssembly tests

    [TestMethodV1]
    public void EnumerateAssemblyShouldReturnEmptyListWhenNoDeclaredTypes()
    {
        var mockAssembly = CreateMockTestableAssembly();

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes).Returns(new List<TypeInfo>());
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);

        Assert.AreEqual(0, _assemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings).Count);
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldReturnEmptyListWhenNoTestElementsInAType()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(DummyTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out _warnings))
            .Returns((ICollection<UnitTestElement>)null);

        Assert.AreEqual(0, _assemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings).Count);
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldReturnTestElementsForAType()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(DummyTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out _warnings))
            .Returns(new Collection<UnitTestElement> { unitTestElement });

        var testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);

        CollectionAssert.AreEqual(new Collection<UnitTestElement> { unitTestElement }, testElements.ToList());
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldReturnMoreThanOneTestElementForAType()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));
        var expectedTestElements = new Collection<UnitTestElement> { unitTestElement, unitTestElement };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(DummyTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out _warnings))
            .Returns(expectedTestElements);

        var testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);

        CollectionAssert.AreEqual(expectedTestElements, testElements.ToList());
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldReturnMoreThanOneTestElementForMoreThanOneType()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var unitTestElement = new UnitTestElement(new TestMethod("DummyMethod", "DummyClass", "DummyAssembly", false));
        var expectedTestElements = new Collection<UnitTestElement> { unitTestElement, unitTestElement };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(DummyTestClass).GetTypeInfo(), typeof(DummyTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out _warnings))
            .Returns(expectedTestElements);

        var testElements = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);

        expectedTestElements.Add(unitTestElement);
        expectedTestElements.Add(unitTestElement);
        CollectionAssert.AreEqual(expectedTestElements, testElements.ToList());
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldNotLogWarningsIfNonePresent()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        ICollection<string> warningsFromTypeEnumerator = null;

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(InternalTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out warningsFromTypeEnumerator));

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldLogWarningsIfPresent()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        ICollection<string> warningsFromTypeEnumerator = new Collection<string>
        {
            "DummyWarning"
        };

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(InternalTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out warningsFromTypeEnumerator));

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);

        CollectionAssert.AreEqual(warningsFromTypeEnumerator.ToList(), _warnings.ToList());
    }

    [TestMethodV1]
    public void EnumerateAssemblyShouldHandleExceptionsWhileEnumeratingAType()
    {
        var mockAssembly = CreateMockTestableAssembly();
        var testableAssemblyEnumerator = new TestableAssemblyEnumerator();
        var exception = new Exception("TypeEnumerationException");

        // Setup mocks
        mockAssembly.Setup(a => a.DefinedTypes)
            .Returns(new List<TypeInfo>() { typeof(InternalTestClass).GetTypeInfo() });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("DummyAssembly", false))
            .Returns(mockAssembly.Object);
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(out _warnings)).Throws(exception);

        testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly", out _warnings);

        CollectionAssert.Contains(
            _warnings.ToList(),
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.CouldNotInspectTypeDuringDiscovery,
                typeof(InternalTestClass),
                "DummyAssembly",
                exception.Message));
    }

    private static Mock<TestableAssembly> CreateMockTestableAssembly()
    {
        var mockAssembly = new Mock<TestableAssembly>();

        // The mock must be configured with a return value for GetCustomAttributes for this attribute type, but the
        // actual return value is irrelevant for these tests.
        mockAssembly
            .Setup(a => a.GetCustomAttributes(
                typeof(FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.DiscoverInternalsAttribute),
                true))
            .Returns(new Attribute[0]);

        mockAssembly
            .Setup(a => a.GetCustomAttributes(
                typeof(FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestDataSourceDiscoveryAttribute),
                true))
            .Returns(new Attribute[0]);

        return mockAssembly;
    }

    #endregion
}

#region Testable Implementations

public class TestableAssembly : Assembly
{
}

internal class TestableAssemblyEnumerator : AssemblyEnumerator
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
            testMethodValidator.Object);
    }

    internal Mock<TypeEnumerator> MockTypeEnumerator { get; set; }

    internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals) => MockTypeEnumerator.Object;
}

#endregion
