// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using static Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheTests : TestContainer
{
    private readonly TypeCache _typeCache;

    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);
        _mockMessageLogger = new Mock<IMessageLogger>();

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        ReflectHelper.Instance.ClearCache();

        SetupMocks();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    #region GetTestMethodInfo tests

    private static TestContextImplementation CreateTestContextImplementationForMethod(TestMethod testMethod)
        => new(testMethod, null, new Dictionary<string, object?>(), null, null);

    private static TestMethod CreateTestMethod(string methodName, string className, string assemblyName, string? displayName)
        => new(className, methodName, null, methodName, className, assemblyName, displayName, null);

    public void GetTestMethodInfoShouldThrowIfTestMethodIsNull()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);

        TestContextImplementation context = CreateTestContextImplementationForMethod(testMethod);
        Action action = () => _typeCache.GetTestMethodInfo(null!, context);
        action.Should().Throw<ArgumentNullException>();
    }

    public void GetTestMethodInfoShouldThrowIfTestContextIsNull()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);
        new Action(() => _typeCache.GetTestMethodInfo(testMethod, null!)).Should().Throw<ArgumentNullException>();
    }

    public void GetTestMethodInfoShouldReturnNullIfClassInfoForTheMethodIsNull()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);

        _typeCache.GetTestMethodInfo(
            testMethod,
            CreateTestContextImplementationForMethod(testMethod))
            .Should().BeNull();
    }

    public void GetTestMethodInfoShouldReturnNullIfLoadingTypeThrowsTypeLoadException()
    {
        TestMethod testMethod = CreateTestMethod("M", "System.TypedReference[]", "A", displayName: null);

        _typeCache.GetTestMethodInfo(
            testMethod,
            CreateTestContextImplementationForMethod(testMethod))
            .Should().BeNull();
    }

    public void GetTestMethodInfoShouldThrowIfLoadingTypeThrowsException()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
            .Throws(new Exception("Load failure"));

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TypeInspectionException exception = new Action(Action).Should().Throw<TypeInspectionException>().Which;

        exception.Message.StartsWith("Unable to get type C. Error: System.Exception: Load failure", StringComparison.Ordinal).Should().BeTrue();
    }

    public void GetTestMethodInfoShouldThrowIfTypeDoesNotHaveADefaultConstructor()
    {
        string className = typeof(DummyTestClassWithNoDefaultConstructor).FullName!;
        TestMethod testMethod = CreateTestMethod("M", className, "A", displayName: null);

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TypeInspectionException exception = new Action(Action).Should().Throw<TypeInspectionException>().Which;
        exception.Message.StartsWith("Cannot find a valid constructor for test class 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TypeCacheTests+DummyTestClassWithNoDefaultConstructor'. Valid constructors are 'public' and either parameterless or with one parameter of type 'TestContext'.", StringComparison.Ordinal).Should().BeTrue();
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasATypeMismatch()
    {
        string className = typeof(DummyTestClassWithIncorrectTestContextType).FullName!;
        TestMethod testMethod = CreateTestMethod("M", className, "A", displayName: null);

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TypeInspectionException exception = new Action(Action).Should().Throw<TypeInspectionException>().Which;
        exception.Message.StartsWith($"The {className}.TestContext has incorrect type.", StringComparison.Ordinal).Should().BeTrue();
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasMultipleAmbiguousTestContextProperties()
    {
        string className = typeof(DummyTestClassWithMultipleTestContextProperties).FullName!;
        TestMethod testMethod = CreateTestMethod("M", className, "A", displayName: null);

        Action action = () =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        action.Should().Throw<TypeInspectionException>()
            .And.Message.Should().StartWith($"Unable to find property {className}.TestContext. Error:Ambiguous match found");
    }

    public void GetTestMethodInfoShouldSetTestContextIfPresent()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                                    testMethod,
                                    CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo.Should().NotBeNull();
        testMethodInfo.Parent.TestContextProperty.Should().NotBeNull();
    }

    public void GetTestMethodInfoShouldSetTestContextToNullIfNotPresent()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        MethodInfo methodInfo = type.GetMethod("TestInit")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                                testMethod,
                                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo.Should().NotBeNull();
        testMethodInfo.Parent.TestContextProperty.Should().BeNull();
    }

    #region Assembly Info Creation tests.

    public void GetTestMethodInfoShouldAddAssemblyInfoToTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.AssemblyInfoCache.Count.Should().Be(1);
    }

    public void GetTestMethodInfoShouldNotThrowIfWeFailToDiscoverTypeFromAnAssembly()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(It.IsAny<Type>())).Throws(new Exception());

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(typeof(DummyTestClassWithTestMethods))).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.AssemblyInfoCache.Count.Should().Be(1);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        TestMethod testMethod = CreateTestMethod("TestInit", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.AssemblyInfoCache.Count.Should().Be(1);
        _typeCache.AssemblyInfoCache.First().AssemblyInitializeMethod.Should().BeSameAs(type.GetMethod("AssemblyInit")!);
    }

    public void GetTestMethodInfoShouldCacheAssemblyCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestCleanup", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.AssemblyInfoCache.Count.Should().Be(1);
        _typeCache.AssemblyInfoCache.First().AssemblyCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitAndCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestInitOrCleanup", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.AssemblyInfoCache.Count.Should().Be(1);
        _typeCache.AssemblyInfoCache.First().AssemblyCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
        _typeCache.AssemblyInfoCache.First().AssemblyInitializeMethod.Should().BeSameAs(type.GetMethod("AssemblyInit")!);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyInitHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        TestMethod testMethod = CreateTestMethod("M", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        MethodInfo methodInfo = type.GetMethod("AssemblyInit")!;
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType!.FullName!,
                methodInfo.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        TestMethod testMethod = CreateTestMethod("M", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        MethodInfo methodInfo = type.GetMethod("AssemblyCleanup")!;
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType!.FullName!,
                methodInfo.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInfoInstanceAndReuseTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _mockReflectHelper.Verify(rh => rh.IsAttributeDefined<TestClassAttribute>(type), Times.Once);
        _typeCache.AssemblyInfoCache.Should().HaveCount(1);
    }

    #endregion

    #region ClassInfo Creation tests.

    public void GetTestMethodInfoShouldAddClassInfoToTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.TestInitializeMethod.Should().BeNull();
        _typeCache.ClassInfoCache.First()!.TestCleanupMethod.Should().BeNull();
    }

    public void GetTestMethodInfoShouldCacheClassInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        TestMethod testMethod = CreateTestMethod("TestInit", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.BaseClassInitMethods.Count.Should().Be(0);
        _typeCache.ClassInfoCache.First()!.ClassInitializeMethod.Should().BeSameAs(type.GetMethod("AssemblyInit")!);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitializeAttributes()
    {
        Type type = typeof(DummyDerivedTestClassWithInitializeMethods);
        Type baseType = typeof(DummyTestClassWithInitializeMethods);

        TestMethod testMethod = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
           rh => rh.IsAttributeDefined<ClassInitializeAttribute>(baseType.GetMethod("AssemblyInit")!)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetFirstAttributeOrDefault<ClassInitializeAttribute>(baseType.GetMethod("AssemblyInit")!))
                    .Returns(new ClassInitializeAttribute(InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
           rh => rh.IsAttributeDefined<ClassInitializeAttribute>(type.GetMethod("ClassInit")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
            testMethod,
            CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.BaseClassCleanupMethods.Count.Should().Be(0, "No base class cleanup");
        _typeCache.ClassInfoCache.First()!.BaseClassInitMethods[0].Should().BeSameAs(baseType.GetMethod("AssemblyInit")!);
    }

    public void GetTestMethodInfoShouldCacheClassCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestCleanup", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.ClassCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
    }

    public void GetTestMethodInfoShouldCacheBaseClassCleanupAttributes()
    {
        Type type = typeof(DummyDerivedTestClassWithCleanupMethods);

        Type baseType = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);
        _mockReflectHelper.Setup(
          rh => rh.IsAttributeDefined<ClassCleanupAttribute>(baseType.GetMethod("AssemblyCleanup")!)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetFirstAttributeOrDefault<ClassCleanupAttribute>(baseType.GetMethod("AssemblyCleanup")!))
                   .Returns(new ClassCleanupAttribute(InheritanceBehavior.BeforeEachDerivedClass));

        _typeCache.GetTestMethodInfo(
            testMethod,
            CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.BaseClassInitMethods.Count.Should().Be(0, "No base class init");
        _typeCache.ClassInfoCache.First()!.BaseClassCleanupMethods[0].Should().BeSameAs(baseType.GetMethod("AssemblyCleanup")!);
    }

    public void GetTestMethodInfoShouldCacheClassInitAndCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestInitOrCleanup", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.ClassInitializeMethod.Should().BeSameAs(type.GetMethod("AssemblyInit")!);
        _typeCache.ClassInfoCache.First()!.ClassCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitAndCleanupAttributes()
    {
        Type baseType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestInitOrCleanup", type.FullName!, "A", displayName: null);

        MethodInfo baseInitializeMethod = baseType.GetMethod("ClassInit")!;
        MethodInfo baseCleanupMethod = baseType.GetMethod("ClassCleanup")!;

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
          rh => rh.IsAttributeDefined<ClassInitializeAttribute>(baseInitializeMethod)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetFirstAttributeOrDefault<ClassInitializeAttribute>(baseInitializeMethod))
                   .Returns(new ClassInitializeAttribute(InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(baseCleanupMethod)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetFirstAttributeOrDefault<ClassCleanupAttribute>(baseCleanupMethod))
                    .Returns(new ClassCleanupAttribute(InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Count.Should().Be(1);
        _typeCache.ClassInfoCache.First()!.ClassInitializeMethod.Should().BeSameAs(type.GetMethod("AssemblyInit")!);
        _typeCache.ClassInfoCache.First()!.ClassCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);

        _typeCache.ClassInfoCache.First()!.BaseClassInitMethods[0].Should().BeSameAs(baseInitializeMethod);
        _typeCache.ClassInfoCache.First()!.BaseClassCleanupMethods[0].Should().BeSameAs(baseCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheParentAndGrandparentClassInitAndCleanupAttributes()
    {
        Type grandparentType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        Type parentType = typeof(DummyChildBaseTestClassWithInitAndCleanupMethods);
        Type type = typeof(DummyTestClassWithParentAndGrandparentInitAndCleanupMethods);

        MethodInfo grandparentInitMethod = grandparentType.GetMethod("ClassInit")!;
        MethodInfo grandparentCleanupMethod = grandparentType.GetMethod("ClassCleanup")!;
        MethodInfo parentInitMethod = parentType.GetMethod("ChildClassInit")!;
        MethodInfo parentCleanupMethod = parentType.GetMethod("ChildClassCleanup")!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(type))
            .Returns(true);

        // Setup grandparent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<ClassInitializeAttribute>(grandparentInitMethod))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstAttributeOrDefault<ClassInitializeAttribute>(grandparentInitMethod))
            .Returns(new ClassInitializeAttribute(InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<ClassCleanupAttribute>(grandparentCleanupMethod))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstAttributeOrDefault<ClassCleanupAttribute>(grandparentCleanupMethod))
            .Returns(new ClassCleanupAttribute(InheritanceBehavior.BeforeEachDerivedClass));

        // Setup parent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<ClassInitializeAttribute>(parentInitMethod))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstAttributeOrDefault<ClassInitializeAttribute>(parentInitMethod))
            .Returns(new ClassInitializeAttribute(InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<ClassCleanupAttribute>(parentCleanupMethod))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstAttributeOrDefault<ClassCleanupAttribute>(parentCleanupMethod))
            .Returns(new ClassCleanupAttribute(InheritanceBehavior.BeforeEachDerivedClass));

        TestMethod testMethod = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);
        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TestClassInfo? classInfo = _typeCache.ClassInfoCache.FirstOrDefault();
        _typeCache.ClassInfoCache.Count.Should().Be(1);
        classInfo!.ClassInitializeMethod.Should().BeNull();
        classInfo.ClassCleanupMethod.Should().BeNull();

        classInfo.BaseClassCleanupMethods.Count.Should().Be(2);
        classInfo.BaseClassCleanupMethods[0].Should().BeSameAs(parentCleanupMethod);
        classInfo.BaseClassCleanupMethods[1].Should().BeSameAs(grandparentCleanupMethod);

        classInfo.BaseClassInitMethods.Count.Should().Be(2);
        classInfo.BaseClassInitMethods[0].Should().BeSameAs(parentInitMethod);
        classInfo.BaseClassInitMethods[1].Should().BeSameAs(grandparentInitMethod);
    }

    public void GetTestMethodInfoShouldThrowIfClassInitHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        TestMethod testMethod = CreateTestMethod("M", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassInitializeAttribute>(type.GetMethod("AssemblyInit")!)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        MethodInfo methodInfo = type.GetMethod("AssemblyInit")!;
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType!.FullName!,
                methodInfo.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldThrowIfClassCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        TestMethod testMethod = CreateTestMethod("M", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        MethodInfo methodInfo = type.GetMethod("AssemblyCleanup")!;
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType!.FullName!,
                methodInfo.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        TestMethod testMethod = CreateTestMethod("TestInit", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestInitializeAttribute>(type.GetMethod("TestInit")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Should().HaveCount(1);
        type.GetMethod("TestInit").Should().BeSameAs(_typeCache.ClassInfoCache.First()!.TestInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestCleanup", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestCleanupAttribute>(type.GetMethod("TestCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Should().HaveCount(1);
        type.GetMethod("TestCleanup").Should().BeSameAs(_typeCache.ClassInfoCache.First()!.TestCleanupMethod);
    }

    public void GetTestMethodInfoShouldThrowIfTestInitOrCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        TestMethod testMethod = CreateTestMethod("M", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestInitializeAttribute>(type.GetMethod("TestInit")!)).Returns(true);

        Action action = () =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TypeInspectionException exception = action.Should().Throw<TypeInspectionException>().Which;

        MethodInfo methodInfo = type.GetMethod("TestInit")!;
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType!.FullName!,
                methodInfo.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttributeDefinedInBaseClass()
    {
        Type type = typeof(DummyDerivedTestClassWithInitializeMethods);
        Type baseType = typeof(DummyTestClassWithInitializeMethods);
        TestMethod testMethod = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestInitializeAttribute>(baseType.GetMethod("TestInit")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Should().HaveCount(1);
        baseType.GetMethod("TestInit").Should().BeSameAs(_typeCache.ClassInfoCache.First()!.BaseTestInitializeMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttributeDefinedInBaseClass()
    {
        Type type = typeof(DummyDerivedTestClassWithCleanupMethods);
        Type baseType = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestCleanupAttribute>(baseType.GetMethod("TestCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.ClassInfoCache.Should().HaveCount(1);
        baseType.GetMethod("TestCleanup").Should().BeSameAs(_typeCache.ClassInfoCache.First()!.BaseTestCleanupMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheClassInfoInstanceAndReuseFromCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        _testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.LoadAssembly(It.IsAny<string>()), Times.Once);
        _typeCache.ClassInfoCache.Should().HaveCount(1);
    }

    #endregion

    #region Method resolution tests

    public void GetTestMethodInfoShouldThrowIfTestMethodHasIncorrectSignatureOrCannotBeFound()
    {
        Type type = typeof(DummyTestClassWithIncorrectTestMethodSignatures);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfo()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.MethodInfo.Should().BeSameAs(methodInfo);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(0);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoWithTimeout()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeout")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TimeoutAttribute>(methodInfo))
            .Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo)).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.MethodInfo.Should().BeSameAs(methodInfo);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(10);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
    }

    public void GetTestMethodInfoShouldThrowWhenTimeoutIsNegative()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNegativeTimeout")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TimeoutAttribute>(methodInfo))
            .Returns(true);
        _mockReflectHelper.Setup(ReflectHelper => ReflectHelper.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo))
            .CallBase();

        void A() => _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        Exception exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be an integer value greater than 0.",
                testMethod.FullClassName,
                testMethod.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldThrowWhenTimeoutIsZero()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeoutOfZero")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TimeoutAttribute>(methodInfo))
            .Returns(true);
        _mockReflectHelper.Setup(ReflectHelper => ReflectHelper.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo))
            .CallBase();

        void A() => _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        TypeInspectionException exception = new Action(A).Should().Throw<TypeInspectionException>().Which;

        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be an integer value greater than 0.",
                testMethod.FullClassName,
                testMethod.Name);

        exception.Message.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoWhenTimeoutAttributeNotSetShouldReturnTestMethodInfoWithGlobalTimeout()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <TestTimeout>4000</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!);

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.TimeoutInfo.Timeout.Should().Be(4000);
    }

    public void GetTestMethodInfoWhenTimeoutAttributeSetShouldReturnTimeoutBasedOnAttributeEvenIfGlobalTimeoutSet()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <TestTimeout>4000</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!);

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeout")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TimeoutAttribute>(methodInfo))
           .Returns(true);
        _mockReflectHelper.Setup(ReflectHelper => ReflectHelper.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo))
            .CallBase();

        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.TimeoutInfo.Timeout.Should().Be(10);
    }

    public void GetTestMethodInfoForInvalidGlobalTimeoutShouldReturnTestMethodInfoWithTimeoutZero()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <TestTimeout>30.5</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!);

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.TimeoutInfo.Timeout.Should().Be(0);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForMethodsAdornedWithADerivedTestMethodAttribute()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithDerivedTestMethodAttribute")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.MethodInfo.Should().BeSameAs(methodInfo);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(0);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
        testMethodInfo.Executor.Should().BeOfType<DerivedTestMethodAttribute>();
    }

    public void GetTestMethodInfoShouldSetTestContextWithCustomProperty()
    {
        // Not using _typeCache here which uses a mocked ReflectHelper which doesn't work well with this test.
        // Setting up the mock feels unnecessary when the original production implementation can work just fine.
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithCustomProperty")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        typeCache.GetTestMethodInfo(testMethod, testContext);
        KeyValuePair<string, object?> customProperty = testContext.Properties.FirstOrDefault(p => p.Key.Equals("WhoAmI", StringComparison.Ordinal));

        customProperty.Should().NotBeNull();
        (customProperty.Value as string).Should().Be("Me");
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyHasSameNameAsPredefinedProperties()
    {
        // Not using _typeCache here which uses a mocked ReflectHelper which doesn't work well with this test.
        // Setting up the mock feels unnecessary when the original production implementation can work just fine.
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTestCategoryAsCustomProperty")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
            methodInfo.DeclaringType!.FullName!,
            methodInfo.Name,
            "TestCategory");
        testMethodInfo.NotRunnableReason.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomOwnerPropertyIsDefined()
    {
        // Test that [TestProperty("Owner", "value")] is still blocked
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithOwnerAsCustomProperty")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
            methodInfo.DeclaringType!.FullName!,
            methodInfo.Name,
            "Owner");
        testMethodInfo.NotRunnableReason.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPriorityPropertyIsDefined()
    {
        // Test that [TestProperty("Priority", "value")] is still blocked
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithPriorityAsCustomProperty")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
            methodInfo.DeclaringType!.FullName!,
            methodInfo.Name,
            "Priority");
        testMethodInfo.NotRunnableReason.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldAllowActualOwnerAttribute()
    {
        // Test that the actual OwnerAttribute is allowed
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithActualOwnerAttribute")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        // Owner should be allowed - no NotRunnableReason should be set
        string.IsNullOrEmpty(testMethodInfo.NotRunnableReason).Should().BeTrue();
        // The Owner property should be added to the test context
        testContext.TryGetPropertyValue("Owner", out object? ownerValue).Should().BeTrue();
        ownerValue?.ToString().Should().Be("TestOwner");
    }

    public void GetTestMethodInfoShouldAllowActualPriorityAttribute()
    {
        // Test that the actual PriorityAttribute is allowed
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithActualPriorityAttribute")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        // Priority should be allowed - no NotRunnableReason should be set
        string.IsNullOrEmpty(testMethodInfo.NotRunnableReason).Should().BeTrue();
        // The Priority property should be added to the test context
        testContext.TryGetPropertyValue("Priority", out object? priorityValue).Should().BeTrue();
        priorityValue?.ToString().Should().Be("1");
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsEmpty()
    {
        // Not using _typeCache here which uses a mocked ReflectHelper which doesn't work well with this test.
        // Setting up the mock feels unnecessary when the original production implementation can work just fine.
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithEmptyCustomPropertyName")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType!.FullName!,
            methodInfo.Name);
        testMethodInfo.NotRunnableReason.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsNull()
    {
        // Not using _typeCache here which uses a mocked ReflectHelper which doesn't work well with this test.
        // Setting up the mock feels unnecessary when the original production implementation can work just fine.
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType!.FullName!,
            methodInfo.Name);
        testMethodInfo.NotRunnableReason.Should().Be(expectedMessage);
    }

    public void GetTestMethodInfoShouldNotAddDuplicateTestPropertiesToTestContext()
    {
        // Not using _typeCache here which uses a mocked ReflectHelper which doesn't work well with this test.
        // Setting up the mock feels unnecessary when the original production implementation can work just fine.
        var typeCache = new TypeCache(new ReflectHelper());
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithDuplicateCustomPropertyNames")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        TestContextImplementation testContext = CreateTestContextImplementationForMethod(testMethod);

        TestMethodInfo? testMethodInfo = typeCache.GetTestMethodInfo(testMethod, testContext);

        testMethodInfo.Should().NotBeNull();

        // Verify that the first value gets set.
        testContext.Properties.TryGetValue("WhoAmI", out object? value).Should().BeTrue();
        value.Should().Be("Me");
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedTestClasses()
    {
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = type.GetRuntimeMethod("DummyTestMethod", [])!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.MethodInfo.Should().BeSameAs(methodInfo);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(0);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedClassMethodOverloadByDefault()
    {
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = type.GetRuntimeMethod("OverloadedTestMethod", [])!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        testMethodInfo!.MethodInfo.Should().BeSameAs(methodInfo);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(0);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDeclaringTypeMethodOverload()
    {
        Type baseType = typeof(BaseTestClass);
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = baseType.GetRuntimeMethod("OverloadedTestMethod", [])!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, baseType.FullName!, "A", displayName: null);
        testMethod.DeclaringClassFullName = baseType.FullName!;

        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>())).CallBase();
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        // The two MethodInfo instances will have different ReflectedType properties,
        // so cannot be compared directly. Use MethodHandle to verify it's the same.
        testMethodInfo!.MethodInfo.MethodHandle.Should().Be(methodInfo.MethodHandle);
        testMethodInfo.TimeoutInfo.Timeout.Should().Be(0);
        testMethodInfo.Parent.Should().Be(_typeCache.ClassInfoCache.First());
        testMethodInfo.Executor.Should().NotBeNull();
    }

    #endregion

    #endregion

    #region ClassInfoListWithExecutableCleanupMethods tests

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheIsEmpty()
    {
        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        cleanupMethods.Any().Should().BeFalse();
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheDoesNotHaveTestCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("TestCleanup")!)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        cleanupMethods.Any().Should().BeFalse();
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnClassInfosWithExecutableCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        cleanupMethods.Count().Should().Be(1);
        cleanupMethods.First().ClassCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
    }

    #endregion

    #region AssemblyInfoListWithExecutableCleanupMethods tests

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheIsEmpty()
    {
        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        cleanupMethods.Any().Should().BeFalse();
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheDoesNotHaveTestCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        cleanupMethods.Any().Should().BeFalse();
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnAssemblyInfoWithExecutableCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup")!)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                CreateTestContextImplementationForMethod(testMethod));

        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        cleanupMethods.Count().Should().Be(1);
        cleanupMethods.First().AssemblyCleanupMethod.Should().BeSameAs(type.GetMethod("AssemblyCleanup")!);
    }

    #endregion

    private void SetupMocks() => _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
            .Returns(Assembly.GetExecutingAssembly());

    #region dummy implementations

    [DummyTestClass]
    internal class DummyTestClassWithTestMethods
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void TestMethod()
        {
        }

        [DerivedTestMethod]
        public void TestMethodWithDerivedTestMethodAttribute()
        {
        }

        [TestMethod]
        [Timeout(10)]
        public void TestMethodWithTimeout()
        {
        }

        [TestMethod]
        [Timeout(-10)]
        public void TestMethodWithNegativeTimeout()
        {
        }

        [TestMethod]
        [Timeout(0)]
        public void TestMethodWithTimeoutOfZero()
        {
        }

        [TestMethod]
        [TestProperty("WhoAmI", "Me")]
        public void TestMethodWithCustomProperty()
        {
        }

        [TestMethod]
        [TestProperty("Owner", "You")]
        public void TestMethodWithOwnerAsCustomProperty()
        {
        }

        [TestMethod]
        [TestProperty("TestCategory", "SomeCategory")]
        public void TestMethodWithTestCategoryAsCustomProperty()
        {
        }

        [TestMethod]
        [Owner("TestOwner")]
        public void TestMethodWithActualOwnerAttribute()
        {
        }

        [TestMethod]
        [Priority(1)]
        public void TestMethodWithActualPriorityAttribute()
        {
        }

        [TestMethod]
        [TestProperty("Priority", "2")]
        public void TestMethodWithPriorityAsCustomProperty()
        {
        }

        [TestMethod]
        [TestProperty("", "You")]
        public void TestMethodWithEmptyCustomPropertyName()
        {
        }

        [TestMethod]
        [TestProperty(null!, "You")]
        public void TestMethodWithNullCustomPropertyName()
        {
        }

        [TestMethod]
        [TestProperty("WhoAmI", "Me")]
        [TestProperty("WhoAmI", "Me2")]
        public void TestMethodWithDuplicateCustomPropertyNames()
        {
        }
    }

    [DummyTestClass]
    [Ignore]
    internal class DummyTestClassWithIgnoreClass
    {
        [TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    [Ignore("IgnoreTestClassMessage")]
    internal class DummyTestClassWithIgnoreClassWithMessage
    {
        [TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    internal class DummyTestClassWithIgnoreTest
    {
        [TestMethod]
        [Ignore]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    internal class DummyTestClassWithIgnoreTestWithMessage
    {
        [TestMethod]
        [Ignore("IgnoreTestMessage")]
        public void TestMethod()
        {
        }
    }

    [Ignore("IgnoreTestClassMessage")]
    [DummyTestClass]
    internal class DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage;

    [Ignore]
    [DummyTestClass]
    internal class DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage;

    [DummyTestClass]
    internal class DerivedTestClass : BaseTestClass
    {
        [TestMethod]
        public new void OverloadedTestMethod()
        {
        }
    }

    internal class BaseTestClass
    {
        [TestMethod]
        public void DummyTestMethod()
        {
        }

        [TestMethod]
        public void OverloadedTestMethod()
        {
        }
    }

    private class DummyTestClassWithNoDefaultConstructor
    {
#pragma warning disable IDE0051 // Remove unused private members
        private DummyTestClassWithNoDefaultConstructor(int a)
#pragma warning restore IDE0051 // Remove unused private members
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithIncorrectTestContextType
    {
        // This is TP.TF type.
        public virtual int TestContext { get; set; }
    }

    private class DummyTestClassWithTestContextProperty : DummyTestClassWithIncorrectTestContextType
    {
        public new string TestContext { get; set; } = null!;
    }

    [DummyTestClass]
    private class DummyTestClassWithMultipleTestContextProperties : DummyTestClassWithTestContextProperty;

    [DummyTestClass]
    private class DummyTestClassWithInitializeMethods
    {
        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static void AssemblyInit(TestContext tc)
        {
        }

        public void TestInit()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public static void AssemblyCleanup()
        {
        }

        public void TestCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyDerivedTestClassWithInitializeMethods : DummyTestClassWithInitializeMethods
    {
        public static void ClassInit(TestContext tc)
        {
        }

        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyDerivedTestClassWithCleanupMethods : DummyTestClassWithCleanupMethods
    {
        public static void ClassCleanup()
        {
        }

        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyBaseTestClassWithInitAndCleanupMethods
    {
        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static void ClassInit(TestContext tc)
        {
        }

        public static void ClassCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithInitAndCleanupMethods : DummyBaseTestClassWithInitAndCleanupMethods
    {
        public static void AssemblyInit(TestContext tc)
        {
        }

        public static void AssemblyCleanup()
        {
        }

        public void TestInitOrCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyChildBaseTestClassWithInitAndCleanupMethods : DummyBaseTestClassWithInitAndCleanupMethods
    {
        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static void ChildClassInit(TestContext tc)
        {
        }

        public static void ChildClassCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithParentAndGrandparentInitAndCleanupMethods : DummyChildBaseTestClassWithInitAndCleanupMethods
    {
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithIncorrectInitializeMethods
    {
        public static void TestInit(int i)
        {
        }

        public void AssemblyInit(TestContext tc)
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithIncorrectCleanupMethods
    {
        public static void TestCleanup(int i)
        {
        }

        public void AssemblyCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithIncorrectTestMethodSignatures
    {
        public static void TestMethod()
        {
        }
    }

    private class DerivedTestMethodAttribute : TestMethodAttribute;

    private class DummyTestClassAttribute : TestClassAttribute;

    #endregion
}
