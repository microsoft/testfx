// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using MSTest.Adapter.UnitTests.Examples.DifferentAssembly;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class TypeEnumeratorTests : TestContainer
{
    private readonly ReflectHelper _reflectHelper;
    private readonly TestMethodValidator _testMethodValidator;
    private readonly TypeValidator _typeValidator;

    private ICollection<string> _warnings;

    public TypeEnumeratorTests()
    {
        _reflectHelper = new ReflectHelper();
        _typeValidator = new TypeValidator(_reflectHelper);
        _testMethodValidator = new TestMethodValidator(_reflectHelper);
        _warnings = new List<string>();
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
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(EmptyTestClass), string.Empty);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(tests.Count == 0);
    }

    #endregion

    #region GetTests tests

    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestClassWithOneTestMethod), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);
        Verify(tests.Count == 1);
        Verify(tests.First().TestMethod.Name == nameof(TestClassWithOneTestMethod.TestMethod));
    }

    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyDerivedTestClass inherits DummyBaseTestClass from same assembly.
        // BestTestMethod from DummyBaseTestClass should be discovered.
        Verify(tests.Any(t => t.TestMethod.Name == nameof(DummyBaseTestClass.BaseTestMethod)));
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DerivedTestClassWithParentFromDifferentAssembly), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DerivedTestClassWithParentFromDifferentAssembly inherits BaseTestClassFromDifferentAssembly from different assembly.
        // BaseTestMethod from DummyRemoteBaseTestClass should be discovered by default.
        Verify(tests.Any(t => t.TestMethod.Name == nameof(BaseTestClassFromDifferentAssembly.BaseTestMethod)));
    }

    public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingsXml =
        @"<RunSettings>   
                <MSTestV2>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DerivedTestClassWithParentFromDifferentAssembly), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        Verify(tests.Any(t => t.TestMethod.Name == nameof(BaseTestClassFromDifferentAssembly.BaseTestMethod)));
    }

    public void GetTestsShouldNotReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
    {
        string runSettingsXml =
            @"<RunSettings>   
                <MSTestV2>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DerivedTestClassWithParentFromDifferentAssembly), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        Verify(tests.All(t => t.TestMethod.Name != nameof(BaseTestClassFromDifferentAssembly.BaseTestMethod)));
    }

    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == nameof(DummyBaseTestClass.BaseTestMethod)) == 1);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == nameof(DummyHidingTestClass.DerivedTestMethod)) == 1);

        // DummyHidingTestClass hides BaseTestMethod so declaring class should be the derived class
        MSTest.TestAdapter.ObjectModel.UnitTestElement unitTest = tests
            .Single(t => t.TestMethod.Name == nameof(DummyHidingTestClass.DerivedTestMethod));

        // BUG in test?: DeclaringClassFullName is null for the method, this is not change caused by this PR,
        // Declaring* should be only defined for members that come from another assembly.
        Verify(unitTest.TestMethod.FullClassName == typeof(DummyHidingTestClass).FullName);
    }

    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == nameof(DummyOverridingTestClass.BaseTestMethod)) == 1);

        // DummyOverridingTestClass inherits BaseTestMethod from DummyHidingTestClass specifically.
        Verify(typeof(DummyHidingTestClass).FullName
            == tests.Single(t => t.TestMethod.Name == nameof(DummyOverridingTestClass.BaseTestMethod)).TestMethod.DeclaringClassFullName);

        // DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.
        Verify(tests.Count(t => t.TestMethod.Name == nameof(DummyOverridingTestClass.DerivedTestMethod)) == 1);

        // DummyOverridingTestClass overrides DerivedTestMethod so is the declaring class.
        Verify(tests.Single(t => t.TestMethod.Name == nameof(DummyOverridingTestClass.DerivedTestMethod)).TestMethod.DeclaringClassFullName is null);
    }

    public void GetTestsShouldNotReturnMethodsFromParentsWhenChildClassIsHidingTheMethodByUsingNewKeyword()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement> tests = typeEnumerator.Enumerate(out _warnings);

        Verify(tests is not null);

        // We found only the methods on DummySecondHidingTestClass because they hide all the methods
        // from the parents.
        Verify(tests.Count == 2);

        // BUG in test?: DeclaringClassFullName is null for the method, this is not change caused by this PR,
        // Declaring* should be only defined for members that come from another assembly.
        Verify(tests.All(t => t.TestMethod.FullClassName == typeof(DummySecondHidingTestClass).FullName));
    }

    #endregion

    #region GetTestFromMethod tests

    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator
            .GetTestFromMethod(typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType)), true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.TestMethod.Name == nameof(DummyTestClass.MethodWithVoidReturnType));
        Verify(typeof(DummyTestClass).FullName == testElement.TestMethod.FullClassName);
        Verify(testElement.TestMethod.AssemblyName == "DummyAssemblyName");
        Verify(!testElement.TestMethod.IsAsync);
    }

    public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.AsyncMethodWithTaskReturnType));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        string expectedAsyncTaskName =
            (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute)
                .StateMachineType.FullName;

        Verify(testElement is not null);
        Verify(expectedAsyncTaskName == testElement.AsyncTypeName);
    }

    public void GetTestFromMethodShouldSetIgnoredPropertyToFalseIfNotSetOnTestClassAndTestMethod()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsWithoutIgnore), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(TestsWithoutIgnore).GetMethod(nameof(TestsWithoutIgnore.WithoutIgnore));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(!testElement.Ignored);
    }

    public void GetTestFromMethodShouldSetTestCategory()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsWithCategory), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(TestsWithCategory).GetMethod(nameof(TestsWithCategory.WithCategory));
        string[] testCategories = new string[] { "category on class", "category on method" };

        // Setup mocks
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.TestCategory.Length == 2);
        Verify(testElement.TestCategory[0] == "category on method");
        Verify(testElement.TestCategory[1] == "category on class");
    }

    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DoNotParallelizeTests), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(DoNotParallelizeTests).GetMethod(nameof(DoNotParallelizeTests.WithDoNotParallelize));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DoNotParallelize);
    }

    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithTraits));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.Traits.Length == 2);
        Verify(testElement.Traits[0].Name == "foo");
        Verify(testElement.Traits[0].Value == "bar");
        Verify(testElement.Traits[1].Name == "fooprime");
        Verify(testElement.Traits[1].Value == "barprime");
    }

    public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithOwner));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.Traits.Length == 1);
        Verify(testElement.Traits[0].Name == "Owner");
        Verify(testElement.Traits[0].Value == "mike");
    }

    public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithPriority));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.Traits.Length == 1);
        Verify(testElement.Traits[0].Name == "Priority");
        Verify(testElement.Traits[0].Value == "1");
    }

    public void GetTestFromMethodShouldSetPriority()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithPriority));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.Priority == 1);
    }

    public void GetTestFromMethodShouldSetDescription()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithDescription));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.Description == "Dummy description");
    }

    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");

        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithWorkItems));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(new string[] { "123", "345" }.SequenceEqual(testElement.WorkItemIds));
    }

    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithoutWorkItems));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.WorkItemIds is null);
    }

    public void GetTestFromMethodShouldSetCssIteration()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithCssIteration));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.CssIteration == "234");
    }

    public void GetTestFromMethodShouldSetCssProjectStructure()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestsForMetadata), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestsForMetadata).GetMethod(nameof(TestsForMetadata.WithCssProjectStructure));
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement.CssProjectStructure == "ProjectStructure123");
    }

