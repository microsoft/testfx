// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using static Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheTests : TestContainer
{
    private readonly TypeCache _typeCache;

    private readonly Mock<ReflectHelper> _mockReflectHelper;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);

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

    public void GetTestMethodInfoShouldThrowIfTestMethodIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void A() => _typeCache.GetTestMethodInfo(
            null,
            new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
            false);

        Exception ex = VerifyThrows(A);
        Verify(ex.GetType() == typeof(ArgumentNullException));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void A() => _typeCache.GetTestMethodInfo(testMethod, null, false);

        Exception ex = VerifyThrows(A);
        Verify(ex.GetType() == typeof(ArgumentNullException));
    }

    public void GetTestMethodInfoShouldReturnNullIfClassInfoForTheMethodIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        Verify(
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false) is null);
    }

    public void GetTestMethodInfoShouldReturnNullIfLoadingTypeThrowsTypeLoadException()
    {
        var testMethod = new TestMethod("M", "System.TypedReference[]", "A", isAsync: false);

        Verify(
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false) is null);
    }

    public void GetTestMethodInfoShouldThrowIfLoadingTypeThrowsException()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new Exception("Load failure"));

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(Action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith("Unable to get type C. Error: System.Exception: Load failure", StringComparison.Ordinal));
    }

    public void GetTestMethodInfoShouldThrowIfTypeDoesNotHaveADefaultConstructor()
    {
        string className = typeof(DummyTestClassWithNoDefaultConstructor).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(Action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith("Unable to get default constructor for class " + className, StringComparison.Ordinal));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasATypeMismatch()
    {
        string className = typeof(DummyTestClassWithIncorrectTestContextType).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(Action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith($"The {className}.TestContext has incorrect type.", StringComparison.Ordinal));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasMultipleAmbiguousTestContextProperties()
    {
        string className = typeof(DummyTestClassWithMultipleTestContextProperties).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void Action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(Action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith(string.Format(CultureInfo.InvariantCulture, "Unable to find property {0}.TestContext. Error:{1}.", className, "Ambiguous match found."), StringComparison.Ordinal));
    }

    public void GetTestMethodInfoShouldSetTestContextIfPresent()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                                    testMethod,
                                    new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                                    false);

        Verify(testMethodInfo is not null);
        Verify(testMethodInfo.Parent.TestContextProperty is not null);
    }

    public void GetTestMethodInfoShouldSetTestContextToNullIfNotPresent()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        MethodInfo methodInfo = type.GetMethod("TestInit");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                                testMethod,
                                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                                false);

        Verify(testMethodInfo is not null);
        Verify(testMethodInfo.Parent.TestContextProperty is null);
    }

    #region Assembly Info Creation tests.

    public void GetTestMethodInfoShouldAddAssemblyInfoToTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.AssemblyInfoCache.Count == 1);
    }

    public void GetTestMethodInfoShouldNotThrowIfWeFailToDiscoverTypeFromAnAssembly()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(It.IsAny<Type>(), true)).Throws(new Exception());

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(typeof(DummyTestClassWithTestMethods), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.AssemblyInfoCache.Count == 1);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.AssemblyInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.AssemblyInfoCache.First().AssemblyInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheAssemblyCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.AssemblyInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.AssemblyInfoCache.First().AssemblyCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitAndCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.AssemblyInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.AssemblyInfoCache.First().AssemblyCleanupMethod);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.AssemblyInfoCache.First().AssemblyInitializeMethod);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyInitHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        MethodInfo methodInfo = type.GetMethod("AssemblyInit");
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        MethodInfo methodInfo = type.GetMethod("AssemblyCleanup");
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInfoInstanceAndReuseTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        _mockReflectHelper.Verify(rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true), Times.Once);
        Verify(_typeCache.AssemblyInfoCache.Count == 1);
    }

    #endregion

    #region ClassInfo Creation tests.

    public void GetTestMethodInfoShouldAddClassInfoToTheCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().TestInitializeMethod is null);
        Verify(_typeCache.ClassInfoCache.First().TestCleanupMethod is null);
    }

    public void GetTestMethodInfoShouldCacheClassInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count == 0);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().ClassInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitializeAttributes()
    {
        Type type = typeof(DummyDerivedTestClassWithInitializeMethods);
        Type baseType = typeof(DummyTestClassWithInitializeMethods);

        var testMethod = new TestMethod("TestMethod", type.FullName, "A", false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, false)).Returns(true);

        _mockReflectHelper.Setup(
           rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(baseType.GetMethod("AssemblyInit"), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassInitializeAttribute>(baseType.GetMethod("AssemblyInit"), true))
                    .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
           rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(type.GetMethod("ClassInit"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
            testMethod,
            new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
            false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item2 is null, "No base class cleanup");
        Verify(baseType.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item1);
    }

    public void GetTestMethodInfoShouldCacheClassCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.First().ClassCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassCleanupAttributes()
    {
        Type type = typeof(DummyDerivedTestClassWithCleanupMethods);

        Type baseType = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestMethod", type.FullName, "A", false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);
        _mockReflectHelper.Setup(
          rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(baseType.GetMethod("AssemblyCleanup"), false)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassCleanupAttribute>(baseType.GetMethod("AssemblyCleanup"), true))
                   .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _typeCache.GetTestMethodInfo(
            testMethod,
            new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
            false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count == 1);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item1 is null, "No base class init");
        Verify(baseType.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item2);
    }

    public void GetTestMethodInfoShouldCacheClassInitAndCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().ClassInitializeMethod);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.First().ClassCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitAndCleanupAttributes()
    {
        Type baseType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        Type type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        MethodInfo baseInitializeMethod = baseType.GetMethod("ClassInit");
        MethodInfo baseCleanupMethod = baseType.GetMethod("ClassCleanup");

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
          rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(baseInitializeMethod, false)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassInitializeAttribute>(baseInitializeMethod, true))
                   .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(baseCleanupMethod, false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassCleanupAttribute>(baseCleanupMethod, true))
                    .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().ClassInitializeMethod);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.First().ClassCleanupMethod);

        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count == 1);
        Verify(baseInitializeMethod == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item1);
        Verify(baseCleanupMethod == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item2);
    }

    public void GetTestMethodInfoShouldCacheParentAndGrandparentClassInitAndCleanupAttributes()
    {
        Type grandparentType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        Type parentType = typeof(DummyChildBaseTestClassWithInitAndCleanupMethods);
        Type type = typeof(DummyTestClassWithParentAndGrandparentInitAndCleanupMethods);

        MethodInfo grandparentInitMethod = grandparentType.GetMethod("ClassInit");
        MethodInfo grandparentCleanupMethod = grandparentType.GetMethod("ClassCleanup");
        MethodInfo parentInitMethod = parentType.GetMethod("ChildClassInit");
        MethodInfo parentCleanupMethod = parentType.GetMethod("ChildClassCleanup");

        _mockReflectHelper
            .Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true))
            .Returns(true);

        // Setup grandparent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(grandparentInitMethod, false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassInitializeAttribute>(grandparentInitMethod, true))
            .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(grandparentCleanupMethod, false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassCleanupAttribute>(grandparentCleanupMethod, true))
            .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        // Setup parent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(parentInitMethod, false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassInitializeAttribute>(parentInitMethod, true))
            .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(parentCleanupMethod, false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetFirstDerivedAttributeOrDefault<UTF.ClassCleanupAttribute>(parentCleanupMethod, true))
            .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        var testMethod = new TestMethod("TestMethod", type.FullName, "A", isAsync: false);
        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        TestClassInfo classInfo = _typeCache.ClassInfoCache.FirstOrDefault();
        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(classInfo.ClassInitializeMethod is null);
        Verify(classInfo.ClassCleanupMethod is null);

        Verify(classInfo.BaseClassInitAndCleanupMethods.Count == 2);

        Tuple<MethodInfo, MethodInfo> parentInitAndCleanup = classInfo.BaseClassInitAndCleanupMethods.Dequeue();
        Verify(parentInitMethod == parentInitAndCleanup.Item1);
        Verify(parentCleanupMethod == parentInitAndCleanup.Item2);

        Tuple<MethodInfo, MethodInfo> grandparentInitAndCleanup = classInfo.BaseClassInitAndCleanupMethods.Dequeue();
        Verify(grandparentInitMethod == grandparentInitAndCleanup.Item1);
        Verify(grandparentCleanupMethod == grandparentInitAndCleanup.Item2);
    }

    public void GetTestMethodInfoShouldThrowIfClassInitHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassInitializeAttribute>(type.GetMethod("AssemblyInit"), false)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        MethodInfo methodInfo = type.GetMethod("AssemblyInit");
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldThrowIfClassCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        MethodInfo methodInfo = type.GetMethod("AssemblyCleanup");
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttribute()
    {
        Type type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestInitializeAttribute>(type.GetMethod("TestInit"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(type.GetMethod("TestInit") == _typeCache.ClassInfoCache.First().TestInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttribute()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestCleanupAttribute>(type.GetMethod("TestCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(type.GetMethod("TestCleanup") == _typeCache.ClassInfoCache.First().TestCleanupMethod);
    }

    public void GetTestMethodInfoShouldThrowIfTestInitOrCleanupHasIncorrectSignature()
    {
        Type type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestInitializeAttribute>(type.GetMethod("TestInit"), false)).Returns(true);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        MethodInfo methodInfo = type.GetMethod("TestInit");
        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be 'Task' or 'ValueTask'.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttributeDefinedInBaseClass()
    {
        Type type = typeof(DummyDerivedTestClassWithInitializeMethods);
        Type baseType = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestMethod", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestInitializeAttribute>(baseType.GetMethod("TestInit"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(baseType.GetMethod("TestInit") == _typeCache.ClassInfoCache.First().BaseTestInitializeMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttributeDefinedInBaseClass()
    {
        Type type = typeof(DummyDerivedTestClassWithCleanupMethods);
        Type baseType = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestMethod", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestCleanupAttribute>(baseType.GetMethod("TestCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(_typeCache.ClassInfoCache.Count == 1);
        Verify(baseType.GetMethod("TestCleanup") == _typeCache.ClassInfoCache.First().BaseTestCleanupMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheClassInfoInstanceAndReuseFromCache()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        _testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        Verify(_typeCache.ClassInfoCache.Count == 1);
    }

    #endregion

    #region Method resolution tests

    public void GetTestMethodInfoShouldThrowIfTestMethodHasIncorrectSignatureOrCannotBeFound()
    {
        Type type = typeof(DummyTestClassWithIncorrectTestMethodSignatures);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        void A() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfo()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoWithTimeout()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TimeoutAttribute>(methodInfo, false))
            .Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 10);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldThrowWhenTimeoutIsNegative()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNegativeTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TimeoutAttribute>(methodInfo, false))
            .Returns(true);

        void A() => _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be an integer value greater than 0.",
                testMethod.FullClassName,
                testMethod.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldThrowWhenTimeoutIsZero()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeoutOfZero");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TimeoutAttribute>(methodInfo, false))
            .Returns(true);

        void A() => _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Exception exception = VerifyThrows(A);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        string expectedMessage =
            string.Format(
                CultureInfo.InvariantCulture,
                "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be an integer value greater than 0.",
                testMethod.FullClassName,
                testMethod.Name);

        Verify(expectedMessage == exception.Message);
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

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias));

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(testMethodInfo.TestMethodOptions.Timeout == 4000);
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

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias));

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.TimeoutAttribute>(methodInfo, false))
           .Returns(true);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(testMethodInfo.TestMethodOptions.Timeout == 10);
    }

    public void GetTestMethodInfoForInvalidGLobalTimeoutShouldReturnTestMethodInfoWithTimeoutZero()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <TestTimeout>30.5</TestTimeout>
              </MSTestV2>
            </RunSettings>
            """;

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias));

        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForMethodsAdornedWithADerivedTestMethodAttribute()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithDerivedTestMethodAttribute");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
        Verify(testMethodInfo.TestMethodOptions.Executor is DerivedTestMethodAttribute);
    }

    public void GetTestMethodInfoShouldSetTestContextWithCustomProperty()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithCustomProperty");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            new ThreadSafeStringWriter(null, "test"),
            new Dictionary<string, object>());

        _typeCache.GetTestMethodInfo(testMethod, testContext, false);
        KeyValuePair<string, object> customProperty = ((IDictionary<string, object>)testContext.Properties).FirstOrDefault(p => p.Key.Equals("WhoAmI", StringComparison.Ordinal));

        Verify((object)customProperty is not null);
        Verify((customProperty.Value as string) == "Me");
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyHasSameNameAsPredefinedProperties()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithOwnerAsCustomProperty");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            new ThreadSafeStringWriter(null, "test"),
            new Dictionary<string, object>());

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name,
            "Owner");
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsEmpty()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithEmptyCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            new ThreadSafeStringWriter(null, "test"),
            new Dictionary<string, object>());

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsNull()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            new ThreadSafeStringWriter(null, "test"),
            new Dictionary<string, object>());

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldNotAddDuplicateTestPropertiesToTestContext()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithDuplicateCustomPropertyNames");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            new ThreadSafeStringWriter(null, "test"),
            new Dictionary<string, object>());

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);

        // Verify that the first value gets set.
        Verify(((IDictionary<string, object>)testContext.Properties).TryGetValue("WhoAmI", out object value));
        Verify((value as string) == "Me");
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedTestClasses()
    {
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = type.GetRuntimeMethod("DummyTestMethod", []);
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedClassMethodOverloadByDefault()
    {
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = type.GetRuntimeMethod("OverloadedTestMethod", []);
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDeclaringTypeMethodOverload()
    {
        Type baseType = typeof(BaseTestClass);
        Type type = typeof(DerivedTestClass);
        MethodInfo methodInfo = baseType.GetRuntimeMethod("OverloadedTestMethod", []);
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false)
        {
            DeclaringClassFullName = baseType.FullName,
        };

        _mockReflectHelper.Setup(rh => rh.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(It.IsAny<MethodInfo>(), false)).CallBase();
        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        // The two MethodInfo instances will have different ReflectedType properties,
        // so cannot be compared directly. Use MethodHandle to verify it's the same.
        Verify(methodInfo.MethodHandle == testMethodInfo.TestMethod.MethodHandle);
        Verify(testMethodInfo.TestMethodOptions.Timeout == 0);
        Verify(_typeCache.ClassInfoCache.First() == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    #endregion

    #endregion

    #region ClassInfoListWithExecutableCleanupMethods tests

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheIsEmpty()
    {
        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(!cleanupMethods.Any());
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheDoesNotHaveTestCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("TestCleanup"), false)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(!cleanupMethods.Any());
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnClassInfosWithExecutableCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.ClassCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        IEnumerable<TestClassInfo> cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods.Count() == 1);
        Verify(type.GetMethod("AssemblyCleanup") == cleanupMethods.First().ClassCleanupMethod);
    }

    #endregion

    #region AssemblyInfoListWithExecutableCleanupMethods tests

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheIsEmpty()
    {
        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(!cleanupMethods.Any());
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheDoesNotHaveTestCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type, true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(!cleanupMethods.Any());
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnAssemblyInfoWithExecutableCleanupMethods()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        MethodInfo methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestClassAttribute>(type.GetTypeInfo(), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyCleanupAttribute>(type.GetMethod("AssemblyCleanup"), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        IEnumerable<TestAssemblyInfo> cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods.Count() == 1);
        Verify(type.GetMethod("AssemblyCleanup") == cleanupMethods.First().AssemblyCleanupMethod);
    }

    #endregion

    #region ResolveExpectedExceptionHelper tests

    public void ResolveExpectedExceptionHelperShouldReturnExpectedExceptionAttributeIfPresent()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithExpectedException");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException));

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ExpectedExceptionAttribute>(methodInfo, false))
            .Returns(true);
        _mockReflectHelper.Setup(rh => rh.ResolveExpectedExceptionHelper(methodInfo, testMethod)).Returns(expectedException);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        Verify(expectedException == testMethodInfo.TestMethodOptions.ExpectedException);
    }

    public void ResolveExpectedExceptionHelperShouldReturnNullIfExpectedExceptionAttributeIsNotPresent()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ExpectedExceptionAttribute>(methodInfo, false))
            .Returns(true);

        TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);

        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException));

        Verify(testMethodInfo.TestMethodOptions.ExpectedException is null);
    }

    public void ResolveExpectedExceptionHelperShouldThrowIfMultipleExpectedExceptionAttributesArePresent()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithMultipleExpectedException");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsNonDerivedAttributeDefined<UTF.ExpectedExceptionAttribute>(methodInfo, false))
            .Returns(true);

        try
        {
            TestMethodInfo testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>()),
                false);
        }
        catch (Exception ex)
        {
            string message = "The test method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TypeCacheTests+DummyTestClassWithTestMethods.TestMethodWithMultipleExpectedException "
                + "has multiple attributes derived from ExpectedExceptionBaseAttribute defined on it. Only one such attribute is allowed.";
            Verify(ex.Message == message);
        }
    }

    #endregion

    private void SetupMocks() => _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

    #region dummy implementations

    [DummyTestClass]
    internal class DummyTestClassWithTestMethods
    {
        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }

        [DerivedTestMethod]
        public void TestMethodWithDerivedTestMethodAttribute()
        {
        }

        [UTF.TestMethod]
        [UTF.Timeout(10)]
        public void TestMethodWithTimeout()
        {
        }

        [UTF.TestMethod]
        [UTF.Timeout(-10)]
        public void TestMethodWithNegativeTimeout()
        {
        }

        [UTF.TestMethod]
        [UTF.Timeout(0)]
        public void TestMethodWithTimeoutOfZero()
        {
        }

        [UTF.TestMethod]
        [UTF.TestProperty("WhoAmI", "Me")]
        public void TestMethodWithCustomProperty()
        {
        }

        [UTF.TestMethod]
        [UTF.TestProperty("Owner", "You")]
        public void TestMethodWithOwnerAsCustomProperty()
        {
        }

        [UTF.TestMethod]
        [UTF.TestProperty("", "You")]
        public void TestMethodWithEmptyCustomPropertyName()
        {
        }

        [UTF.TestMethod]
        [UTF.TestProperty(null, "You")]
        public void TestMethodWithNullCustomPropertyName()
        {
        }

        [UTF.TestMethod]
        [UTF.TestProperty("WhoAmI", "Me")]
        [UTF.TestProperty("WhoAmI", "Me2")]
        public void TestMethodWithDuplicateCustomPropertyNames()
        {
        }

        [UTF.TestMethod]
        [UTF.ExpectedException(typeof(DivideByZeroException))]
        public void TestMethodWithExpectedException()
        {
        }

        [UTF.TestMethod]
        [UTF.ExpectedException(typeof(DivideByZeroException))]
        [CustomExpectedException(typeof(ArgumentNullException), "Custom Exception")]
        public void TestMethodWithMultipleExpectedException()
        {
        }
    }

    [DummyTestClass]
    [UTF.Ignore]
    internal class DummyTestClassWithIgnoreClass
    {
        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    [UTF.Ignore("IgnoreTestClassMessage")]
    internal class DummyTestClassWithIgnoreClassWithMessage
    {
        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    internal class DummyTestClassWithIgnoreTest
    {
        [UTF.TestMethod]
        [UTF.Ignore]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    internal class DummyTestClassWithIgnoreTestWithMessage
    {
        [UTF.TestMethod]
        [UTF.Ignore("IgnoreTestMessage")]
        public void TestMethod()
        {
        }
    }

    [UTF.Ignore("IgnoreTestClassMessage")]
    [DummyTestClass]
    internal class DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage;

    [UTF.Ignore]
    [DummyTestClass]
    internal class DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage;

    [DummyTestClass]
    internal class DerivedTestClass : BaseTestClass
    {
        [UTF.TestMethod]
        public new void OverloadedTestMethod()
        {
        }
    }

    internal class BaseTestClass
    {
        [UTF.TestMethod]
        public void DummyTestMethod()
        {
        }

        [UTF.TestMethod]
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

    private class DummyTestClassWithIncorrectTestContextType
    {
        // This is TP.TF type.
        public virtual int TestContext { get; set; }
    }

    private class DummyTestClassWithTestContextProperty : DummyTestClassWithIncorrectTestContextType
    {
        public new string TestContext { get; set; }
    }

    private class DummyTestClassWithMultipleTestContextProperties : DummyTestClassWithTestContextProperty;

    [DummyTestClass]
    private class DummyTestClassWithInitializeMethods
    {
        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void AssemblyInit(UTFExtension.TestContext tc)
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
        public static void ClassInit(UTFExtension.TestContext tc)
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
        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void ClassInit(UTFExtension.TestContext tc)
        {
        }

        public static void ClassCleanup()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithInitAndCleanupMethods : DummyBaseTestClassWithInitAndCleanupMethods
    {
        public static void AssemblyInit(UTFExtension.TestContext tc)
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
        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void ChildClassInit(UTFExtension.TestContext tc)
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

        public void AssemblyInit(UTFExtension.TestContext tc)
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

    private class DerivedTestMethodAttribute : UTF.TestMethodAttribute;

    private class DummyTestClassAttribute : UTF.TestClassAttribute;

    #endregion
}
