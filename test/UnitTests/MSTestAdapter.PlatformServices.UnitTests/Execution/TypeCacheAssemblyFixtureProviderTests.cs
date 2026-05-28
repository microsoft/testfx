// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheAssemblyFixtureProviderTests : TestContainer
{
    private readonly TypeCache _typeCache;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheAssemblyFixtureProviderTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _typeCache = new TypeCache(_mockReflectHelper.Object);

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        ReflectHelper.ClearCache();

        _testablePlatformServiceProvider.MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
            .Returns(Assembly.GetExecutingAssembly());
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

    public void GetTestMethodInfoShouldDiscoverAssemblyInitializeFromProvider()
    {
        Type hostType = typeof(DummyHostTestClass);
        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo providerInit = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyInit))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        // Only the provider's init method has the attribute. The host test class has no
        // [AssemblyInitialize] methods, so the in-assembly pass leaves the slot empty.
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(providerInit))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");
        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyInitializeMethod.Should().BeSameAs(providerInit);
        info.AssemblyCleanupMethod.Should().BeNull();
    }

    public void GetTestMethodInfoShouldDiscoverAssemblyCleanupFromProvider()
    {
        Type hostType = typeof(DummyHostTestClass);
        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo providerCleanup = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyCleanup))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(providerCleanup))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");
        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyCleanupMethod.Should().BeSameAs(providerCleanup);
        info.AssemblyInitializeMethod.Should().BeNull();
    }

    public void GetTestMethodInfoShouldDiscoverBothFromProvider()
    {
        Type hostType = typeof(DummyHostTestClass);
        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo providerInit = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyInit))!;
        MethodInfo providerCleanup = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyCleanup))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(providerInit))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(providerCleanup))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");
        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyInitializeMethod.Should().BeSameAs(providerInit);
        info.AssemblyCleanupMethod.Should().BeSameAs(providerCleanup);
    }

    public void GetTestMethodInfoShouldPreferLocalAssemblyInitializeOverProviderSilently()
    {
        Type localType = typeof(LocalTestClassWithLocalFixtures);
        MethodInfo localInit = localType.GetMethod(nameof(LocalTestClassWithLocalFixtures.LocalAssemblyInit))!;

        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo providerInit = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyInit))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(localType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(localInit))
            .Returns(true);
        // Also tell the discovery the provider's init method has the attribute. Local must win
        // without throwing (the local declaration is always authoritative).
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(providerInit))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(LocalTestClassWithLocalFixtures.TestMethod), localType.FullName!, "A");

        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyInitializeMethod.Should().BeSameAs(localInit);
    }

    public void GetTestMethodInfoShouldFillCleanupFromProviderWhenLocalOnlyHasInitialize()
    {
        Type localType = typeof(LocalTestClassWithLocalFixtures);
        MethodInfo localInit = localType.GetMethod(nameof(LocalTestClassWithLocalFixtures.LocalAssemblyInit))!;

        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo providerCleanup = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyCleanup))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(localType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(localInit))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(providerCleanup))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(LocalTestClassWithLocalFixtures.TestMethod), localType.FullName!, "A");
        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyInitializeMethod.Should().BeSameAs(localInit);
        info.AssemblyCleanupMethod.Should().BeSameAs(providerCleanup);
    }

    public void GetTestMethodInfoShouldThrowWhenProviderMethodHasWrongSignature()
    {
        Type hostType = typeof(DummyHostTestClass);
        Type providerType = typeof(DummyFixtureProvider);
        MethodInfo bad = providerType.GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyInitWithBadSignature))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(bad))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");

        Action act = () => _typeCache.GetTestMethodInfo(testMethod);
        act.Should().Throw<TypeInspectionException>();
    }

    private static TestMethod CreateTestMethod(string methodName, string className, string assemblyName)
        => new(methodName, null, methodName, className, assemblyName, displayName: null, null);

    // The fixture type pointed at by [assembly: AssemblyFixtureProvider(...)] in AssemblyAttributes.cs.
    // It intentionally does NOT carry [TestClass] — markers explicitly opt in to fixture exposure
    // without the type having to be a test class.
    public class DummyFixtureProvider
    {
        public static void ProviderAssemblyInit(TestContext context)
        {
        }

        public static void ProviderAssemblyCleanup()
        {
        }

        // Wrong signature: instance method. Used to validate the existing diagnostic still fires
        // when discovery is driven through the provider path.
        public void ProviderAssemblyInitWithBadSignature(TestContext context)
        {
        }
    }

    // Placeholder test class used purely to drive TypeCache through GetClassInfo → GetAssemblyInfo
    // so that the AssemblyFixtureProvider discovery pass runs. It deliberately has no
    // [AssemblyInitialize]/[AssemblyCleanup] methods.
    // [TestClass] is required so that ReflectHelper.Instance.GetSingleAttributeOrDefault<TestClassAttribute>
    // (called inside CreateClassInfo) finds a real attribute — otherwise DebugEx.Assert fails fatally on
    // .NET Framework Debug builds via Environment.FailFast.
    [TestClass]
    public class DummyHostTestClass
    {
        public void TestMethod()
        {
        }
    }

    // Local test class declared in this assembly, used to verify local declarations win over
    // provider-supplied ones without throwing.
    [TestClass]
    public class LocalTestClassWithLocalFixtures
    {
        public static void LocalAssemblyInit(TestContext context)
        {
        }

        public void TestMethod()
        {
        }
    }
}
