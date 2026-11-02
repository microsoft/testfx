// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), string.Empty);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();
        tests.Should().HaveCount(0);
    }

    public void EnumerateShouldUseCompleteGeneratedDescriptorsWithoutLegacyMethodValidation()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false);
        MethodInfo method = typeof(DescriptorTestClass).GetMethod(nameof(DescriptorTestClass.PlainTest))!;
        SetGeneratedDescriptorOperations(
            typeof(DescriptorTestClass),
            [method],
            areAllTestMethodsSupported: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DescriptorTestClass), Assembly.GetExecutingAssembly().Location);

        List<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings, useGeneratedDescriptors: true);

        tests.Should().ContainSingle();
        tests[0].TestMethod.MethodInfo.Should().BeSameAs(method);
        tests[0].IsFromGeneratedDescriptor.Should().BeTrue();
        _mockTestMethodValidator.Verify(
            validator => validator.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()),
            Times.Never);
    }

    public void EnumerateShouldFallBackPerMethodWhenGeneratedDescriptorsAreIncomplete()
    {
        _mockTypeValidator.Setup(validator => validator.IsValidTestClass(It.IsAny<Type>(), It.IsAny<List<string>>()))
            .Returns(true);
        _mockTestMethodValidator.Setup(
            validator => validator.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns((MethodInfo method, Type _, ICollection<string> _) => method.Name == nameof(DescriptorTestClass.FallbackTest));
        MethodInfo descriptorMethod = typeof(DescriptorTestClass).GetMethod(nameof(DescriptorTestClass.PlainTest))!;
        SetGeneratedDescriptorOperations(
            typeof(DescriptorTestClass),
            [descriptorMethod],
            areAllTestMethodsSupported: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DescriptorTestClass), Assembly.GetExecutingAssembly().Location);

        List<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings, useGeneratedDescriptors: true);

        tests.Should().HaveCount(2);
        tests.Single(test => test.TestMethod.Name == nameof(DescriptorTestClass.PlainTest))
            .IsFromGeneratedDescriptor.Should().BeTrue();
        tests.Single(test => test.TestMethod.Name == nameof(DescriptorTestClass.FallbackTest))
            .IsFromGeneratedDescriptor.Should().BeFalse();
        _mockTestMethodValidator.Verify(
            validator => validator.IsValidTestMethod(descriptorMethod, It.IsAny<Type>(), It.IsAny<ICollection<string>>()),
            Times.Never);
    }

    public void EnumerateShouldIgnoreGeneratedDescriptorsOutsideNativeMtp()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        MethodInfo descriptorMethod = typeof(DescriptorTestClass).GetMethod(nameof(DescriptorTestClass.PlainTest))!;
        SetGeneratedDescriptorOperations(
            typeof(DescriptorTestClass),
            [descriptorMethod],
            areAllTestMethodsSupported: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DescriptorTestClass), Assembly.GetExecutingAssembly().Location);

        List<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeEmpty();
        tests.Should().OnlyContain(test => !test.IsFromGeneratedDescriptor);
        _mockTestMethodValidator.Verify(
            validator => validator.IsValidTestMethod(descriptorMethod, It.IsAny<Type>(), It.IsAny<ICollection<string>>()),
            Times.Once);
    }

    public void EnumerateShouldSelectPlainAndDataRowDescriptorsWhenComplete()
    {
        Type type = typeof(DescriptorCompleteTestClass);
        MethodInfo[] methods =
        [
            type.GetMethod(nameof(DescriptorCompleteTestClass.PlainTest))!,
            type.GetMethod(nameof(DescriptorCompleteTestClass.DataRowTest))!,
        ];
        SetGeneratedDescriptorOperations(type, methods, areAllTestMethodsSupported: true);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(type, Assembly.GetExecutingAssembly().Location);

        List<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings, useGeneratedDescriptors: true);

        tests.Should().HaveCount(2);
        tests.Should().OnlyContain(test => test.IsFromGeneratedDescriptor);
        tests.Select(test => test.TestMethod.Name)
            .Should().BeEquivalentTo(nameof(DescriptorCompleteTestClass.PlainTest), nameof(DescriptorCompleteTestClass.DataRowTest));
        tests.Select(test => test.TestMethod.Name)
            .Should().NotContain(nameof(DescriptorCompleteTestClass.FallbackOnlyTest));
        _mockTestMethodValidator.Verify(
            validator => validator.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()),
            Times.Never);
    }

    #endregion

    #region GetTests tests

    public void GetTestsShouldReturnDeclaredTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
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
              </MSTestV2>
            </RunSettings>
            """;

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);

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
              </MSTestV2>
            </RunSettings>
            """;

        var mockRunContext = new Mock<IRunContext>();
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        MSTestSettings.PopulateSettings(mockRunContext.Object.RunSettings?.SettingsXml, _mockMessageLogger.Object.ToAdapterMessageLogger(), null);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly.
        // BestTestMethod from DummyRemoteBaseTestClass should be discovered.
        tests.Should().Contain(t => t.TestMethod.Name == "BaseTestMethod");
    }

    public void GetTestsShouldNotReturnHiddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyHidingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummyHidingTestClass declares BaseTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);
    }

    public void GetTestsShouldReturnOverriddenTestMethods()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyOverridingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummyOverridingTestClass inherits BaseTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummyOverridingTestClass overrides DerivedTestMethod directly so it should always be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);
    }

    public void GetTestsShouldNotReturnHiddenTestMethodsFromAnyLevel()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummySecondHidingTestClass), Assembly.GetExecutingAssembly().FullName!);

        ICollection<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings);

        tests.Should().NotBeNull();

        // DummySecondHidingTestClass hides BaseTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "BaseTestMethod").Should().HaveCount(1);

        // DummySecondHidingTestClass hides DerivedTestMethod so it should be discovered.
        tests.Where(t => t.TestMethod.Name == "DerivedTestMethod").Should().HaveCount(1);
    }

    #endregion

    #region GetTestFromMethod tests

    public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.TestMethod.Name.Should().Be("MethodWithVoidReturnType");
        testElement.TestMethod.FullClassName.Should().Be(typeof(DummyTestClass).FullName);
        testElement.TestMethod.AssemblyName.Should().Be("DummyAssemblyName");
    }

    /// <summary>
    /// A class-level <c>[DependsOn(nameof(Setup))]</c> is expanded onto every method of the class, including
    /// <c>Setup</c> itself. That generated self-edge is dropped, because the declaration plainly means "every
    /// *other* test waits for Setup"; keeping it would make the graph report a cycle the user never wrote,
    /// fail <c>Setup</c>, and cascade a skip over the whole class.
    /// </summary>
    public void GetTestFromMethodShouldNotMakeAClassLevelDependencyTargetTargetItself()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClassWithClassLevelDependsOn), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement target = typeEnumerator.GetTestFromMethod(
            typeof(DummyTestClassWithClassLevelDependsOn).GetMethod(nameof(DummyTestClassWithClassLevelDependsOn.Setup))!,
            classDisablesParallelization: false,
            _warnings);

        target.Dependencies.Should().BeNull();

        // Every other method of the class still gets the dependency.
        MSTest.TestAdapter.ObjectModel.UnitTestElement dependent = typeEnumerator.GetTestFromMethod(
            typeof(DummyTestClassWithClassLevelDependsOn).GetMethod(nameof(DummyTestClassWithClassLevelDependsOn.PlaceOrder))!,
            classDisablesParallelization: false,
            _warnings);

        dependent.Dependencies.Should().ContainSingle();
        dependent.Dependencies![0].TargetMethodName.Should().Be(nameof(DummyTestClassWithClassLevelDependsOn.Setup));
    }

    /// <summary>
    /// When the same prerequisite is declared at both class and method scope with different
    /// <c>ProceedOnFailure</c> values, the conservative value must win - matching the rule applied across
    /// distinct prerequisites. Merging permissively would let a class-level opt-out silently override a
    /// method-level default and run a test whose precondition did not hold.
    /// </summary>
    public void GetTestFromMethodShouldMergeDuplicateDependenciesConservatively()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClassWithConflictingDependsOn), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement element = typeEnumerator.GetTestFromMethod(
            typeof(DummyTestClassWithConflictingDependsOn).GetMethod(nameof(DummyTestClassWithConflictingDependsOn.PlaceOrder))!,
            classDisablesParallelization: false,
            _warnings);

        // One edge, because both declarations name the same prerequisite - and it must not proceed on
        // failure, because the method-level declaration did not ask to.
        element.Dependencies.Should().ContainSingle();
        element.Dependencies![0].TargetMethodName.Should().Be(nameof(DummyTestClassWithConflictingDependsOn.Setup));
        element.Dependencies[0].ProceedOnFailure.Should().BeFalse();
    }

    /// <summary>
    /// A test method declared on a base class runs as a test of every derived test class, so the dependency
    /// it declares has to travel with it: the edge resolves against the <em>derived</em> class, where both
    /// the dependent and its prerequisite exist. Dropping it there would silently discard the declared
    /// ordering in every concrete test class - the same silent-loss failure mode as an unmatched edge.
    /// This is unrelated to <c>Inherited = false</c>, which governs override chains (see the test below).
    /// </summary>
    public void GetTestFromMethodShouldResolveAnInheritedDependencyAgainstTheDerivedClass()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClassInheritingADependency), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement element = typeEnumerator.GetTestFromMethod(
            typeof(DummyTestClassInheritingADependency).GetMethod(nameof(DummyTestClassInheritingADependency.PlaceOrder))!,
            classDisablesParallelization: false,
            _warnings);

        element.Dependencies.Should().ContainSingle();

        // A null target class is what keeps the edge inside the derived class: it is resolved against the
        // dependent's own class, so DerivedA.PlaceOrder waits for DerivedA.Setup rather than the base's.
        element.Dependencies![0].TargetClassFullName.Should().BeNull();
        element.Dependencies[0].TargetMethodName.Should().Be(nameof(DummyTestClassBaseDeclaringADependency.Setup));
        element.TestMethod.FullClassName.Should().Be(typeof(DummyTestClassInheritingADependency).FullName);
    }

    /// <summary>
    /// This is what <c>Inherited = false</c> buys: an override that does not re-declare the attribute does
    /// not pick up the base method's dependency. Re-pointing a prerequisite onto a method the author
    /// rewrote is exactly the unintended edge the attribute opts out of.
    /// </summary>
    public void GetTestFromMethodShouldNotCarryADependencyOntoAnOverrideThatDoesNotRedeclareIt()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClassOverridingADependentTest), "DummyAssemblyName");

        MSTest.TestAdapter.ObjectModel.UnitTestElement element = typeEnumerator.GetTestFromMethod(
            typeof(DummyTestClassOverridingADependentTest).GetMethod(nameof(DummyTestClassOverridingADependentTest.PlaceOrder))!,
            classDisablesParallelization: false,
            _warnings);

        element.Dependencies.Should().BeNull();
    }

    public void GetTestFromMethodShouldUseClosedFullClassNameAndOpenManagedTypeNameForGenericTypes()
    {
        Type closedType = typeof(DummyGenericTestClass<int>);
        string assemblyName = Assembly.GetExecutingAssembly().FullName!;
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(closedType, assemblyName);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(closedType.GetMethod(nameof(DummyGenericTestClass<>.GenericTestMethod))!, classDisablesParallelization: false, _warnings);

        testElement.TestMethod.FullClassName.Should().Be(closedType.FullName);
        testElement.TestMethod.ManagedTypeName.Should().Be(typeof(DummyGenericTestClass<>).FullName);

        var testCase = testElement.ToTestCase();
        (testCase.GetPropertyValue(TestCaseExtensions.ManagedTypeProperty) as string).Should().Be(typeof(DummyGenericTestClass<>).FullName);

        testCase.LocalExtensionData = null;
        MSTest.TestAdapter.ObjectModel.UnitTestElement roundTrippedTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);
        // Simulate execution after serialization, where MethodInfo is not available and managed metadata must resolve the method.
        roundTrippedTestElement.TestMethod.MethodInfo = null;

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(assemblyName))
            .Returns(Assembly.GetExecutingAssembly());
        new TypeCache().GetTestMethodInfo(roundTrippedTestElement.TestMethod).Should().NotBeNull();
    }

    public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType")!;

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
    }

    public void GetTestFromMethodShouldSetTestCategory()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestCategoryAttribute("foo"), new TestCategoryAttribute("bar"));
        string[] testCategories = ["foo", "bar"];

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.TestCategory.Should().BeEquivalentTo(testCategories);
    }

    public void GetTestFromMethodShouldSetDoNotParallelize()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DoNotParallelizeAttribute());

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.DoNotParallelize.Should().BeTrue();
    }

    public void GetTestFromMethodShouldFillTraitsWithTestProperties()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(
            methodInfo,
            new TestMethodAttribute(),
            new TestPropertyAttribute("foo", "bar"),
            new TestPropertyAttribute("fooprime", "barprime"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.Traits.Should().HaveCount(2);
        testElement.Traits![0].Name.Should().Be("foo");
        testElement.Traits[0].Value.Should().Be("bar");
        testElement.Traits[1].Name.Should().Be("fooprime");
        testElement.Traits[1].Value.Should().Be("barprime");
    }

    public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(
            methodInfo,
            new TestMethodAttribute(),
            new TestPropertyAttribute("foo", "bar"),
            new TestPropertyAttribute("fooprime", "barprime"),
            new OwnerAttribute("mike"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

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
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestPropertyAttribute("foo", "bar"), new TestPropertyAttribute("fooprime", "barprime"), new PriorityAttribute(1));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

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
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new PriorityAttribute(1));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.Priority.Should().Be(1);
    }

    public void GetTestFromMethodShouldSetDescription()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DescriptionAttribute("Dummy description"));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Traits.Should().NotBeNull();
        testElement.Traits.Should().Contain(t => t.Name == "Description" && t.Value == "Dummy description");
    }

    public void GetTestFromMethodShouldSetWorkItemIds()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new WorkItemAttribute(123), new WorkItemAttribute(345));

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.WorkItemIds.Should().BeEquivalentTo(["123", "345"]);
    }

    public void GetTestFromMethodShouldSetWorkItemIdsToNullIfNotAny()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.WorkItemIds.Should().BeNull();
    }