#if !WINDOWS_UWP && !WIN_UI
    public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DeploymentItemTests), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DeploymentItemTests).GetMethod(nameof(DeploymentItemTests.WithoutDeploymentItem));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DeploymentItems is null);
    }

    public void GetTestFromMethodShouldSetDeploymentItems()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DeploymentItemTests), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DeploymentItemTests).GetMethod(nameof(DeploymentItemTests.WithDeploymentItem));

        KeyValuePair<string, string>[] deploymentItems = new[] { new KeyValuePair<string, string>("C:\\temp", string.Empty) };

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DeploymentItems is not null);
        Verify(deploymentItems.SequenceEqual(testElement.DeploymentItems));
    }
#endif

    public void GetTestFromMethodShouldSetDeclaringAssemblyName()
    {
        const bool isMethodFromSameAssembly = false;

        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(BaseTestClassFromDifferentAssembly), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(BaseTestClassFromDifferentAssembly).GetMethod(nameof(BaseTestClassFromDifferentAssembly.BaseTestMethod));

        // Setup mocks
        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssembly, _warnings);

        Verify(testElement is not null);
        // BUG?: Property is called DeclaringAssemblyName, but contains path, not assembly name.
        Verify(typeof(BaseTestClassFromDifferentAssembly).Assembly.Location == testElement.TestMethod.DeclaringAssemblyName);
    }

    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestDisplayName), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestDisplayName).GetMethod(nameof(TestDisplayName.WithoutDisplayName));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == nameof(TestDisplayName.WithoutDisplayName));
    }

    public void GetTestFromMethodShouldSetDisplayNameFromTestMethodAttribute()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestDisplayName), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestDisplayName).GetMethod(nameof(TestDisplayName.WithDisplayName));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == "display name");
    }

    public void GetTestFromMethodShouldSetDisplayNameFromDataTestMethodAttribute()
    {
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(TestDisplayName), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(TestDisplayName).GetMethod(nameof(TestDisplayName.WithDataDisplayName));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, _warnings);

        Verify(testElement is not null);
        Verify(testElement.DisplayName == "data display name");
    }

    #endregion

    #region private methods

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName,
        TestIdGenerationStrategy idGenerationStrategy = TestIdGenerationStrategy.FullyQualified)
        => new(
            type,
            assemblyName,
            _reflectHelper,
            _typeValidator,
            _testMethodValidator,
            idGenerationStrategy);

    #endregion
}
