// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheClassInfoMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";

    private readonly TypeCache _typeCache;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly Mock<IMessageLogger> _mockMessageLogger;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheClassInfoMutationTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);
        _mockMessageLogger = new Mock<IMessageLogger>();
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        ReflectHelper.ClearCache();
        MSTestSettings.Reset();
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>())).Returns(Assembly.GetExecutingAssembly());
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

#if DEBUG && !NETFRAMEWORK
    public void GetTestMethodInfoShouldAssertThatTheTestMethodIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action act = () => _typeCache.GetTestMethodInfo(null!);

        act.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("test method is null");
    }
#endif

    public void GetTestMethodInfoShouldLogTheTypeAndAssemblyNamesWhenTheTypeCannotBeLoaded()
    {
        _testablePlatformServiceProvider.MockTraceLogger.Setup(l => l.IsWarningEnabled).Returns(true);
        var testMethod = new TestMethod("M", null, "M", "System.TypedReference[]", "A", null, null);

        _typeCache.GetTestMethodInfo(testMethod).Should().BeNull();

        _testablePlatformServiceProvider.MockTraceLogger.Verify(
            l => l.Warning(
                "TypeCache.LoadType: Failed to load type '{0}' from assembly '{1}'. The corresponding test(s) will be reported as not found. {2}",
                "System.TypedReference[]",
                "A",
                It.IsAny<TypeLoadException>()),
            Times.Once);
    }

    public void GetTestMethodInfoShouldTreatAnFSharpModuleAsHavingNoConstructor()
    {
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif
        Type moduleType = new FSharpMetadataAssembly().CreateModuleClass();
        moduleType.GetConstructors().Should().ContainSingle("the dynamic module type gets a default constructor, which must not be selected");
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>())).Returns(moduleType.Assembly);
        var testMethod = new TestMethod(TestMethodName, null, TestMethodName, moduleType.FullName!, "A", null, null)
        {
            MethodInfo = typeof(FixtureTestClass).GetMethod(TestMethodName),
        };

        _typeCache.GetTestMethodInfo(testMethod).Should().NotBeNull();

        TestClassInfo classInfo = _typeCache.ClassInfoCache.First()!;
        classInfo.IsFSharpModule.Should().BeTrue();
        classInfo.Constructor.Should().BeNull();
        classInfo.IsParameterlessConstructor.Should().BeTrue();
        classInfo.ClassAttribute.Should().NotBeNull();
#if DEBUG && !NETFRAMEWORK
        assertions.Should().BeEmpty();