#if !WINDOWS_UWP && !WIN_UI
    public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), _warnings))
            .Returns((KeyValuePair<string, string>[])null!);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.DeploymentItems.Should().BeNull();
    }

    public void GetTestFromMethodShouldSetDeploymentItems()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!;
        KeyValuePair<string, string>[] deploymentItems = [new KeyValuePair<string, string>("C:\\temp", string.Empty)];

        // Setup mocks
        _testablePlatformServiceProvider.MockTestDeployment.Setup(
            td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), _warnings)).Returns(deploymentItems);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
        testElement.DeploymentItems.Should().NotBeNull();
        testElement.DeploymentItems.Should().BeEquivalentTo(deploymentItems);
    }
#endif

    public void GetTestFromMethodShouldSetDisplayNameToTestMethodNameIfDisplayNameIsNotPresent()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestMethodAttribute());

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
    }

    public void GetTestFromMethodShouldSetDisplayNameFromTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new TestMethodAttribute() { DisplayName = "Test method display name." });

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
    }

    public void GetTestFromMethodShouldSetDisplayNameFromDataTestMethodAttribute()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.MethodWithVoidReturnType))!;
        methodInfo = new MockedMethodInfoWithExtraAttributes(methodInfo, new DataTestMethodAttribute() { DisplayName = "Test method display name." });

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(methodInfo, classDisablesParallelization: false, _warnings);

        testElement.Should().NotBeNull();
    }

    #endregion

    #region private methods

    private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod)
    {
        _mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<List<string>>()))
            .Returns(isValidTestClass);
        _mockTestMethodValidator.Setup(
            tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
    }

    private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName)
        => new(
            type,
            assemblyName,
            _mockReflectHelper.Object,
            _mockTypeValidator.Object,
            _mockTestMethodValidator.Object);

    private void SetGeneratedDescriptorOperations(Type type, MethodInfo[] descriptorMethods, bool areAllTestMethodsSupported)
    {
        Attribute[] typeAttributes = type.GetCustomAttributes(inherit: true).OfType<Attribute>().ToArray();
        var methodAttributes = new Dictionary<MethodInfo, Attribute[]>();
        foreach (MethodInfo method in type.GetMethods())
        {
            methodAttributes[method] = method.GetCustomAttributes(inherit: true).OfType<Attribute>().ToArray();
        }

        var provider = new SourceGeneratedReflectionDataProvider
        {
            TypeAttributes = new Dictionary<Type, Attribute[]> { [type] = typeAttributes },
            TypeMethodAttributes = methodAttributes,
            DescriptorTestMethods = new Dictionary<Type, MethodInfo[]> { [type] = descriptorMethods },
            DescriptorCompleteTypes = areAllTestMethodsSupported
                ? new Dictionary<Type, bool> { [type] = true }
                : [],
        };
        _testablePlatformServiceProvider.SetReflectionOperations(new SourceGeneratedReflectionOperations(provider));
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

[TestClass]
public class DescriptorTestClass
{
    [TestMethod]
    public void PlainTest()
    {
    }

    [TestMethod]
    [TestCategory("fallback")]
    public void FallbackTest()
    {
    }
}

[TestClass]
public class DescriptorCompleteTestClass
{
    [TestMethod]
    public void PlainTest()
    {
    }

    [TestMethod]
    [DataRow(1)]
    public void DataRowTest(int value)
    {
    }

    [TestMethod]
    public void FallbackOnlyTest()
    {
    }
}

[TestClass]
internal class DummyGenericTestClass<T>
{
    [TestMethod]
    public void GenericTestMethod()
    {
    }
}

[TestClass]
[DependsOn(nameof(Setup))]
internal class DummyTestClassWithClassLevelDependsOn
{
    [TestMethod]
    public void Setup()
    {
    }

    [TestMethod]
    public void PlaceOrder()
    {
    }
}

[TestClass]
[DependsOn(nameof(Setup), ProceedOnFailure = true)]
internal class DummyTestClassWithConflictingDependsOn
{
    [TestMethod]
    public void Setup()
    {
    }

    [TestMethod]
    [DependsOn(nameof(Setup))]
    public void PlaceOrder()
    {
    }
}

internal class DummyTestClassBaseDeclaringADependency
{
    [TestMethod]
    public void Setup()
    {
    }

    [TestMethod]
    [DependsOn(nameof(Setup))]
    public virtual void PlaceOrder()
    {
    }
}

[TestClass]
internal class DummyTestClassInheritingADependency : DummyTestClassBaseDeclaringADependency
{
}

[TestClass]
internal class DummyTestClassOverridingADependentTest : DummyTestClassBaseDeclaringADependency
{
    [TestMethod]
    public override void PlaceOrder()
    {
    }
}

#endregion
