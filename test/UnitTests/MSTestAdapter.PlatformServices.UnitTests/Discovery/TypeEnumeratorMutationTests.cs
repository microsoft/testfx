// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestElement = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class TypeEnumeratorMutationTests : TestContainer
{
    private readonly Mock<ReflectHelper> _mockReflectHelper = new() { CallBase = true };
    private readonly Mock<TypeValidator> _mockTypeValidator;
    private readonly Mock<TestMethodValidator> _mockTestMethodValidator;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider = new();
    private readonly List<string> _warnings = [];
#if !WINDOWS_UWP && !WIN_UI
    private readonly MSTestTelemetryDataCollector? _previousCollector = MSTestTelemetryDataCollector.Current;
#endif

    public TypeEnumeratorMutationTests()
    {
        _mockTypeValidator = new Mock<TypeValidator>(MockBehavior.Default, _mockReflectHelper.Object);
        _mockTestMethodValidator = new Mock<TestMethodValidator>(MockBehavior.Default, _mockReflectHelper.Object, false);
        _mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<List<string>>())).Returns(true);
        _mockTestMethodValidator
            .Setup(tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns(true);

        // Attributes and runtime methods are fully controlled by the tests, so the enumeration order never depends on the runtime.
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
#if !WINDOWS_UWP && !WIN_UI
            MSTestTelemetryDataCollector.Current = _previousCollector;
#endif
        }
    }

#if !WINDOWS_UWP && !WIN_UI
    public void EnumerateTracksTheClassAttributesOfAValidTestClassForTelemetry()
    {
        SetAttributes(typeof(SampleTestClass), new TestClassAttribute(), new DoNotParallelizeAttribute());
        var collector = new MSTestTelemetryDataCollector();
        MSTestTelemetryDataCollector.Current = collector;

        List<UnitTestElement>? tests = CreateTypeEnumerator(typeof(SampleTestClass)).Enumerate(_warnings);

        tests.Should().BeEmpty();
        collector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(
            new Dictionary<string, long>
            {
                ["TestClassAttribute"] = 1,
                ["DoNotParallelizeAttribute"] = 1,
            });
    }

    public void GetTestFromMethodTracksTheMethodAttributesForTelemetry()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(method, new TestMethodAttribute(), new TestCategoryAttribute("first"), new TestCategoryAttribute("second"));
        var collector = new MSTestTelemetryDataCollector();
        MSTestTelemetryDataCollector.Current = collector;

        CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        collector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(
            new Dictionary<string, long>
            {
                ["TestMethodAttribute"] = 1,
                ["TestCategoryAttribute"] = 2,
            });
    }
