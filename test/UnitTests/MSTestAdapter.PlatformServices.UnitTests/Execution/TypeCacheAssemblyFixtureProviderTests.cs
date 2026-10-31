// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheAssemblyFixtureProviderTests : TestContainer
{
    public void ProviderMarkerFailureKeepsOriginalCauseWhenAssemblyNameCannotBeInspected()
    {
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        foreach (Exception nameFailure in new Exception[]
        {
            new FileNotFoundException("name unavailable"),
            new FileLoadException("name unavailable"),
            new BadImageFormatException("name unavailable"),
            new System.Security.SecurityException("name unavailable"),
            new NotSupportedException("name unavailable"),
        })
        {
            NameUnavailableAssembly assembly = new(nameFailure);
            TypeLoadException cause = new("attribute unavailable");
            _testablePlatformServiceProvider.MockReflectionOperations
                .Setup(value => value.GetCustomAttributes(assembly, typeof(AssemblyFixtureProviderAttribute)))
                .Throws(cause);
            Action load = () => TypeCache.LoadProviderMarkers(assembly);

            TypeInspectionException actual = load.Should().ThrowExactly<TypeInspectionException>().Which;
            actual.Message.Should().Be(string.Format(
                CultureInfo.CurrentCulture, Resource.UTA_AssemblyFixtureProviderLoadFailed, "<unknown>", "attribute unavailable"));
            actual.InnerException.Should().BeSameAs(cause);
            assembly.NameReads.Should().Be(1);
        }
    }

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

    public void GetTestMethodInfoShouldThrowWhenTwoProvidersContributeAssemblyInitialize()
    {
        Type hostType = typeof(DummyHostTestClass);
        MethodInfo firstInit = typeof(DummyFixtureProvider).GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyInit))!;
        MethodInfo secondInit = typeof(SecondDummyFixtureProvider).GetMethod(nameof(SecondDummyFixtureProvider.SecondProviderAssemblyInit))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(firstInit))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(secondInit))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");

        Action act = () => _typeCache.GetTestMethodInfo(testMethod);
        // Both providers contribute an [AssemblyInitialize] method — the TestAssemblyInfo setter
        // throws UTA013 (UTA_ErrorMultiAssemblyInit) on the second assignment.
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA013*");
    }

    public void GetTestMethodInfoShouldThrowWhenTwoProvidersContributeAssemblyCleanup()
    {
        Type hostType = typeof(DummyHostTestClass);
        MethodInfo firstCleanup = typeof(DummyFixtureProvider).GetMethod(nameof(DummyFixtureProvider.ProviderAssemblyCleanup))!;
        MethodInfo secondCleanup = typeof(SecondDummyFixtureProvider).GetMethod(nameof(SecondDummyFixtureProvider.SecondProviderAssemblyCleanup))!;

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(firstCleanup))
            .Returns(true);
        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<AssemblyCleanupAttribute>(secondCleanup))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");

        Action act = () => _typeCache.GetTestMethodInfo(testMethod);
        // Both providers contribute an [AssemblyCleanup] method — the TestAssemblyInfo setter
        // throws UTA014 (UTA_ErrorMultiAssemblyClean) on the second assignment.
        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA014*");
    }

    public void GetTestMethodInfoShouldNotThrowWhenProviderHasNoFixtureMethods()
    {
        // The second provider is also registered at the assembly level, but no test sets up its
        // methods as fixture methods. Plain discovery should treat this marker as a no-op.
        Type hostType = typeof(DummyHostTestClass);

        _mockReflectHelper
            .Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(hostType))
            .Returns(true);

        TestMethod testMethod = CreateTestMethod(nameof(DummyHostTestClass.TestMethod), hostType.FullName!, "A");
        _typeCache.GetTestMethodInfo(testMethod);

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.AssemblyInitializeMethod.Should().BeNull();
        info.AssemblyCleanupMethod.Should().BeNull();
    }

    // Direct unit tests for the small, extracted helpers used by the discovery pipeline.
    // Driving these branches through GetTestMethodInfo would require polluting AssemblyAttributes.cs
    // with broken markers, which would then fire for every other test in this class. The helpers
    // are scoped, internal, and self-contained, so testing them directly is both safer and clearer.
    public void ProcessProviderFixtureTypeShouldThrowUTA070ForOpenGenericFixtureType()
    {
        TestAssemblyInfo assemblyInfo = new(Assembly.GetExecutingAssembly());

        Action act = () => TypeCache.ProcessProviderFixtureType(
            typeof(GenericFixtureProvider<>),
            assemblyInfo,
            _typeCache,
            localProvidedInit: false,
            localProvidedCleanup: false);

        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA070*");
    }

    public void ProcessProviderFixtureTypeShouldThrowUTA070ForClosedGenericFixtureType()
    {
        TestAssemblyInfo assemblyInfo = new(Assembly.GetExecutingAssembly());

        Action act = () => TypeCache.ProcessProviderFixtureType(
            typeof(GenericFixtureProvider<int>),
            assemblyInfo,
            _typeCache,
            localProvidedInit: false,
            localProvidedCleanup: false);

        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA070*");
    }

    public void CollectFixtureMethodsFromProviderTypeShouldThrowUTA071WhenMethodEnumerationFails()
    {
        // Force GetDeclaredMethods to throw for the provider type so we can observe how the
        // extracted helper surfaces a reflection failure as UTA071 — i.e. the explicit opt-in
        // marker does not silently disappear when method inspection blows up.
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetDeclaredMethods(typeof(DummyFixtureProvider)))
            .Throws(new InvalidOperationException("Simulated reflection failure"));

        TestAssemblyInfo assemblyInfo = new(Assembly.GetExecutingAssembly());

        Action act = () => TypeCache.CollectFixtureMethodsFromProviderType(
            typeof(DummyFixtureProvider),
            assemblyInfo,
            _typeCache,
            localProvidedInit: false,
            localProvidedCleanup: false);

        act.Should().Throw<TypeInspectionException>().WithMessage("*UTA071*");
    }

    public void LoadProviderMarkersShouldThrowUTA072WhenAttributeInstantiationFails()
    {
        // Force GetCustomAttributes to throw — this models the case where CustomAttributeData
        // confirmed the marker is present but the attribute itself cannot be instantiated
        // (typically a typeof(...) argument whose target assembly fails to resolve). Explicit
        // opt-in markers must not silently disappear here, so the helper raises UTA072.
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        var assembly = Assembly.GetExecutingAssembly();
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(assembly, typeof(AssemblyFixtureProviderAttribute)))
            .Throws(new TypeLoadException("Simulated attribute load failure"));

        Action act = () => TypeCache.LoadProviderMarkers(assembly);

        act.Should().Throw<TypeInspectionException>()
            .WithMessage("*UTA072*")
            .WithInnerException(typeof(TypeLoadException));
    }

    private sealed class NameUnavailableAssembly(Exception failure) : Assembly
    {
        public int NameReads { get; private set; }

        public override string FullName => "name-unavailable-fixture";

        public override AssemblyName GetName()
        {
            NameReads++;
            throw failure;
        }
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

    // Second fixture provider, also registered via assembly-level marker. Used exclusively by the
    // cross-provider-duplicate tests so that the assembly is observed with two providers, each
    // contributing fixture methods.
    public class SecondDummyFixtureProvider
    {
        public static void SecondProviderAssemblyInit(TestContext context)
        {
        }

        public static void SecondProviderAssemblyCleanup()
        {
        }
    }

    // Generic fixture provider type used by the UTA070 tests. Both the open form
    // (typeof(GenericFixtureProvider<>)) and a closed form (typeof(GenericFixtureProvider<int>))
    // must be rejected — the public contract is that the fixture type must be non-generic.
    public class GenericFixtureProvider<T>
    {
        public static void ProviderAssemblyInit(TestContext context)
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

    public void ProviderProbeAndReferenceFailuresDoNotSuppressOtherCandidates()
    {
        foreach (bool metadataFailure in new[] { true, false })
        {
            var assembly = new FaultingGraphAssembly(metadataFailure);
            Type type = new AssemblyOwnedType(typeof(GraphDriver), assembly);
            _testablePlatformServiceProvider.SetupMockReflectionOperations();
            _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetType(typeof(GraphDriver).FullName!)).Returns(type);
            _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDefinedTypes(assembly)).Returns([type]);
            _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(type)).Returns([new TestClassAttribute()]);
            _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDeclaredConstructors(type))
                .Returns(typeof(GraphDriver).GetConstructors());
            _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDeclaredMethods(type))
                .Returns(typeof(GraphDriver).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
            _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);
            _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);
            List<(string Format, object?[] Arguments)> warnings = [];
            _testablePlatformServiceProvider.MockTraceLogger.Setup(value => value.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
                .Callback<string, object?[]>((format, arguments) => warnings.Add((format, arguments)));
            var cache = new TypeCache(_mockReflectHelper.Object);
            TestMethod method = CreateTestMethod(nameof(GraphDriver.Test), typeof(GraphDriver).FullName!, "graph-source");
            method.MethodInfo = typeof(GraphDriver).GetMethod(nameof(GraphDriver.Test));

            TestMethodInfo resolved = cache.GetTestMethodInfo(method)!;

            resolved.MethodInfo.Should().BeSameAs(typeof(GraphDriver).GetMethod(nameof(GraphDriver.Test)));
            warnings.Should().ContainSingle();
            warnings[0].Format.Should().Be(metadataFailure
                ? "TypeCache: Exception occurred while probing AssemblyFixtureProviderAttribute metadata from assembly {0}. {1}"
                : "TypeCache: Exception occurred while enumerating referenced assemblies of {0} for AssemblyFixtureProvider discovery. {1}");
            warnings[0].Arguments[0].Should().Be("CoverageFixtureGraph");
            warnings[0].Arguments[1].Should().BeSameAs(assembly.Failure);
        }
    }

    private sealed class AssemblyOwnedType(Type type, Assembly assembly) : TypeDelegator(type)
    {
        public override Assembly Assembly => assembly;

        public override IList<CustomAttributeData> GetCustomAttributesData() => UnderlyingSystemType.GetCustomAttributesData();
    }

    private sealed class FaultingGraphAssembly(bool metadataFailure, AssemblyName[]? references = null) : Assembly
    {
        internal Exception Failure { get; } = new TypeLoadException("provider graph metadata unavailable");

        public override string FullName => "CoverageFixtureGraph";

        public override AssemblyName GetName() => new("CoverageFixtureGraph");

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<Attribute>();

        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<Attribute>();

        public override bool IsDefined(Type attributeType, bool inherit) => false;

        public override IList<CustomAttributeData> GetCustomAttributesData()
            => metadataFailure ? throw Failure : [];

        public override AssemblyName[] GetReferencedAssemblies()
            => references ?? (metadataFailure ? [] : throw Failure);
    }

    [TestClass]
    public sealed class GraphDriver
    {
        public void Test()
        {
        }
    }

    public async Task GlobalFixtureDiscoverySelectsOnlyValidSignatures()
    {
        Type host = typeof(DummyHostTestClass);
        Type fixture = typeof(TypeCacheGlobalContinuationFixture);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(host)).Returns(true);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(fixture)).Returns(true);
        MethodInfo initialize = fixture.GetMethod(nameof(TypeCacheGlobalContinuationFixture.Initialize))!;
        MethodInfo cleanup = fixture.GetMethod(nameof(TypeCacheGlobalContinuationFixture.Cleanup))!;
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestInitializeAttribute>(initialize)).Returns(true);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestCleanupAttribute>(cleanup)).Returns(true);
        foreach (MethodInfo invalid in fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.StartsWith("Invalid", StringComparison.Ordinal)))
        {
            _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestInitializeAttribute>(invalid)).Returns(true);
            _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestCleanupAttribute>(invalid)).Returns(true);
        }

        TestMethodInfo methodInfo = _typeCache.GetTestMethodInfo(CreateTestMethod(nameof(DummyHostTestClass.TestMethod), host.FullName!, "A"))!;

        TestAssemblyInfo info = _typeCache.AssemblyInfoCache.Single();
        info.GlobalTestInitializations.Count.Should().Be(1);
        info.GlobalTestCleanups.Count.Should().Be(1);
        List<string> events = [];
        methodInfo.TestContext = new TestContextImplementation(null, host.FullName,
            new Dictionary<string, object?> { ["global-events"] = events }, null, null);
        methodInfo.TimeoutInfo = TimeoutInfo.FromTimeout(0);
        TestResult result = await methodInfo.InvokeAsync(null);
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        events.Should().Equal("initialize", "cleanup");
    }

    public async Task UninspectableTypesDoNotHideLaterGlobalFixtureMethods()
    {
        Type host = typeof(DummyHostTestClass);
        Type fixture = typeof(TypeCacheGlobalContinuationFixture);
        var cause = new TypeLoadException("unrelated class attribute unavailable");
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(host)).Throws(cause);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(fixture)).Returns(true);
        MethodInfo initialize = fixture.GetMethod(nameof(TypeCacheGlobalContinuationFixture.Initialize))!;
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestInitializeAttribute>(initialize)).Returns(true);
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);

        TestMethodInfo methodInfo = _typeCache.GetTestMethodInfo(CreateTestMethod(nameof(DummyHostTestClass.TestMethod), host.FullName!, "A"))!;

        _typeCache.AssemblyInfoCache.Single().GlobalTestInitializations.Count.Should().Be(1);
        List<string> events = [];
        methodInfo.TestContext = new TestContextImplementation(null, host.FullName,
            new Dictionary<string, object?> { ["global-events"] = events }, null, null);
        methodInfo.TimeoutInfo = TimeoutInfo.FromTimeout(0);
        (await methodInfo.InvokeAsync(null)).Outcome.Should().Be(UnitTestOutcome.Passed);
        events.Should().Equal("initialize");
        _testablePlatformServiceProvider.MockTraceLogger.Verify(value => value.Warning(
            "TypeCache: Exception occurred while checking whether type {0} is a test class or not. {1}", host.FullName, cause), Times.Once);
    }

    public async Task GlobalInitializationCancellationStopsBeforeTheTestBody()
    {
        Type host = typeof(DummyHostTestClass);
        Type fixture = typeof(TypeCacheGlobalContinuationFixture);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(host)).Returns(true);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(fixture)).Returns(true);
        MethodInfo initialize = fixture.GetMethod(nameof(TypeCacheGlobalContinuationFixture.Initialize))!;
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<GlobalTestInitializeAttribute>(initialize)).Returns(true);
        TestMethodInfo method = _typeCache.GetTestMethodInfo(CreateTestMethod(nameof(DummyHostTestClass.TestMethod), host.FullName!, "A"))!;
        List<string> events = [];
        var context = new TestContextImplementation(null, host.FullName,
            new Dictionary<string, object?> { ["global-events"] = events }, null, null);
        context.CancellationTokenSource.Cancel();
        method.TestContext = context;
        method.TimeoutInfo = TimeoutInfo.FromTimeout(0);

        TestResult result = await method.InvokeAsync(null);

        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.ExceptionMessage.Should().Be(string.Format(
            CultureInfo.InvariantCulture, Resource.GlobalTestInitializeWasCancelled, fixture.FullName, nameof(TypeCacheGlobalContinuationFixture.Initialize)));
        events.Should().BeEmpty();
    }

