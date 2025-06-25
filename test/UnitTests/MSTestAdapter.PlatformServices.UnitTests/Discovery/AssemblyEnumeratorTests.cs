// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

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
                actualReader.Read();
                actualReader.ReadInnerXml();
            });
        var mockMessageLogger = new Mock<IMessageLogger>();
        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, mockMessageLogger.Object)!;

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

        Verify(AssemblyEnumerator.GetTypes(mockAssembly.Object).Length == 0);
    }

    public void GetTypesShouldReturnSetOfDefinedTypes()
    {
        Mock<TestableAssembly> mockAssembly = new();

        TypeInfo[] expectedTypes = [typeof(DummyTestClass).GetTypeInfo(), typeof(DummyTestClass).GetTypeInfo()];

        // Setup mocks
        mockAssembly.Setup(a => a.GetTypes()).Returns(expectedTypes);

        Type[] types = AssemblyEnumerator.GetTypes(mockAssembly.Object);
        Verify(expectedTypes.SequenceEqual(types));
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

        AssemblyEnumerationResult result = _assemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);
        Verify(result.TestElements.Count == 0);
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
            .Returns((List<UnitTestElement>)null!);

        AssemblyEnumerationResult result = _assemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);
        Verify(result.TestElements.Count == 0);
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
            .Returns([unitTestElement]);

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);

        Verify(new Collection<UnitTestElement> { unitTestElement }.SequenceEqual(result.TestElements));
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

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);

        Verify(expectedTestElements.SequenceEqual(result.TestElements));
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

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);

        expectedTestElements.Add(unitTestElement);
        expectedTestElements.Add(unitTestElement);
        Verify(expectedTestElements.SequenceEqual(result.TestElements));
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

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);
        Verify(result.Warnings.Count == 0);
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
        testableAssemblyEnumerator.MockTypeEnumerator.Setup(te => te.Enumerate(It.IsAny<List<string>>()))
            .Callback<List<string>>((w) => w.AddRange(warningsFromTypeEnumerator));

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");

        Verify(warningsFromTypeEnumerator.SequenceEqual(result.Warnings));
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

        AssemblyEnumerationResult result = testableAssemblyEnumerator.EnumerateAssembly("DummyAssembly");
        _warnings.AddRange(result.Warnings);

        Verify(result.Warnings.Contains(
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
        // NOTE: Don't convert Array.Empty<Attribute>()  to [] as it will cause an InvalidCastException.
        // [] will produce `object[]`, then it will fail to cast here:
        // https://github.com/dotnet/runtime/blob/4252c8d09b2ec537928f34dad269f02f167c8ce5/src/coreclr/System.Private.CoreLib/src/System/Attribute.CoreCLR.cs#L710
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
        var testMethodValidator = new Mock<TestMethodValidator>(reflectHelper.Object, false);
        MockTypeEnumerator = new Mock<TypeEnumerator>(
            typeof(DummyTestClass),
            "DummyAssembly",
            reflectHelper.Object,
            typeValidator.Object,
            testMethodValidator.Object);
    }

    internal Mock<TypeEnumerator> MockTypeEnumerator { get; set; }

    internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
        => MockTypeEnumerator.Object;
}

#endregion