#endif

    public void GetTestsKeepsTheMethodDeclaredClosestToTheTestClassWhenTheBaseMethodIsEnumeratedFirst()
    {
        MethodInfo baseMethod = typeof(BaseHidingTestClass).GetMethod(nameof(BaseHidingTestClass.HiddenTest))!;
        MethodInfo derivedMethod = typeof(DerivedHidingTestClass).GetMethod(
            nameof(DerivedHidingTestClass.HiddenTest),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        baseMethod.ToString().Should().Be(derivedMethod.ToString(), because: "the two methods must be seen as duplicates");
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(r => r.GetRuntimeMethods(typeof(DerivedHidingTestClass)))
            .Returns([baseMethod, derivedMethod]);

        List<UnitTestElement> tests = CreateTypeEnumerator(typeof(DerivedHidingTestClass)).GetTests(_warnings);

        UnitTestElement test = tests.Should().ContainSingle().Subject;
        test.TestMethod.MethodInfo!.DeclaringType.Should().Be(typeof(DerivedHidingTestClass));
    }

    public void GetTestsKeepsTheMethodDeclaredClosestToTheTestClassWhenTheDerivedMethodIsEnumeratedFirst()
    {
        MethodInfo baseMethod = typeof(BaseHidingTestClass).GetMethod(nameof(BaseHidingTestClass.HiddenTest))!;
        MethodInfo derivedMethod = typeof(DerivedHidingTestClass).GetMethod(
            nameof(DerivedHidingTestClass.HiddenTest),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(r => r.GetRuntimeMethods(typeof(DerivedHidingTestClass)))
            .Returns([derivedMethod, baseMethod]);

        List<UnitTestElement> tests = CreateTypeEnumerator(typeof(DerivedHidingTestClass)).GetTests(_warnings);

        UnitTestElement test = tests.Should().ContainSingle().Subject;
        test.TestMethod.MethodInfo!.DeclaringType.Should().Be(typeof(DerivedHidingTestClass));
    }

    public void GetTestFromMethodSeparatesTheParameterTypesWithCommas()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.TwoParameters))!;

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        test.TestMethod.ParameterTypes.Should().Be("System.Int32,System.String");
    }

    public void GetTestFromMethodKeepsTheWorkItemIdsInDeclarationOrder()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(method, new WorkItemAttribute(123), new WorkItemAttribute(7), new WorkItemAttribute(345));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        test.WorkItemIds.Should().Equal("123", "7", "345");
    }

    public void GetTestFromMethodKeepsEveryResourceLockDeclaredOnTheMethod()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(method, Lock("first"), Lock("second", ResourceAccessMode.Read), Lock("third"));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test).Should().Equal("first:ReadWrite", "second:Read", "third:ReadWrite");
    }

    public void GetTestFromMethodReturnsNoResourceLocksWhenNeitherTheClassNorTheMethodDeclaresAny()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        test.ResourceLocks.Should().BeNull();
    }

    public void GetTestFromMethodReturnsTheResourceLocksDeclaredOnlyOnTheClass()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(typeof(SampleTestClass), Lock("exclusive"), Lock("shared", ResourceAccessMode.Read));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test).Should().Equal("exclusive:ReadWrite", "shared:Read");
    }

    public void GetTestFromMethodReturnsTheResourceLocksDeclaredOnlyOnTheMethod()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(method, Lock("exclusive"), Lock("shared", ResourceAccessMode.Read));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test).Should().Equal("exclusive:ReadWrite", "shared:Read");
    }

    public void GetTestFromMethodMergesTheClassAndMethodResourceLocksSortedByOrdinalResourceName()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(typeof(SampleTestClass), Lock("zeta"), Lock("alpha", ResourceAccessMode.Read));
        SetAttributes(method, Lock("Beta"), Lock("middle"));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test).Should().Equal("Beta:ReadWrite", "alpha:Read", "middle:ReadWrite", "zeta:ReadWrite");
    }

    public void GetTestFromMethodKeepsTheStrongestAccessModeWhenTheSameResourceIsLockedMoreThanOnce()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(
            typeof(SampleTestClass),
            Lock("read-then-write", ResourceAccessMode.Read),
            Lock("write-then-read"),
            Lock("read-then-read", ResourceAccessMode.Read),
            Lock("write-then-write"));
        SetAttributes(
            method,
            Lock("read-then-write"),
            Lock("write-then-read", ResourceAccessMode.Read),
            Lock("read-then-read", ResourceAccessMode.Read),
            Lock("write-then-write"));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test).Should().Equal(
            "read-then-read:Read",
            "read-then-write:ReadWrite",
            "write-then-read:ReadWrite",
            "write-then-write:ReadWrite");
    }

    public void GetTestFromMethodKeepsEveryDependencyDeclaredOnTheMethod()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        SetAttributes(method, new DependsOnAttribute("First"), new DependsOnAttribute("Second") { ProceedOnFailure = true });

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test.Dependencies).Should().Equal("|First|False", "|Second|True");
    }

    public void GetTestFromMethodKeepsTheClassLevelDependencyWhenTheMethodRepeatsTheSameTargetWithTheSameSkipBehavior()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        string targetFullName = typeof(DependencyTargetClass).FullName!;

        // Both declarations describe the same target ("<full name>.*"), but only the class-level one names it as a method.
        SetAttributes(typeof(SampleTestClass), new DependsOnAttribute($"{targetFullName}.*"));
        SetAttributes(method, new DependsOnAttribute(typeof(DependencyTargetClass)));

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test.Dependencies).Should().Equal($"|{targetFullName}.*|False");
    }

    public void GetTestFromMethodKeepsTheClassLevelDependencyWhenTheMethodRepeatsTheSameTargetWithTheSameProceedOnFailureBehavior()
    {
        MethodInfo method = typeof(SampleTestClass).GetMethod(nameof(SampleTestClass.NoParameters))!;
        string targetFullName = typeof(DependencyTargetClass).FullName!;
        SetAttributes(typeof(SampleTestClass), new DependsOnAttribute($"{targetFullName}.*") { ProceedOnFailure = true });
        SetAttributes(method, new DependsOnAttribute(typeof(DependencyTargetClass)) { ProceedOnFailure = true });

        UnitTestElement test = CreateTypeEnumerator(typeof(SampleTestClass)).GetTestFromMethod(method, classDisablesParallelization: false, _warnings);

        Describe(test.Dependencies).Should().Equal($"|{targetFullName}.*|True");
    }

    private static ResourceLockAttribute Lock(string resource, ResourceAccessMode mode = ResourceAccessMode.ReadWrite)
        => new(resource) { Mode = mode };

    private static string[] Describe(UnitTestElement test)
        => test.ResourceLocks!.Select(l => $"{l.Resource}:{l.Mode}").ToArray();

    private static string[] Describe(MSTest.TestAdapter.ObjectModel.TestDependencyInfo[]? dependencies)
        => dependencies!.Select(d => $"{d.TargetClassFullName}|{d.TargetMethodName}|{d.ProceedOnFailure}").ToArray();

    private TypeEnumerator CreateTypeEnumerator(Type type)
        => new(type, "DummyAssembly", _mockReflectHelper.Object, _mockTypeValidator.Object, _mockTestMethodValidator.Object);

    private void SetAttributes(MemberInfo member, params Attribute[] attributes)
        => _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(r => r.GetCustomAttributes(member))
            .Returns(attributes);

    private sealed class SampleTestClass
    {
        public void NoParameters()
        {
        }

        public void TwoParameters(int number, string text)
        {
            _ = number;
            _ = text;
        }
    }

    private class BaseHidingTestClass
    {
        public void HiddenTest()
        {
        }
    }

    private sealed class DerivedHidingTestClass : BaseHidingTestClass
    {
        public new void HiddenTest()
        {
        }
    }

    private sealed class DependencyTargetClass
    {
    }
}
