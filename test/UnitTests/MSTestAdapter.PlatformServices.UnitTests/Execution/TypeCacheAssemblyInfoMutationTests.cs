// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheAssemblyInfoMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";

    private readonly TypeCache _typeCache;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheAssemblyInfoMutationTests()
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

    public void AssemblyFixturesShouldNotBeCollectedFromATypeWhoseTestClassAttributeCannotBeInspected()
    {
        Type uninspectableType = typeof(UninspectableFixtureHolder);
        MethodInfo initialize = uninspectableType.GetMethod(nameof(UninspectableFixtureHolder.Initialize))!;
        var cause = new TypeLoadException("class attributes cannot be loaded");
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<TestClassAttribute>(uninspectableType)).Throws(cause);
        _mockReflectHelper.Setup(rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(initialize)).Returns(true);
        _testablePlatformServiceProvider.MockTraceLogger.Setup(l => l.IsWarningEnabled).Returns(true);

        _typeCache.GetTestMethodInfo(CreateTestMethod(typeof(HostTestClass))).Should().NotBeNull();

        TestAssemblyInfo assemblyInfo = _typeCache.AssemblyInfoCache.Single();
        assemblyInfo.AssemblyInitializeMethod.Should().BeNull("a type that cannot be inspected is skipped entirely");
        _testablePlatformServiceProvider.MockTraceLogger.Verify(
            l => l.Warning(
                "TypeCache: Exception occurred while checking whether type {0} is a test class or not. {1}",
                uninspectableType.FullName,
                cause),
            Times.Once);
    }

#if DEBUG && !NETFRAMEWORK
    public void UpdateInfoWithInitializeAndCleanupMethodsShouldAssertThatExactlyTwoMethodSlotsAreProvided()
    {
        MethodInfo updateMethod = typeof(TypeCache).GetMethod("UpdateInfoWithInitializeAndCleanupMethods", BindingFlags.NonPublic | BindingFlags.Static)!;
        var classInfo = new TestClassInfo(
            typeof(HostTestClass),
            typeof(HostTestClass).GetConstructor(Type.EmptyTypes),
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(HostTestClass).Assembly));
        object?[] arguments = [classInfo, new MethodInfo?[3]];
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        updateMethod.Invoke(null, arguments);

        assertions.Should().Equal("initAndCleanupMethods.Length == 2");
        arguments[1].Should().BeOfType<MethodInfo[]>().Which.Should().HaveCount(2);
    }
#endif

    private static TestMethod CreateTestMethod(Type type)
        => new(TestMethodName, null, TestMethodName, type.FullName!, "A", null, null)
        {
            MethodInfo = type.GetMethod(TestMethodName),
        };

    [TestClass]
    public class HostTestClass
    {
        public void TestMethod()
        {
        }
    }

    public static class UninspectableFixtureHolder
    {
        public static void Initialize(TestContext context)
        {
        }
    }
}