#if NETFRAMEWORK
    public void MissingProviderReferenceIsWarnedWithoutLosingTheConsumerTest()
    {
        var missing = new AssemblyName("Coverage.Missing.Provider." + Guid.NewGuid().ToString("N"));
        var assembly = new FaultingGraphAssembly(false, [missing]);
        Type type = new AssemblyOwnedType(typeof(GraphDriver), assembly);
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetType(typeof(GraphDriver).FullName!)).Returns(type);
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDefinedTypes(assembly)).Returns([type]);
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetCustomAttributes(type)).Returns([new TestClassAttribute()]);
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDeclaredConstructors(type)).Returns(typeof(GraphDriver).GetConstructors());
        _testablePlatformServiceProvider.MockReflectionOperations.Setup(value => value.GetDeclaredMethods(type))
            .Returns(typeof(GraphDriver).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);
        _mockReflectHelper.Setup(value => value.IsAttributeDefined<TestClassAttribute>(type)).Returns(true);
        List<object?[]> diagnostics = [];
        _testablePlatformServiceProvider.MockTraceLogger.Setup(value => value.Warning(
            "TypeCache: Could not load referenced assembly {0} for AssemblyFixtureProvider discovery. {1}", It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, arguments) => diagnostics.Add(arguments));
        var cache = new TypeCache(_mockReflectHelper.Object);
        TestMethod method = CreateTestMethod(nameof(GraphDriver.Test), typeof(GraphDriver).FullName!, "graph-source");
        method.MethodInfo = typeof(GraphDriver).GetMethod(nameof(GraphDriver.Test));

        TestMethodInfo resolved = cache.GetTestMethodInfo(method)!;

        resolved.MethodInfo.Should().BeSameAs(typeof(GraphDriver).GetMethod(nameof(GraphDriver.Test)));
        diagnostics.Should().ContainSingle();
        diagnostics[0][0].Should().Be(missing.FullName);
        diagnostics[0][1].Should().BeOfType<FileNotFoundException>();
    }
#endif
}

public sealed class TypeCacheGlobalContinuationFixture
{
    public static void Initialize(TestContext context)
    {
        ((List<string>)context.Properties["global-events"]!).Add("initialize");
    }

    public static Task Cleanup(TestContext context)
    {
        ((List<string>)context.Properties["global-events"]!).Add("cleanup");
        return Task.CompletedTask;
    }

    public static void InvalidGeneric<T>(TestContext context)
    {
    }

    public static void InvalidNoContext()
    {
        ((List<string>)TestContext.Current!.Properties["global-events"]!).Add("invalid-no-context");
    }

    public void InvalidInstance(TestContext context)
    {
    }

    public static int InvalidReturn(TestContext context)
    {
        ((List<string>)context.Properties["global-events"]!).Add("invalid-return");
        return 17;
    }
}
