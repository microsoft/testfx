// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using global::MSTestAdapter.TestUtilities;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using static TestMethodInfoTests;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

public class TypeCacheTests : TestContainer
{
    private TypeCache _typeCache;

    private Mock<ReflectHelper> _mockReflectHelper;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

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
        void a() => _typeCache.GetTestMethodInfo(
            null,
            new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
            false);

        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void a() => _typeCache.GetTestMethodInfo(testMethod, null, false);

        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    public void GetTestMethodInfoShouldReturnNullIfClassInfoForTheMethodIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        Verify(
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false) is null);
    }

    public void GetTestMethodInfoShouldReturnNullIfLoadingTypeThrowsTypeLoadException()
    {
        var testMethod = new TestMethod("M", "System.TypedReference[]", "A", isAsync: false);

        Verify(
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false) is null);
    }

    public void GetTestMethodInfoShouldThrowIfLoadingTypeThrowsException()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new Exception("Load failure"));

        void action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith("Unable to get type C. Error: System.Exception: Load failure"));
    }

    public void GetTestMethodInfoShouldThrowIfTypeDoesNotHaveADefaultConstructor()
    {
        string className = typeof(DummyTestClassWithNoDefaultConstructor).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith("Unable to get default constructor for class " + className));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasATypeMismatch()
    {
        string className = typeof(DummyTestClassWithIncorrectTestContextType).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith(string.Format("The {0}.TestContext has incorrect type.", className)));
    }

    public void GetTestMethodInfoShouldThrowIfTestContextHasMultipleAmbiguousTestContextProperties()
    {
        string className = typeof(DummyTestClassWithMultipleTestContextProperties).FullName;
        var testMethod = new TestMethod("M", className, "A", isAsync: false);

        void action() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(action);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);
        Verify(exception.Message.StartsWith(string.Format("Unable to find property {0}.TestContext. Error:{1}.", className, "Ambiguous match found.")));
    }

    public void GetTestMethodInfoShouldSetTestContextIfPresent()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                                    testMethod,
                                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                    false);

        Verify(testMethodInfo is not null);
        Verify(testMethodInfo.Parent.TestContextProperty is not null);
    }

    public void GetTestMethodInfoShouldSetTestContextToNullIfNotPresent()
    {
        var type = typeof(DummyTestClassWithInitializeMethods);
        var methodInfo = type.GetMethod("TestInit");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                                testMethod,
                                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                false);

        Verify(testMethodInfo is not null);
        Verify(testMethodInfo.Parent.TestContextProperty is null);
    }

    #region Assembly Info Creation tests.

    public void GetTestMethodInfoShouldAddAssemblyInfoToTheCache()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 ==_typeCache.AssemblyInfoCache.Count());
    }

    public void GetTestMethodInfoShouldNotThrowIfWeFailToDiscoverTypeFromAnAssembly()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), true)).Throws(new Exception());

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(typeof(DummyTestClassWithTestMethods), typeof(UTF.TestClassAttribute), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.AssemblyInfoCache.Count());
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitializeAttribute()
    {
        var type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.AssemblyInfoCache.Count());
        Verify(type.GetMethod("AssemblyInit") == _typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheAssemblyCleanupAttribute()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.AssemblyInfoCache.Count());
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInitAndCleanupAttribute()
    {
        var type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.AssemblyInfoCache.Count());
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyInitHasIncorrectSignature()
    {
        var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var methodInfo = type.GetMethod("AssemblyInit");
        var expectedMessage =
            string.Format(
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldThrowIfAssemblyCleanupHasIncorrectSignature()
    {
        var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var methodInfo = type.GetMethod("AssemblyCleanup");
        var expectedMessage =
            string.Format(
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheAssemblyInfoInstanceAndReuseTheCache()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        _mockReflectHelper.Verify(rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true), Times.Once);
        Verify(1 == _typeCache.AssemblyInfoCache.Count());
    }

    #endregion

    #region ClassInfo Creation tests.

    public void GetTestMethodInfoShouldAddClassInfoToTheCache()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(_typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod is null);
        Verify(_typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod is null);
    }

    public void GetTestMethodInfoShouldCacheClassInitializeAttribute()
    {
        var type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(0 == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count);
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().ClassInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitializeAttributes()
    {
        var type = typeof(DummyDerivedTestClassWithInitializeMethods);
        var baseType = typeof(DummyTestClassWithInitializeMethods);

        var testMethod = new TestMethod("TestMehtod", type.FullName, "A", false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
           rh => rh.IsAttributeDefined(baseType.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(baseType.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute)))
                    .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
           rh => rh.IsAttributeDefined(type.GetMethod("ClassInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
            testMethod,
            new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
            false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(1 == _typeCache.ClassInfoCache.ToArray()[0].BaseClassInitAndCleanupMethods.Count);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item2 is null, "No base class cleanup");
        Verify(baseType.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item1);
    }

    public void GetTestMethodInfoShouldCacheClassCleanupAttribute()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassCleanupAttributes()
    {
        var type = typeof(DummyDerivedTestClassWithCleanupMethods);

        var baseType = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestMehtod", type.FullName, "A", false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);
        _mockReflectHelper.Setup(
          rh => rh.IsAttributeDefined(baseType.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetCustomAttribute(baseType.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute)))
                   .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _typeCache.GetTestMethodInfo(
            testMethod,
            new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
            false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(1 == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Count);
        Verify(_typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item1 is null, "No base class init");
        Verify(baseType.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.First().BaseClassInitAndCleanupMethods.Peek().Item2);
    }

    public void GetTestMethodInfoShouldCacheClassInitAndCleanupAttribute()
    {
        var type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
    }

    public void GetTestMethodInfoShouldCacheBaseClassInitAndCleanupAttributes()
    {
        var baseType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        var type = typeof(DummyTestClassWithInitAndCleanupMethods);
        var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

        var baseInitializeMethod = baseType.GetMethod("ClassInit");
        var baseCleanupMethod = baseType.GetMethod("ClassCleanup");

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
          rh => rh.IsAttributeDefined(baseInitializeMethod, typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
           rh => rh.GetCustomAttribute(baseInitializeMethod, typeof(UTF.ClassInitializeAttribute)))
                   .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(baseCleanupMethod, typeof(UTF.ClassCleanupAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.GetCustomAttribute(baseCleanupMethod, typeof(UTF.ClassCleanupAttribute)))
                    .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(type.GetMethod("AssemblyInit") == _typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
        Verify(type.GetMethod("AssemblyCleanup") == _typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);

        Verify(1 == _typeCache.ClassInfoCache.ToArray()[0].BaseClassInitAndCleanupMethods.Count);
        Verify(baseInitializeMethod == _typeCache.ClassInfoCache.ToArray()[0].BaseClassInitAndCleanupMethods.Peek().Item1);
        Verify(baseCleanupMethod == _typeCache.ClassInfoCache.ToArray()[0].BaseClassInitAndCleanupMethods.Peek().Item2);
    }

    public void GetTestMethodInfoShouldCacheParentAndGrandparentClassInitAndCleanupAttributes()
    {
        var grandparentType = typeof(DummyBaseTestClassWithInitAndCleanupMethods);
        var parentType = typeof(DummyChildBaseTestClassWithInitAndCleanupMethods);
        var type = typeof(DummyTestClassWithParentAndGrandparentInitAndCleanupMeethods);

        var grandparentInitMethod = grandparentType.GetMethod("ClassInit");
        var grandparentCleanupMethod = grandparentType.GetMethod("ClassCleanup");
        var parentInitMethod = parentType.GetMethod("ChildClassInit");
        var parentCleanupMethod = parentType.GetMethod("ChildClassCleanup");

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true))
            .Returns(true);

        // Setup grandparent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined(grandparentInitMethod, typeof(UTF.ClassInitializeAttribute), false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetCustomAttribute(grandparentInitMethod, typeof(UTF.ClassInitializeAttribute)))
            .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined(grandparentCleanupMethod, typeof(UTF.ClassCleanupAttribute), false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetCustomAttribute(grandparentCleanupMethod, typeof(UTF.ClassCleanupAttribute)))
            .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        // Setup parent class init/cleanup methods
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined(parentInitMethod, typeof(UTF.ClassInitializeAttribute), false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetCustomAttribute(parentInitMethod, typeof(UTF.ClassInitializeAttribute)))
            .Returns(new UTF.ClassInitializeAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined(parentCleanupMethod, typeof(UTF.ClassCleanupAttribute), false))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.GetCustomAttribute(parentCleanupMethod, typeof(UTF.ClassCleanupAttribute)))
            .Returns(new UTF.ClassCleanupAttribute(UTF.InheritanceBehavior.BeforeEachDerivedClass));

        var testMethod = new TestMethod("TestMethod", type.FullName, "A", isAsync: false);
        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var classInfo = _typeCache.ClassInfoCache.FirstOrDefault();
        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(classInfo.ClassInitializeMethod is null);
        Verify(classInfo.ClassCleanupMethod is null);

        Verify(2 == classInfo.BaseClassInitAndCleanupMethods.Count);

        var parentInitAndCleanup = classInfo.BaseClassInitAndCleanupMethods.Dequeue();
        Verify(parentInitMethod == parentInitAndCleanup.Item1);
        Verify(parentCleanupMethod == parentInitAndCleanup.Item2);

        var grandparentInitAndCleanup = classInfo.BaseClassInitAndCleanupMethods.Dequeue();
        Verify(grandparentInitMethod == grandparentInitAndCleanup.Item1);
        Verify(grandparentCleanupMethod == grandparentInitAndCleanup.Item2);
    }

    public void GetTestMethodInfoShouldThrowIfClassInitHasIncorrectSignature()
    {
        var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var methodInfo = type.GetMethod("AssemblyInit");
        var expectedMessage =
            string.Format(
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldThrowIfClassCleanupHasIncorrectSignature()
    {
        var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);
        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var methodInfo = type.GetMethod("AssemblyCleanup");
        var expectedMessage =
            string.Format(
                "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttribute()
    {
        var type = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(type.GetMethod("TestInit") == _typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod);
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttribute()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(type.GetMethod("TestCleanup") == _typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod);
    }

    public void GetTestMethodInfoShouldThrowIfTestInitOrCleanupHasIncorrectSignature()
    {
        var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
        var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var methodInfo = type.GetMethod("TestInit");
        var expectedMessage =
            string.Format(
                "Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldCacheTestInitializeAttributeDefinedInBaseClass()
    {
        var type = typeof(DummyDerivedTestClassWithInitializeMethods);
        var baseType = typeof(DummyTestClassWithInitializeMethods);
        var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(baseType.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(baseType.GetMethod("TestInit") == _typeCache.ClassInfoCache.ToArray()[0].BaseTestInitializeMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheTestCleanupAttributeDefinedInBaseClass()
    {
        var type = typeof(DummyDerivedTestClassWithCleanupMethods);
        var baseType = typeof(DummyTestClassWithCleanupMethods);
        var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(baseType.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(1 == _typeCache.ClassInfoCache.Count());
        Verify(baseType.GetMethod("TestCleanup") == _typeCache.ClassInfoCache.ToArray()[0].BaseTestCleanupMethodsQueue.Peek());
    }

    public void GetTestMethodInfoShouldCacheClassInfoInstanceAndReuseFromCache()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        _testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        Verify(1 == _typeCache.ClassInfoCache.Count());
    }

    #endregion

    #region Method resolution tests

    public void GetTestMethodInfoShouldThrowIfTestMethodHasIncorrectSignatureOrCannotBeFound()
    {
        var type = typeof(DummyTestClassWithIncorrectTestMethodSignatures);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        void a() =>
            _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var expectedMessage = string.Format(
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfo()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoWithTimeout()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
            .Returns(true);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(10 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldThrowWhenTimeoutIsIncorrect()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithIncorrectTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
            .Returns(true);

        void a() => _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var exception = ActionUtility.PerformActionAndReturnException(a);

        Verify(exception is not null);
        Verify(exception is TypeInspectionException);

        var expectedMessage =
            string.Format(
                "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be a valid integer value and cannot be less than 0.",
                testMethod.FullClassName,
                testMethod.Name);

        Verify(expectedMessage == exception.Message);
    }

    public void GetTestMethodInfoWhenTimeoutAttributeNotSetShouldReturnTestMethodInfoWithGlobalTimeout()
    {
        string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                        <TestTimeout>4000</TestTimeout>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(4000 == testMethodInfo.TestMethodOptions.Timeout);
    }

    public void GetTestMethodInfoWhenTimeoutAttributeSetShouldReturnTimeoutBasedOnAtrributeEvenIfGlobalTimeoutSet()
    {
        string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                        <TestTimeout>4000</TestTimeout>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithTimeout");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
           .Returns(true);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(10 == testMethodInfo.TestMethodOptions.Timeout);
    }

    public void GetTestMethodInfoForInvalidGLobalTimeoutShouldReturnTestMethodInfoWithTimeoutZero()
    {
        string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                        <TestTimeout>30.5</TestTimeout>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings.PopulateSettings(MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias));

        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForMethodsAdornedWithADerivedTestMethodAttribute()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithDerivedTestMethodAttribute");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
        Verify(testMethodInfo.TestMethodOptions.Executor is DerivedTestMethodAttribute);
    }

    public void GetTestMethodInfoShouldSetTestContextWithCustomProperty()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithCustomProperty");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            null,
            new Dictionary<string, object>());

        _typeCache.GetTestMethodInfo(testMethod, testContext, false);
        var customProperty = ((IDictionary<string, object>)testContext.Properties).FirstOrDefault(p => p.Key.Equals("WhoAmI"));

        Verify((object)customProperty is not null);
        Verify("Me" == customProperty.Value as string);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyHasSameNameAsPredefinedProperties()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithOwnerAsCustomProperty");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
             null,
            new Dictionary<string, object>());

        var testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        var expectedMessage = string.Format(
            "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name,
            "Owner");
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsEmpty()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithEmptyCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            null,
            new Dictionary<string, object>());

        var testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        var expectedMessage = string.Format(
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsNull()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            null,
            new Dictionary<string, object>());

        var testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);
        var expectedMessage = string.Format(
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);
        Verify(expectedMessage == testMethodInfo.NotRunnableReason);
    }

    public void GetTestMethodInfoShouldNotAddDuplicateTestPropertiesToTestContext()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithDuplicateCustomPropertyNames");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        var testContext = new TestContextImplementation(
            testMethod,
            null,
            new Dictionary<string, object>());

        var testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, false);

        Verify(testMethodInfo is not null);

        // Verify that the first value gets set.
        Verify(((IDictionary<string, object>)testContext.Properties).TryGetValue("WhoAmI", out var value));
        Verify("Me" == value as string);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedTestClasses()
    {
        var type = typeof(DerivedTestClass);
        var methodInfo = type.GetRuntimeMethod("DummyTestMethod", Array.Empty<Type>());
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedClassMethodOverloadByDefault()
    {
        var type = typeof(DerivedTestClass);
        var methodInfo = type.GetRuntimeMethod("OverloadedTestMethod", Array.Empty<Type>());
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(methodInfo == testMethodInfo.TestMethod);
        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    public void GetTestMethodInfoShouldReturnTestMethodInfoForDeclaringTypeMethodOverload()
    {
        var baseType = typeof(BaseTestClass);
        var type = typeof(DerivedTestClass);
        var methodInfo = baseType.GetRuntimeMethod("OverloadedTestMethod", Array.Empty<Type>());
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false)
        {
            DeclaringClassFullName = baseType.FullName
        };

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        // The two MethodInfo instances will have different ReflectedType properties,
        // so cannot be compared directly. Use MethodHandle to verify it's the same.
        Verify(methodInfo.MethodHandle == testMethodInfo.TestMethod.MethodHandle);
        Verify(0 == testMethodInfo.TestMethodOptions.Timeout);
        Verify(_typeCache.ClassInfoCache.ToArray()[0] == testMethodInfo.Parent);
        Verify(testMethodInfo.TestMethodOptions.Executor is not null);
    }

    #endregion

    #endregion

    #region ClassInfoListWithExecutableCleanupMethods tests

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheIsEmpty()
    {
        var cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(0 == cleanupMethods.Count());
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheDoesNotHaveTestCleanupMethods()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(0 == cleanupMethods.Count());
    }

    public void ClassInfoListWithExecutableCleanupMethodsShouldReturnClassInfosWithExecutableCleanupMethods()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var cleanupMethods = _typeCache.ClassInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(1 == cleanupMethods.Count());
        Verify(type.GetMethod("AssemblyCleanup") == cleanupMethods.ToArray()[0].ClassCleanupMethod);
    }

    #endregion

    #region AssemblyInfoListWithExecutableCleanupMethods tests

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheIsEmpty()
    {
        var cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(0 == cleanupMethods.Count());
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheDoesNotHaveTestCleanupMethods()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(false);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(0 == cleanupMethods.Count());
    }

    public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnAssemblyInfosWithExecutableCleanupMethods()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var methodInfo = type.GetMethod("TestCleanup");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

        _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        var cleanupMethods = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;

        Verify(cleanupMethods is not null);
        Verify(1 == cleanupMethods.Count());
        Verify(type.GetMethod("AssemblyCleanup") == cleanupMethods.ToArray()[0].AssemblyCleanupMethod);
    }

    #endregion

    #region ResolveExpectedExceptionHelper tests

    public void ResolveExpectedExceptionHelperShouldReturnExpectedExceptionAttributeIfPresent()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithExpectedException");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException));

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
            .Returns(true);
        _mockReflectHelper.Setup(rh => rh.ResolveExpectedExceptionHelper(methodInfo, testMethod)).Returns(expectedException);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        Verify(expectedException == testMethodInfo.TestMethodOptions.ExpectedException);
    }

    public void ResolveExpectedExceptionHelperShouldReturnNullIfExpectedExceptionAttributeIsNotPresent()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
            .Returns(true);

        var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException));

        Verify(testMethodInfo.TestMethodOptions.ExpectedException is null);
    }

    public void ResolveExpectedExceptionHelperShouldThrowIfMultipleExpectedExceptionAttributesArePresent()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithMultipleExpectedException");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
            .Returns(true);

        try
        {
            var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);
        }
        catch (Exception ex)
        {
            var message = "The test method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TypeCacheTests+DummyTestClassWithTestMethods.TestMethodWithMultipleExpectedException "
                + "has multiple attributes derived from ExpectedExceptionBaseAttribute defined on it. Only one such attribute is allowed.";
            Verify(ex.Message == message);
        }
    }

    #endregion

    private void SetupMocks()
    {
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
    }

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
        public void TestMethodWithIncorrectTimeout()
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
    internal class DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage
    {
    }

    [UTF.Ignore]
    [DummyTestClass]
    internal class DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage : DummyTestClassWithIgnoreTestWithMessage
    {
    }

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
        private DummyTestClassWithNoDefaultConstructor(int a)
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

    private class DummyTestClassWithMultipleTestContextProperties : DummyTestClassWithTestContextProperty
    {
    }

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

        public void TestMehtod()
        {
        }
    }

    [DummyTestClass]
    private class DummyDerivedTestClassWithCleanupMethods : DummyTestClassWithCleanupMethods
    {
        public static void ClassCleanup()
        {
        }

        public void TestMehtod()
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
    private class DummyTestClassWithParentAndGrandparentInitAndCleanupMeethods : DummyChildBaseTestClassWithInitAndCleanupMethods
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

    private class DerivedTestMethodAttribute : UTF.TestMethodAttribute
    {
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }

    #endregion
}