#endif
    }

    public void GetTestMethodInfoShouldIgnoreNonPublicConstructors()
    {
        TestClassInfo classInfo = GetCachedClassInfo(typeof(ClassWithPublicAndPrivateTestContextConstructors));

        classInfo.IsParameterlessConstructor.Should().BeTrue();
        classInfo.Constructor.Should().NotBeNull();
        classInfo.Constructor!.IsPublic.Should().BeTrue();
        classInfo.Constructor.GetParameters().Should().BeEmpty();
    }

    public void GetTestMethodInfoShouldStopAtTheFirstTestContextConstructor()
    {
        Type type = typeof(ClassWithTestContextConstructor);
        ConstructorInfo first = type.GetConstructors()[0];
        ConstructorInfo second = typeof(OtherClassWithTestContextConstructor).GetConstructors()[0];
        var mockReflectionOperations = new Mock<IReflectionOperations>();
        mockReflectionOperations.Setup(m => m.GetType(type.FullName!)).Returns(type);
        mockReflectionOperations.Setup(m => m.GetDeclaredConstructors(type)).Returns(new[] { first, second });
        mockReflectionOperations.Setup(m => m.GetCustomAttributes(type)).Returns(new object[] { new TestClassAttribute() });
        _testablePlatformServiceProvider.SetupMockReflectionOperations(mockReflectionOperations);

        TestClassInfo classInfo = GetCachedClassInfo(type);

        classInfo.Constructor.Should().BeSameAs(first);
        classInfo.IsParameterlessConstructor.Should().BeFalse();
    }

    public void GetTestMethodInfoShouldThrowWhenThereIsNoValidConstructorAndNoInstanceFactory()
    {
        TestClassInstanceFactoryProvider.Current.Should().BeNull();
        Type type = typeof(ClassWithOnlyAnIntConstructor);

        Action action = () => _typeCache.GetTestMethodInfo(CreateTestMethod(type));

        TypeInspectionException exception = action.Should().Throw<TypeInspectionException>().Which;
        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_NoValidConstructor, type.FullName));
    }

    public void GetTestMethodInfoShouldUseTheFirstPublicConstructorWhenAnInstanceFactoryIsRegistered()
    {
        using IDisposable factoryScope = TestClassInstanceFactoryProvider.Push(new ThrowingTestClassInstanceFactory());

        TestClassInfo classInfo = GetCachedClassInfo(typeof(ClassWithOnlyAnIntConstructor));

        classInfo.Constructor.Should().NotBeNull();
        classInfo.Constructor!.GetParameters().Select(p => p.ParameterType).Should().Equal(typeof(int));
        classInfo.IsParameterlessConstructor.Should().BeFalse();
    }

    public void GetTestMethodInfoShouldRecordTheTimeoutOfAClassInitializeMethod()
    {
        MethodInfo method = GetFixtureMethod(nameof(FixtureTestClass.ClassInit));
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<ClassInitializeAttribute>(method)).Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(method)).Returns(new TimeoutAttribute(1234));

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.ClassInitializeMethodTimeoutMilliseconds.Should().HaveCount(1);
        classInfo.ClassInitializeMethodTimeoutMilliseconds[method].Timeout.Should().Be(1234);
    }

    public void GetTestMethodInfoShouldRecordTheTimeoutOfAClassCleanupMethod()
    {
        MethodInfo method = GetFixtureMethod(nameof(FixtureTestClass.ClassCleanup));
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<ClassCleanupAttribute>(method)).Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(method)).Returns(new TimeoutAttribute(1235));

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.ClassCleanupMethodTimeoutMilliseconds.Should().HaveCount(1);
        classInfo.ClassCleanupMethodTimeoutMilliseconds[method].Timeout.Should().Be(1235);
    }

    public void GetTestMethodInfoShouldRecordTheTimeoutOfATestInitializeMethod()
    {
        MethodInfo method = GetFixtureMethod(nameof(FixtureTestClass.TestInit));
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestInitializeAttribute>(method)).Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(method)).Returns(new TimeoutAttribute(1236));

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.TestInitializeMethodTimeoutMilliseconds.Should().HaveCount(1);
        classInfo.TestInitializeMethodTimeoutMilliseconds[method].Timeout.Should().Be(1236);
    }

    public void GetTestMethodInfoShouldRecordTheTimeoutOfATestCleanupMethod()
    {
        MethodInfo method = GetFixtureMethod(nameof(FixtureTestClass.TestCleanup));
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestCleanupAttribute>(method)).Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(method)).Returns(new TimeoutAttribute(1237));

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.TestCleanupMethodTimeoutMilliseconds.Should().HaveCount(1);
        classInfo.TestCleanupMethodTimeoutMilliseconds[method].Timeout.Should().Be(1237);
    }

    public void GetTestMethodInfoShouldThrowForAnInvalidFixtureTimeout()
    {
        MethodInfo method = GetFixtureMethod(nameof(FixtureTestClass.ClassInit));
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<ClassInitializeAttribute>(method)).Returns(true);
        _mockReflectHelper.Setup(rh => rh.GetFirstAttributeOrDefault<TimeoutAttribute>(method)).Returns(new TimeoutAttribute(-1));

        Action action = () => _typeCache.GetTestMethodInfo(CreateTestMethod(typeof(FixtureTestClass)));

        TypeInspectionException exception = action.Should().Throw<TypeInspectionException>().Which;
        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, typeof(FixtureTestClass).FullName, nameof(FixtureTestClass.ClassInit)));
    }

    public void GetTestMethodInfoShouldUseTheGlobalFixtureTimeoutsWhenNoTimeoutAttributeIsSpecified()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <ClassInitializeTimeout>1001</ClassInitializeTimeout>
                <ClassCleanupTimeout>1002</ClassCleanupTimeout>
                <TestInitializeTimeout>1003</TestInitializeTimeout>
                <TestCleanupTimeout>1004</TestCleanupTimeout>
              </MSTestV2>
            </RunSettings>
            """;
        var settings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object.ToAdapterMessageLogger());
        settings.Should().NotBeNull();
        MSTestSettings.PopulateSettings(settings);
        SetupAllFixtureMethods();

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.ClassInitializeMethodTimeoutMilliseconds[GetFixtureMethod(nameof(FixtureTestClass.ClassInit))].Timeout.Should().Be(1001);
        classInfo.ClassCleanupMethodTimeoutMilliseconds[GetFixtureMethod(nameof(FixtureTestClass.ClassCleanup))].Timeout.Should().Be(1002);
        classInfo.TestInitializeMethodTimeoutMilliseconds[GetFixtureMethod(nameof(FixtureTestClass.TestInit))].Timeout.Should().Be(1003);
        classInfo.TestCleanupMethodTimeoutMilliseconds[GetFixtureMethod(nameof(FixtureTestClass.TestCleanup))].Timeout.Should().Be(1004);
    }

    public void GetTestMethodInfoShouldNotRecordFixtureTimeoutsWhenNoTimeoutIsConfigured()
    {
        SetupAllFixtureMethods();

        TestClassInfo classInfo = GetCachedClassInfo(typeof(FixtureTestClass));

        classInfo.ClassInitializeMethodTimeoutMilliseconds.Should().BeEmpty();
        classInfo.ClassCleanupMethodTimeoutMilliseconds.Should().BeEmpty();
        classInfo.TestInitializeMethodTimeoutMilliseconds.Should().BeEmpty();
        classInfo.TestCleanupMethodTimeoutMilliseconds.Should().BeEmpty();
    }

    public void GetTestMethodInfoShouldNotQueueAnOverriddenBaseTestInitializeMethod()
    {
        MethodInfo derivedInit = typeof(DerivedFixtureClass).GetMethod(nameof(DerivedFixtureClass.Init))!;
        MethodInfo baseInit = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.Init))!;
        MethodInfo baseOnlyInit = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.BaseOnlyInit))!;
        SetupTestInitializeMethods(derivedInit, baseInit, baseOnlyInit);

        TestClassInfo classInfo = GetCachedClassInfo(typeof(DerivedFixtureClass));

        classInfo.TestInitializeMethod.Should().BeSameAs(derivedInit);
        classInfo.BaseTestInitializeMethodsQueue.Should().Equal(baseOnlyInit);
    }

    public void GetTestMethodInfoShouldNotQueueABaseTestInitializeMethodOverriddenByAnUnattributedMethod()
    {
        MethodInfo baseInit = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.Init))!;
        MethodInfo baseOnlyInit = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.BaseOnlyInit))!;
        SetupTestInitializeMethods(baseInit, baseOnlyInit);

        TestClassInfo classInfo = GetCachedClassInfo(typeof(DerivedFixtureClass));

        classInfo.TestInitializeMethod.Should().BeNull();
        classInfo.BaseTestInitializeMethodsQueue.Should().Equal(baseOnlyInit);
    }

    public void GetTestMethodInfoShouldNotQueueAnOverriddenBaseTestCleanupMethod()
    {
        MethodInfo derivedCleanup = typeof(DerivedFixtureClass).GetMethod(nameof(DerivedFixtureClass.Cleanup))!;
        MethodInfo baseCleanup = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.Cleanup))!;
        MethodInfo baseOnlyCleanup = typeof(BaseFixtureClass).GetMethod(nameof(BaseFixtureClass.BaseOnlyCleanup))!;
        foreach (MethodInfo method in new[] { derivedCleanup, baseCleanup, baseOnlyCleanup })
        {
            _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestCleanupAttribute>(method)).Returns(true);
        }

        TestClassInfo classInfo = GetCachedClassInfo(typeof(DerivedFixtureClass));

        classInfo.TestCleanupMethod.Should().BeSameAs(derivedCleanup);
        classInfo.BaseTestCleanupMethodsQueue.Should().Equal(baseOnlyCleanup);
    }

    private static MethodInfo GetFixtureMethod(string name)
        => typeof(FixtureTestClass).GetMethod(name)!;

    private static TestMethod CreateTestMethod(Type type)
        => new(TestMethodName, null, TestMethodName, type.FullName!, "A", null, null) { MethodInfo = type.GetMethod(TestMethodName) };

    private TestClassInfo GetCachedClassInfo(Type type)
    {
        _typeCache.GetTestMethodInfo(CreateTestMethod(type)).Should().NotBeNull();

        return _typeCache.ClassInfoCache.First()!;
    }

    private void SetupTestInitializeMethods(params MethodInfo[] methods)
    {
        foreach (MethodInfo method in methods)
        {
            _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestInitializeAttribute>(method)).Returns(true);
        }
    }

    private void SetupAllFixtureMethods()
    {
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<ClassInitializeAttribute>(GetFixtureMethod(nameof(FixtureTestClass.ClassInit)))).Returns(true);
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<ClassCleanupAttribute>(GetFixtureMethod(nameof(FixtureTestClass.ClassCleanup)))).Returns(true);
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestInitializeAttribute>(GetFixtureMethod(nameof(FixtureTestClass.TestInit)))).Returns(true);
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestCleanupAttribute>(GetFixtureMethod(nameof(FixtureTestClass.TestCleanup)))).Returns(true);
    }

    [TestClass]
    public class ClassWithPublicAndPrivateTestContextConstructors
    {
        public ClassWithPublicAndPrivateTestContextConstructors()
        {
        }

#pragma warning disable IDE0051 // Remove unused private members
        private ClassWithPublicAndPrivateTestContextConstructors(TestContext testContext)
        {
        }
#pragma warning restore IDE0051 // Remove unused private members

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class ClassWithTestContextConstructor
    {
        public ClassWithTestContextConstructor(TestContext testContext)
        {
        }

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class OtherClassWithTestContextConstructor
    {
        public OtherClassWithTestContextConstructor(TestContext testContext)
        {
        }

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class ClassWithOnlyAnIntConstructor
    {
        public ClassWithOnlyAnIntConstructor(int value)
        {
        }

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class FixtureTestClass
    {
        public static void ClassInit(TestContext testContext)
        {
        }

        public static void ClassCleanup()
        {
        }

        public void TestInit()
        {
        }

        public void TestCleanup()
        {
        }

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class BaseFixtureClass
    {
        public virtual void Init()
        {
        }

        public virtual void Cleanup()
        {
        }

        public void BaseOnlyInit()
        {
        }

        public void BaseOnlyCleanup()
        {
        }
    }

    [TestClass]
    public class DerivedFixtureClass : BaseFixtureClass
    {
        public override void Init()
        {
        }

        public override void Cleanup()
        {
        }

        public void TestMethod()
        {
        }
    }

    private sealed class ThrowingTestClassInstanceFactory : ITestClassInstanceFactory
    {
        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
            => throw new NotSupportedException();
    }

    // Models the compiler metadata without a dependency on FSharp.Core. The attribute type is defined in the same
    // dynamic assembly as the type that carries it, so that no cross-assembly reference between dynamic assemblies is needed.
    private sealed class FSharpMetadataAssembly
    {
        private const int ModuleKind = 7;

        private readonly ModuleBuilder _module;
        private readonly Type _compilationMapping;

        public FSharpMetadataAssembly()
        {
            var name = new AssemblyName("FSharp.Core");
#if NETFRAMEWORK
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
            _module = assemblyBuilder.DefineDynamicModule("MetadataModule");
            _compilationMapping = DefineCompilationMappingAttribute();
        }

        public Type CreateModuleClass()
        {
            TypeBuilder builder = _module.DefineType("FSharpModuleFixture_" + Guid.NewGuid().ToString("N"), TypeAttributes.Public);
            builder.SetCustomAttribute(new CustomAttributeBuilder(_compilationMapping.GetConstructor([typeof(int)])!, [ModuleKind]));
            return builder.CreateTypeInfo()!.AsType();
        }

        private static void DefineConstructor(TypeBuilder builder, Type[] parameterTypes)
        {
            ConstructorBuilder constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameterTypes);
            ILGenerator il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
            il.Emit(OpCodes.Ret);
        }

        private Type DefineCompilationMappingAttribute()
        {
            TypeBuilder builder = _module.DefineType("Microsoft.FSharp.Core.CompilationMappingAttribute", TypeAttributes.Public | TypeAttributes.Sealed, typeof(Attribute));
            DefineConstructor(builder, Type.EmptyTypes);
            DefineConstructor(builder, [typeof(int)]);
            return builder.CreateTypeInfo()!.AsType();
        }
    }
}
