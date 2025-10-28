// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public partial class TypeEnumeratorTests : TestContainer
{
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly Mock<TestMethodValidator> _mockTestMethodValidator;
    private readonly Mock<TypeValidator> _mockTypeValidator;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    private readonly List<string> _warnings;

    public TypeEnumeratorTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>
        {
            CallBase = true,
        };

        _mockTypeValidator = new Mock<TypeValidator>(MockBehavior.Default, _mockReflectHelper.Object);
        _mockTestMethodValidator = new Mock<TestMethodValidator>(MockBehavior.Default, _mockReflectHelper.Object, false);
        _warnings = [];
        _mockMessageLogger = new Mock<IMessageLogger>();

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

    #region Enumerate tests

    public void EnumerateShouldReturnNullIfTypeIsNotValid()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(IDummyInterface), string.Empty);
        typeEnumerator.Enumerate(_warnings).Should().BeNull();
    }

    public void EnumerateShouldReturnEmptyCollectionWhenNoValidTestMethodsExist()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), string.Empty);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();
        tests.Should().HaveCount(0);
    }

    #endregion

    #region GetTests tests

    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyDerivedTestClass inherits DummyBaseTestClass from same assembly. BestTestMethod from DummyBaseTestClass should be discovered.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>true</CaptureTraceOutput>
                <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object, _mockMessageLogger.Object, null);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered by default.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>true</CaptureTraceOutput>
                <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object, _mockMessageLogger.Object, null);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly.
        // BestTestMethod from DummyRemoteBaseTestClass should be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = true.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldNotReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>true</CaptureTraceOutput>
                <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>
            """;

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object, _mockMessageLogger.Object, null);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly.
        // BestTestMethod from DummyRemoteBaseTestClass should not be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = false.
        tests.Should().NotContain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);

        // DummyHidingTestClass hides BaseTestMethod so declaring class should not be the base class
        tests.Should().NotContain(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName);
    }

    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);

        // DummyOverridingTestClass inherits BaseTestMethod from DummyHidingTestClass specifically.
        tests.Single(t => t.TestMethod.Name == "BaseTestMethod").TestMethod.DeclaringClassFullName.Should().Be(typeof(DummyHidingTestClass).FullName);

        // DummyOverridingTestClass overrides DerivedTestMethod so is the declaring class.
        tests.Single(t => t.TestMethod.Name == "DerivedTestMethod").TestMethod.DeclaringClassFullName.Should().BeNull();
    }

    public void GetTestsShouldNotReturnHiddenTestMethodsFromAnyLevel()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummySecondHidingTestClass hides BaseTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummySecondHidingTestClass hides DerivedTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        tests.Should().NotContain(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName);

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        tests.Should().NotContain(t => t.TestMethod.DeclaringClassFullName == typeof(DummyHidingTestClass).FullName);

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        tests.Should().NotContain(t => t.TestMethod.DeclaringClassFullName == typeof(DummyOverridingTestClass).FullName);
    }

    #endregion

    #region GetTestFromMethod tests

    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!, _warnings);

        testElement.Should().NotBeNull();
        testElement.TestMethod.Name.Should().Be("MethodWithVoidReturnType");
        testElement.TestMethod.FullClassName.Should().Be(typeof(DummyTestClass).FullName);
        testElement.TestMethod.AssemblyName.Should().Be("DummyAssemblyName");
    }

    public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType")!;

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        string? expectedAsyncTaskName = methodInfo.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.FullName;

        testElement.Should().NotBeNull();
        testElement.AsyncTypeName.Should().Be(expectedAsyncTaskName);
    }

    public void GetTestFromMethodShouldSetTestCategory()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestCategoryAttribute("foo"), new TestCategoryAttribute("bar"));
        string[] testCategories = ["foo", "bar"];

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.TestCategory.Should().BeEquivalentTo(testCategories);
    }

    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DoNotParallelizeAttribute());

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DoNotParallelize.Should().BeTrue();
    }

    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(
            methodInfo,
            new TestMethodAttribute(),
            new TestPropertyAttribute("foo", "bar"),
            new TestPropertyAttribute("fooprime", "barprime"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.Traits.Should().HaveCount(2);
        testElement.Traits![0].Name.Should().Be("foo");
        testElement.Traits[0].Value.Should().Be("bar");
        testElement.Traits[1].Name.Should().Be("fooprime");
        testElement.Traits[1].Value.Should().Be("barprime");
    }

    public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(
            methodInfo,
            new TestMethodAttribute(),
            new TestPropertyAttribute("foo", "bar"),
            new TestPropertyAttribute("fooprime", "barprime"),
            new OwnerAttribute("mike"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.Traits.Should().HaveCount(3);
        testElement.Traits![0].Name.Should().Be("foo");
        testElement.Traits[0].Value.Should().Be("bar");
        testElement.Traits[1].Name.Should().Be("fooprime");
        testElement.Traits[1].Value.Should().Be("barprime");
        testElement.Traits[2].Name.Should().Be("Owner");
        testElement.Traits[2].Value.Should().Be("mike");
    }

    public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestPropertyAttribute("foo", "bar"), new TestPropertyAttribute("fooprime", "barprime"), new PriorityAttribute(1));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.Traits.Should().HaveCount(3);
        testElement.Traits![0].Name.Should().Be("foo");
        testElement.Traits[0].Value.Should().Be("bar");
        testElement.Traits[1].Name.Should().Be("fooprime");
        testElement.Traits[1].Value.Should().Be("barprime");
        testElement.Traits[2].Name.Should().Be("Priority");
        testElement.Traits[2].Value.Should().Be("1");
    }

    public void GetTestFromMethodShouldSetPriority()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new PriorityAttribute(1));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.Priority.Should().Be(1);
    }

    public void GetTestFromMethodShouldSetDescription()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DescriptionAttribute("Dummy description"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Traits.Should().NotBeNull();
        testElement.Traits.Should().Contain(t => t.Name == "Description" && t.Value == "Dummy description");
    }

    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new WorkItemAttribute(123), new WorkItemAttribute(345));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.WorkItemIds.Should().BeEquivalentTo(["123", "345"]);
    }

    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.WorkItemIds.Should().BeNull();
    }

    public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), _warnings))
            .Returns((KeyValuePair<string, string>[])null!);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DeploymentItems.Should().BeNull();
    }

    public void GetTestFromMethodShouldSetDeploymentItems()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        KeyValuePair<string, string>[] deploymentItems = [new KeyValuePair<string, string>("C:\\temp", string.Empty)];

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), _warnings)).Returns(deploymentItems);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DeploymentItems.Should().NotBeNull();
        testElement.DeploymentItems.Should().BeEquivalentTo(deploymentItems);
    }

<<<<<<< HEAD
    public void GetTestFromMethodShouldSetDeclaringAssemblyName()
    {
        const bool isMethodFromSameAssembly = false;

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;

        // Setup mocks
        string otherAssemblyName = "ADifferentAssembly";
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetAssemblyPath(It.IsAny<Assembly>()))
            .Returns(otherAssemblyName);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssembly, _warnings);

        testElement.Should().NotBeNull();
        testElement.TestMethod.DeclaringAssemblyName.Should().Be(otherAssemblyName);
    }

=======
>>>>>>> main
    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestMethodAttribute());

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DisplayName.Should().Be("MethodWithVoidReturnType");
    }

    public void GetTestFromMethodShouldSetDisplayNameFromTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestMethodAttribute() { DisplayName = "Test method display name." });

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DisplayName.Should().Be("Test method display name.");
    }

    public void GetTestFromMethodShouldSetDisplayNameFromDataTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DataTestMethodAttribute() { DisplayName = "Test method display name." });

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, _warnings);

        testElement.Should().NotBeNull();
        testElement.DisplayName.Should().Be("Test method display name.");
    }

    #endregion

    #region private methods

    private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod, bool isMethodFromSameAssembly)
    {
        _mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<List<string>>()))
            .Returns(isValidTestClass);
        _mockTestMethodValidator.Setup(
            tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
        _mockReflectHelper.Setup(
            rh => rh.IsMethodDeclaredInSameAssemblyAsType(It.IsAny<MethodInfo>(), It.IsAny<Type>())).Returns(isMethodFromSameAssembly);
    }

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName)
        => new(
            type,
            assemblyName,
            _mockReflectHelper.Object,
            _mockTypeValidator.Object,
            _mockTestMethodValidator.Object);

    #endregion
}

#region Dummy Test Types

public class DummyBaseTestClass
{
    public void BaseTestMethod()
    {
    }
}

public class DummyDerivedTestClass : DummyBaseTestClass
{
    public void DerivedTestMethod()
    {
    }
}

public class DummyHidingTestClass : DummyBaseTestClass
{
    public new virtual void BaseTestMethod()
    {
    }

    public virtual void DerivedTestMethod()
    {
    }
}

public class DummyOverridingTestClass : DummyHidingTestClass
{
    public override void DerivedTestMethod()
    {
    }
}

public class DummySecondHidingTestClass : DummyOverridingTestClass
{
    public new void BaseTestMethod()
    {
    }

    public new void DerivedTestMethod()
    {
    }
}

#endregion
