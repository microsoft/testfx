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

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TypeCacheClassInfoPass2MutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";
    private const string WrongSignatureMethodName = "WrongSignature";

    private readonly TypeCache _typeCache;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheClassInfoPass2MutationTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);
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

    public void GetTestMethodInfoShouldKeepTheFirstOfSeveralPublicConstructorsWhenAnInstanceFactoryIsRegistered()
    {
        using IDisposable factoryScope = TestClassInstanceFactoryProvider.Push(new ThrowingTestClassInstanceFactory());
        Type type = typeof(ClassWithSeveralParameterizedConstructors);
        ConstructorInfo intConstructor = type.GetConstructor([typeof(int)])!;
        ConstructorInfo stringConstructor = type.GetConstructor([typeof(string)])!;
        SetupDeclaredConstructors(type, intConstructor, stringConstructor);

        TestClassInfo classInfo = GetCachedClassInfo(type);

        classInfo.Constructor.Should().BeSameAs(intConstructor);
        classInfo.IsParameterlessConstructor.Should().BeFalse();
    }

    public void GetTestMethodInfoShouldKeepTheFirstOfSeveralParameterlessConstructors()
    {
        // A type cannot declare two parameterless constructors, so the second one comes from another type.
        Type type = typeof(ClassWithAParameterlessConstructor);
        ConstructorInfo first = type.GetConstructors()[0];
        ConstructorInfo second = typeof(OtherClassWithAParameterlessConstructor).GetConstructors()[0];
        SetupDeclaredConstructors(type, first, second);

        TestClassInfo classInfo = GetCachedClassInfo(type);

        classInfo.Constructor.Should().BeSameAs(first);
        classInfo.IsParameterlessConstructor.Should().BeTrue();
    }

    public void GetTestMethodInfoShouldKeepTheTestClassAttributeDeclaredOnAnFSharpModule()
    {
        Type moduleType = new FSharpMetadataAssembly().CreateModuleClass(
            builder => builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(CustomTestClassAttribute).GetConstructor(Type.EmptyTypes)!, [])));
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>())).Returns(moduleType.Assembly);

        _typeCache.GetTestMethodInfo(CreateFSharpTestMethod(moduleType)).Should().NotBeNull();

        TestClassInfo classInfo = _typeCache.ClassInfoCache.First()!;
        classInfo.IsFSharpModule.Should().BeTrue();
        classInfo.ClassAttribute.Should().BeOfType<CustomTestClassAttribute>();
    }

#if DEBUG && !NETFRAMEWORK
    public void GetTestMethodInfoForDiscoveryShouldAssertThatATypeThatIsNotAnFSharpModuleHasATestClassAttribute()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        // The execution path cannot be used: a null class attribute makes the TestMethodInfo constructor throw.
        _typeCache.GetTestMethodInfoForDiscovery(CreateTestMethod(typeof(ClassWithoutTestClassAttribute))).Should().NotBeNull();

        TestClassInfo classInfo = _typeCache.ClassInfoCache.First()!;
        classInfo.IsFSharpModule.Should().BeFalse();
        classInfo.ClassAttribute.Should().BeNull();
        assertions.Should().Equal("testClassAttribute is null");
    }
#endif

    public void GetTestMethodInfoShouldReportTheFSharpSignatureMessageForAnInvalidFSharpTestInitializeFunction()
    {
#if DEBUG && !NETFRAMEWORK
        // The module has no [TestClass]: a regression of the default attribute would otherwise fail the debug assert and crash the test host.
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif
        Type moduleType = new FSharpMetadataAssembly().CreateModuleClass(builder =>
        {
            MethodBuilder method = builder.DefineMethod(WrongSignatureMethodName, MethodAttributes.Public | MethodAttributes.HideBySig, typeof(void), Type.EmptyTypes);
            method.GetILGenerator().Emit(OpCodes.Ret);
        });
        MethodInfo wrongSignature = moduleType.GetMethod(WrongSignatureMethodName)!;
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestInitializeAttribute>(wrongSignature)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>())).Returns(moduleType.Assembly);

        Action action = () => _typeCache.GetTestMethodInfo(CreateFSharpTestMethod(moduleType));

        TypeInspectionException exception = action.Should().Throw<TypeInspectionException>().Which;
        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_FSharpTestInitializeAndCleanupMethodHasWrongSignature, moduleType.FullName, WrongSignatureMethodName));
#if DEBUG && !NETFRAMEWORK
        assertions.Should().BeEmpty();
#endif
    }

    private static TestMethod CreateTestMethod(Type type)
        => new(TestMethodName, null, TestMethodName, type.FullName!, "A", null, null) { MethodInfo = type.GetMethod(TestMethodName) };

    private static TestMethod CreateFSharpTestMethod(Type moduleType)
        => new(TestMethodName, null, TestMethodName, moduleType.FullName!, "A", null, null) { MethodInfo = typeof(ClassWithAParameterlessConstructor).GetMethod(TestMethodName) };

    private void SetupDeclaredConstructors(Type type, params ConstructorInfo[] constructors)
    {
        var mockReflectionOperations = new Mock<IReflectionOperations>();
        mockReflectionOperations.Setup(m => m.GetType(type.FullName!)).Returns(type);
        mockReflectionOperations.Setup(m => m.GetDeclaredConstructors(type)).Returns(constructors);
        mockReflectionOperations.Setup(m => m.GetCustomAttributes(type)).Returns(new object[] { new TestClassAttribute() });
        _testablePlatformServiceProvider.SetupMockReflectionOperations(mockReflectionOperations);
    }

    private TestClassInfo GetCachedClassInfo(Type type)
    {
        _typeCache.GetTestMethodInfo(CreateTestMethod(type)).Should().NotBeNull();

        return _typeCache.ClassInfoCache.First()!;
    }

    [TestClass]
    public class ClassWithSeveralParameterizedConstructors
    {
        public ClassWithSeveralParameterizedConstructors(int value)
        {
        }

        public ClassWithSeveralParameterizedConstructors(string value)
        {
        }

        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class ClassWithAParameterlessConstructor
    {
        public void TestMethod()
        {
        }
    }

    [TestClass]
    public class OtherClassWithAParameterlessConstructor
    {
        public void TestMethod()
        {
        }
    }

    public class ClassWithoutTestClassAttribute
    {
        public void TestMethod()
        {
        }
    }

    public sealed class CustomTestClassAttribute : TestClassAttribute
    {
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

        public Type CreateModuleClass(Action<TypeBuilder> configure)
        {
            TypeBuilder builder = _module.DefineType("FSharpModuleFixture_" + Guid.NewGuid().ToString("N"), TypeAttributes.Public);
            builder.SetCustomAttribute(new CustomAttributeBuilder(_compilationMapping.GetConstructor([typeof(int)])!, [ModuleKind]));
            configure(builder);
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
