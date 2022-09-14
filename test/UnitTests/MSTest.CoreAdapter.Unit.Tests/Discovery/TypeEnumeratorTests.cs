// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Moq;

using TestFramework.ForTestingMSTest;

using TestMethodV2 = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class TypeEnumeratorTests : TestContainer
{
    private Mock<ReflectHelper> _mockReflectHelper;

    private Mock<TestMethodValidator> _mockTestMethodValidator;
    private Mock<TypeValidator> _mockTypeValidator;

    private ICollection<string> _warnings;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

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
        Verify(0 == tests.Count);
    }

    #endregion

    #region GetTests tests

    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.");
    }

    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedTestClass inherits DummyBaseTestClass from same assembly. BestTestMethod from DummyBaseTestClass should be discovered.");
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered by default.");
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
        Verify(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = truem.");
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
        Verify(
            tests.All(t => t.TestMethod.Name != "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should not be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = false.");
    }

    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.");
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.");
        Verify(!
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName),
            "DummyHidingTestClass hides BaseTestMethod so declaring class should not be the base class");
    }

    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.");
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.");
        Verify(
            typeof(DummyHidingTestClass).FullName ==
            tests.Single(t => t.TestMethod.Name == "BaseTestMethod").TestMethod.DeclaringClassFullName,
            "DummyOverridingTestClass inherits BaseTestMethod from DummyHidingTestClass specifically.");
        Verify(
            tests.Single(t => t.TestMethod.Name == "DerivedTestMethod").TestMethod.DeclaringClassFullName is null,
            "DummyOverridingTestClass overrides DerivedTestMethod so is the declaring class.");
    }

    public void GetTestsShouldNotReturnHiddenTestMethodsFromAnyLevel()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummySecondHidingTestClass hides BaseTestMethod so it should be discovered.");
        Verify(
            1 ==
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummySecondHidingTestClass hides DerivedTestMethod so it should be discovered.");
        Verify(!
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
        Verify(!
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyHidingTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
        Verify(!
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyOverridingTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
    }

    #endregion

    #region GetTestFromMethod tests

    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        var testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType"), true, _warnings);

        Verify(testElement is not null);
        Verify("MethodWithVoidReturnType" == testElement.TestMethod.Name);
        Verify(typeof(DummyTestClass).FullName == testElement.TestMethod.FullClassName);
        Verify("DummyAssemblyName" == testElement.TestMethod.AssemblyName);
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
            rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(false);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(false);

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
        Verify(testCategories == testElement.TestCategory);
    }

    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes(It.IsAny<MemberInfo>(), typeof(UTF.DoNotParallelizeAttribute))).Returns(new[] { new UTF.DoNotParallelizeAttribute() });

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DoNotParallelize);
    }

    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };

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
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
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
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
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
        Verify(1 == testElement.Priority);
    }

    public void GetTestFromMethodShouldSetDescription()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.DescriptionAttribute))).Returns(new UTF.DescriptionAttribute("Dummy description"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify("Dummy description" == testElement.Description);
    }

    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes(methodInfo, typeof(UTF.WorkItemAttribute))).Returns(new Attribute[] { new UTF.WorkItemAttribute(123), new UTF.WorkItemAttribute(345) });

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(new string[] { "123", "345" }.SequenceEqual(testElement.WorkItemIds));
    }

    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttributes(methodInfo, typeof(UTF.WorkItemAttribute))).Returns(Array.Empty<Attribute>());

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.WorkItemIds is null);
    }

    public void GetTestFromMethodShouldSetCssIteration()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.CssIterationAttribute))).Returns(new UTF.CssIterationAttribute("234"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify("234" == testElement.CssIteration);
    }

    public void GetTestFromMethodShouldSetCssProjectStructure()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        _mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.CssProjectStructureAttribute))).Returns(new UTF.CssProjectStructureAttribute("ProjectStructure123"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify("ProjectStructure123" == testElement.CssProjectStructure);
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
        Verify(deploymentItems.SequenceEqual(testElement.DeploymentItems.ToArray()));
    }

    public void GetTestFromMethodShouldSetDeclaringAssemblyName()
    {
        const bool isMethodFromSameAssemly = false;

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        string otherAssemblyName = "ADifferentAssembly";
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetAssemblyPath(It.IsAny<Assembly>()))
            .Returns(otherAssemblyName);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssemly, _warnings);

        Verify(testElement is not null);
        Verify(otherAssemblyName == testElement.TestMethod.DeclaringAssemblyName);
    }

    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have  attribute on the method
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(It.IsAny<MemberInfo>(), It.IsAny<Type>())).Returns(new TestMethodV2());

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify("MethodWithVoidReturnType" == testElement.DisplayName);
    }

    public void GetTestFromMethodShouldSetDisplayNameFromAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [TestMethod("Test method display name.")] attribute on the method
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(methodInfo, typeof(TestMethodV2))).Returns(new TestMethodV2("Test method display name."));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify("Test method display name." == testElement.DisplayName);
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

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName)
    {
        return new(
            type,
            assemblyName,
            _mockReflectHelper.Object,
            _mockTypeValidator.Object,
            _mockTestMethodValidator.Object);
    }

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
