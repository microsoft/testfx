// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;

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
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using TestMethodV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TypeEnumeratorTests
{
    private Mock<ReflectHelper> mockReflectHelper;

    private Mock<TestMethodValidator> mockTestMethodValidator;
    private Mock<TypeValidator> mockTypeValidator;

    private ICollection<string> warnings;

    private TestablePlatformServiceProvider testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        mockReflectHelper = new Mock<ReflectHelper>();
        mockTypeValidator = new Mock<TypeValidator>(MockBehavior.Default, mockReflectHelper.Object);
        mockTestMethodValidator = new Mock<TestMethodValidator>(MockBehavior.Default, mockReflectHelper.Object);
        warnings = new List<string>();

        testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    #region Enumerate tests

    [TestMethod]
    public void EnumerateShouldReturnNullIfTypeIsNotValid()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(IDummyInterface), string.Empty);
        Assert.IsNull(typeEnumerator.Enumerate(out warnings));
    }

    [TestMethod]
    public void EnumerateShouldReturnEmptyCollectionWhenNoValidTestMethodsExist()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), string.Empty);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.AreEqual(0, tests.Count);
    }

    #endregion

    #region GetTests tests

    [TestMethod]
    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.IsTrue(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.");
    }

    [TestMethod]
    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.IsTrue(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedTestClass inherits DummyBaseTestClass from same assembly. BestTestMethod from DummyBaseTestClass should be discovered.");
    }

    [TestMethod]
    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.IsTrue(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered by default.");
    }

    [TestMethod]
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

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.IsTrue(
            tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = truem.");
    }

    [TestMethod]
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

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.IsTrue(
            tests.All(t => t.TestMethod.Name != "BaseTestMethod"),
            "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should not be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = false.");
    }

    [TestMethod]
    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.");
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.");
        Assert.IsFalse(
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName),
            "DummyHidingTestClass hides BaseTestMethod so declaring class should not be the base class");
    }

    [TestMethod]
    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.");
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.");
        Assert.AreEqual(
            typeof(DummyHidingTestClass).FullName,
            tests.Single(t => t.TestMethod.Name == "BaseTestMethod").TestMethod.DeclaringClassFullName,
            "DummyOverridingTestClass inherits BaseTestMethod from DummyHidingTestClass specifically.");
        Assert.IsNull(
            tests.Single(t => t.TestMethod.Name == "DerivedTestMethod").TestMethod.DeclaringClassFullName,
            "DummyOverridingTestClass overrides DerivedTestMethod so is the declaring class.");
    }

    [TestMethod]
    public void GetTestsShouldNotReturnHiddenTestMethodsFromAnyLevel()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        var tests = typeEnumerator.Enumerate(out warnings);

        Assert.IsNotNull(tests);
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "BaseTestMethod"),
            "DummySecondHidingTestClass hides BaseTestMethod so it should be discovered.");
        Assert.AreEqual(
            1,
            tests.Count(t => t.TestMethod.Name == "DerivedTestMethod"),
            "DummySecondHidingTestClass hides DerivedTestMethod so it should be discovered.");
        Assert.IsFalse(
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyBaseTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
        Assert.IsFalse(
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyHidingTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
        Assert.IsFalse(
            tests.Any(t => t.TestMethod.DeclaringClassFullName == typeof(DummyOverridingTestClass).FullName),
            "DummySecondHidingTestClass hides all base test methods so declaring class should not be any base class");
    }

    #endregion

    #region GetTestFromMethod tests

    [TestMethod]
    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        var testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType"), true, warnings);

        Assert.IsNotNull(testElement);
        Assert.AreEqual("MethodWithVoidReturnType", testElement.TestMethod.Name);
        Assert.AreEqual(typeof(DummyTestClass).FullName, testElement.TestMethod.FullClassName);
        Assert.AreEqual("DummyAssemblyName", testElement.TestMethod.AssemblyName);
        Assert.IsFalse(testElement.TestMethod.IsAsync);
    }

    [TestMethod]
    public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType");

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        var expectedAsyncTaskName =
            (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute)
                .StateMachineType.FullName;

        Assert.IsNotNull(testElement);
        Assert.AreEqual(expectedAsyncTaskName, testElement.AsyncTypeName);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetIgnoredPropertyToFalseIfNotSetOnTestClassAndTestMethod()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(false);
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(false);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.IsFalse(testElement.Ignored);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetTestCategory()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testCategories = new string[] { "foo", "bar" };

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetCategories(methodInfo, typeof(DummyTestClass))).Returns(testCategories);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        CollectionAssert.AreEqual(testCategories, testElement.TestCategory);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetCustomAttributes(It.IsAny<MemberInfo>(), typeof(UTF.DoNotParallelizeAttribute))).Returns(new[] { new UTF.DoNotParallelizeAttribute() });

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.IsTrue(testElement.DoNotParallelize);
    }

    [TestMethod]
    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        CollectionAssert.AreEqual(testProperties, testElement.Traits);
    }

    [TestMethod]
    public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
        var ownerTrait = new Trait("owner", "mike");

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
        mockReflectHelper.Setup(rh => rh.GetTestOwnerAsTraits(methodInfo)).Returns(ownerTrait);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        testProperties.Add(ownerTrait);
        CollectionAssert.AreEqual(testProperties, testElement.Traits);
    }

    [TestMethod]
    public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
        var priorityTrait = new Trait("Priority", "1");

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
        mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);
        mockReflectHelper.Setup(rh => rh.GetTestPriorityAsTraits(1)).Returns(priorityTrait);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        testProperties.Add(priorityTrait);
        CollectionAssert.AreEqual(testProperties, testElement.Traits);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetPriority()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.AreEqual(1, testElement.Priority);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDescription()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.DescriptionAttribute))).Returns(new UTF.DescriptionAttribute("Dummy description"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.AreEqual("Dummy description", testElement.Description);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        mockReflectHelper.Setup(rh => rh.GetCustomAttributes(methodInfo, typeof(UTF.WorkItemAttribute))).Returns(new Attribute[] { new UTF.WorkItemAttribute(123), new UTF.WorkItemAttribute(345) });

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        CollectionAssert.AreEqual(new string[] { "123", "345" }, testElement.WorkItemIds);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        mockReflectHelper.Setup(rh => rh.GetCustomAttributes(methodInfo, typeof(UTF.WorkItemAttribute))).Returns(new Attribute[0]);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNull(testElement.WorkItemIds);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetCssIteration()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.CssIterationAttribute))).Returns(new UTF.CssIterationAttribute("234"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.AreEqual("234", testElement.CssIteration);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetCssProjectStructure()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        mockReflectHelper.Setup(rh => rh.GetCustomAttribute(methodInfo, typeof(UTF.CssProjectStructureAttribute))).Returns(new UTF.CssProjectStructureAttribute("ProjectStructure123"));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.AreEqual("ProjectStructure123", testElement.CssProjectStructure);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), warnings))
            .Returns((KeyValuePair<string, string>[])null);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.IsNull(testElement.DeploymentItems);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDeploymentItems()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
        var deploymentItems = new[] { new KeyValuePair<string, string>("C:\\temp", string.Empty) };

        // Setup mocks
        testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), warnings)).Returns(deploymentItems);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.IsNotNull(testElement.DeploymentItems);
        CollectionAssert.AreEqual(deploymentItems, testElement.DeploymentItems.ToArray());
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDeclaringAssemblyName()
    {
        const bool isMethodFromSameAssemly = false;

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

        // Setup mocks
        string otherAssemblyName = "ADifferentAssembly";
        testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetAssemblyPath(It.IsAny<Assembly>()))
            .Returns(otherAssemblyName);

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssemly, warnings);

        Assert.IsNotNull(testElement);
        Assert.AreEqual(otherAssemblyName, testElement.TestMethod.DeclaringAssemblyName);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [TestMethod] attribute on the method
        mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(It.IsAny<MemberInfo>(), It.IsAny<Type>())).Returns(new TestMethodV2());

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.AreEqual("MethodWithVoidReturnType", testElement.DisplayName);
    }

    [TestMethod]
    public void GetTestFromMethodShouldSetDisplayNameFromAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        var methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType));

        // Setup mocks to behave like we have [TestMethod("Test method display name.")] attribute on the method
        mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(methodInfo, typeof(TestMethodV2))).Returns(new TestMethodV2("Test method display name."));

        var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, warnings);

        Assert.IsNotNull(testElement);
        Assert.AreEqual("Test method display name.", testElement.DisplayName);
    }

    #endregion

    #region private methods

    private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod, bool isMethodFromSameAssembly)
    {
        mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns(isValidTestClass);
        mockTestMethodValidator.Setup(
            tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
        mockReflectHelper.Setup(
            rh => rh.IsMethodDeclaredInSameAssemblyAsType(It.IsAny<MethodInfo>(), It.IsAny<Type>())).Returns(isMethodFromSameAssembly);
    }

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName)
    {
        return new TypeEnumerator(
            type,
            assemblyName,
            mockReflectHelper.Object,
            mockTypeValidator.Object,
            mockTestMethodValidator.Object);
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
