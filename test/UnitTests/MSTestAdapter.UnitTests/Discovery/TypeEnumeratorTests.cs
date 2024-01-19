// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class TypeEnumeratorTests : TestContainer
{
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly Mock<TestMethodValidator> _mockTestMethodValidator;
    private readonly Mock<TypeValidator> _mockTypeValidator;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private ICollection<string> _warnings;

    public TypeEnumeratorTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _mockTypeValidator = new Mock<TypeValidator>(MockBehavior.Default, _mockReflectHelper.Object);
        _mockTestMethodValidator = new Mock<TestMethodValidator>(MockBehavior.Default, _mockReflectHelper.Object);
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

    #region Enumerate tests

    public void EnumerateShouldReturnNullIfTypeIsNotValid()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(IDummyInterface), string.Empty);
        Verify(typeEnumerator.Enumerate(out _warnings) is null);
    }

    public void EnumerateShouldReturnEmptyCollectionWhenNoValidTestMethodsExist()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), string.Empty);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(tests.Count == 0);
    }

    #endregion

    #region GetTests tests

    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.
        Verify(tests.Any(t => t.TestMethod.Name == "BaseTestMethod"));
    }

    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyDerivedTestClass inherits DummyBaseTestClass from same assembly. BestTestMethod from DummyBaseTestClass should be discovered.
        Verify(tests.Any(t => t.TestMethod.Name == "BaseTestMethod"));
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered by default.
        Verify(tests.Any(t => t.TestMethod.Name == "BaseTestMethod"));
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingxml =
        @"<RunSettings>   
                <MSTestV2>
                  <CaptureTraceOutput>true</CaptureTraceOutput>
                  <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        MSTestSettings.PopulateSettings(mockRunContext.Object);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly.
        // BestTestMethod from DummyRemoteBaseTestClass should be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = truem.
        Verify(tests.Any(t => t.TestMethod.Name == "BaseTestMethod"));
    }

    public void GetTestsShouldNotReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingxml =
            @"<RunSettings>   
                <MSTestV2>
                  <CaptureTraceOutput>true</CaptureTraceOutput>
                  <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        MSTestSettings.PopulateSettings(mockRunContext.Object);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly.
        // BestTestMethod from DummyRemoteBaseTestClass should not be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = false.
        Verify(tests.All(t => t.TestMethod.Name != "BaseTestMethod"));
    }

    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "BaseTestMethod") == 1);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "DerivedTestMethod") == 1);

        // DummyHidingTestClass hides BaseTestMethod so declaring class should not be the base class
        Verify(!tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName));
    }

    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "BaseTestMethod") == 1);

        // DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "DerivedTestMethod") == 1);

        // DummyOverridingTestClass inherits BaseTestMethod from DummyHidingTestClass specifically.
        Verify(typeof(DummyHidingTestClass).FullName
            == tests.Single(t => t.TestMethod.Name == "BaseTestMethod").TestMethod.DeclaringClassFullName);

        // DummyOverridingTestClass overrides DerivedTestMethod so is the declaring class.
        Verify(tests.Single(t => t.TestMethod.Name == "DerivedTestMethod").TestMethod.DeclaringClassFullName is null);
    }

    public void GetTestsShouldNotReturnHiddenTestMethodsFromAnyLevel()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummySecondHidingTestClass hides BaseTestMethod so it should be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "BaseTestMethod") == 1);

        // DummySecondHidingTestClass hides DerivedTestMethod so it should be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == "DerivedTestMethod") == 1);

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        Verify(!tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName));

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        Verify(!tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyHidingTestClass).FullName));

        // DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class.
        Verify(!tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyOverridingTestClass).FullName));
    }

    #endregion

    #region GetTestFromMethod tests

    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        var testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType"), true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.TestMethod.Name == "MethodWithVoidReturnType");
        Verify(typeof(DummyTestClass).FullName == testElement.TestMethod.FullClassName);
        Verify(testElement.TestMethod.AssemblyName == "DummyAssemblyName");
        Verify(!testElement.TestMethod.IsAsync);
    }

    public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType");

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        var expectedAsyncTaskName =
            (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute)
                .StateMachineType.FullName;

        Verify(testElement is not null);
        Verify(expectedAsyncTaskName == testElement.AsyncTypeName);
    }

    public void GetTestFromMethodShouldSetIgnoredPropertyToFalseIfNotSetOnTestClassAndTestMethod()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<IgnoreAttribute>(typeof(DummyTestClass), false)).Returns(false);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<IgnoreAttribute>(methodInfo, false)).Returns(false);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(!testElement.Ignored);
    }

    public void GetTestFromMethodShouldSetTestCategory()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testCategories = new string[] { "foo", "bar" };

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetCategories(methodInfo, typeof(DummyTestClass))).Returns(testCategories);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testCategories.SequenceEqual(testElement.TestCategory));
    }

    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes<DoNotParallelizeAttribute>(It.IsAny<MemberInfo>())).Returns([new DoNotParallelizeAttribute()]);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DoNotParallelize);
    }

    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new("foo", "bar"), new("fooprime", "barprime") };

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testProperties.SequenceEqual(testElement.Traits));
    }

    public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new("foo", "bar"), new("fooprime", "barprime") };
        var ownerTrait = new Trait("owner", "mike");

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
        _mockReflectHelper.Setup(rh => rh.GetTestOwnerAsTraits(methodInfo)).Returns(ownerTrait);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        testProperties.Add(ownerTrait);
        Verify(testProperties.SequenceEqual(testElement.Traits));
    }

    public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new("foo", "bar"), new("fooprime", "barprime") };
        var priorityTrait = new Trait("Priority", "1");

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
        _mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);
        _mockReflectHelper.Setup(rh => rh.GetTestPriorityAsTraits(1)).Returns(priorityTrait);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        testProperties.Add(priorityTrait);
        Verify(testProperties.SequenceEqual(testElement.Traits));
    }

    public void GetTestFromMethodShouldSetPriority()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.Priority == 1);
    }

    public void GetTestFromMethodShouldSetDescription()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute<DescriptionAttribute>(methodInfo)).Returns(new DescriptionAttribute("Dummy description"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.Description == "Dummy description");
    }

    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes<WorkItemAttribute>(methodInfo)).Returns([new(123), new(345)]);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(new string[] { "123", "345" }.SequenceEqual(testElement.WorkItemIds));
    }

    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes<WorkItemAttribute>(methodInfo)).Returns(Array.Empty<WorkItemAttribute>());

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.WorkItemIds is null);
    }

    public void GetTestFromMethodShouldSetCssIteration()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute<CssIterationAttribute>(methodInfo)).Returns(new CssIterationAttribute("234"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.CssIteration == "234");
    }

    public void GetTestFromMethodShouldSetCssProjectStructure()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute<CssProjectStructureAttribute>(methodInfo)).Returns(new CssProjectStructureAttribute("ProjectStructure123"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.CssProjectStructure == "ProjectStructure123");
    }

    public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), _warnings))
            .Returns((KeyValuePair<string, string>[])null);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DeploymentItems is null);
    }

    public void GetTestFromMethodShouldSetDeploymentItems()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var deploymentItems = new[] { new KeyValuePair<string, string>("C:\\temp", string.Empty) };

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), _warnings)).Returns(deploymentItems);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DeploymentItems is not null);
        Verify(deploymentItems.SequenceEqual(testElement.DeploymentItems));
    }

    public void GetTestFromMethodShouldSetDeclaringAssemblyName()
    {
        const bool isMethodFromSameAssembly = false;

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        string otherAssemblyName = "ADifferentAssembly";
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetAssemblyPath(It.IsAny<Assembly>()))
            .Returns(otherAssemblyName);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssembly, _warnings);

        Verify(testElement is not null);
        Verify(otherAssemblyName == testElement.TestMethod.DeclaringAssemblyName);
    }

    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [TestMethod] attribute on the method
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute<TestMethodAttribute>(It.IsAny<MemberInfo>())).Returns(new TestMethodAttribute());

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == "MethodWithVoidReturnType");
    }

    public void GetTestFromMethodShouldSetDisplayNameFromTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [TestMethod("Test method display name.")] attribute on the method
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute<TestMethodAttribute>(methodInfo)).Returns(new TestMethodAttribute("Test method display name."));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == "Test method display name.");
    }

    public void GetTestFromMethodShouldSetDisplayNameFromDataTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [DataTestMethod("Test method display name.")] attribute on the method
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute<TestMethodAttribute>(methodInfo)).Returns(new DataTestMethodAttribute("Test method display name."));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == "Test method display name.");
    }

    #endregion

    #region private methods

    private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod, bool isMethodFromSameAssembly)
    {
        _mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns(isValidTestClass);
        _mockTestMethodValidator.Setup(
            tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
        _mockReflectHelper.Setup(
            rh => rh.IsMethodDeclaredInSameAssemblyAsType(It.IsAny<MethodInfo>(), It.IsAny<Type>())).Returns(isMethodFromSameAssembly);
    }

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName,
        TestIdGenerationStrategy idGenerationStrategy = TestIdGenerationStrategy.FullyQualified)
        => new(
            type,
            assemblyName,
            _mockReflectHelper.Object,
            _mockTypeValidator.Object,
            _mockTestMethodValidator.Object,
            idGenerationStrategy);

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
