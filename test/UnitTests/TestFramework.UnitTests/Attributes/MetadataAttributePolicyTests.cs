// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class MetadataAttributePolicyTests : TestContainer
{
    public void DiscoverInternalsAttribute_IsSealed()
        => typeof(DiscoverInternalsAttribute).IsSealed.Should().BeTrue("internal discovery is a concrete policy, not an extension point");

    public void DoNotParallelizeAttribute_IsSealed()
        => typeof(DoNotParallelizeAttribute).IsSealed.Should().BeTrue("the parallelization opt-out is a concrete policy");

    public void ParallelizeAttribute_IsSealed()
        => typeof(ParallelizeAttribute).IsSealed.Should().BeTrue("parallelization settings are configured without attribute inheritance");

    public void TestDataSourceDiscoveryAttribute_IsSealed()
        => typeof(TestDataSourceDiscoveryAttribute).IsSealed.Should().BeTrue("data-source discovery is a concrete policy");

    public void STATestClassAttribute_IsSealed()
        => typeof(STATestClassAttribute).IsSealed.Should().BeTrue("custom class execution extends TestClassAttribute instead");

    public void DataRowAttribute_IsSealed()
        => typeof(DataRowAttribute).IsSealed.Should().BeTrue("custom data sources implement ITestDataSource instead");

#if WIN_UI
    public void WinUITestTargetAttribute_IsSealed()
        => typeof(Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.WinUITestTargetAttribute).IsSealed.Should()
            .BeTrue("the WinUI application target is a concrete policy");

    public void UITestMethodAttribute_IsSealed()
        => typeof(Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethodAttribute).IsSealed.Should()
            .BeTrue("custom method execution extends TestMethodAttribute instead");
#endif

    public void DataTestMethodAttribute_IsAbsentFromFrameworkAssembly()
        => typeof(TestMethodAttribute).Assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute")
            .Should().BeNull("MSTest 5 uses TestMethodAttribute for both ordinary and data-driven tests");

    public void TestClassAttribute_RemainsExtensible()
    {
        typeof(TestClassAttribute).IsSealed.Should().BeFalse("custom test classes remain supported");
        MethodInfo selectionHook = typeof(TestClassAttribute).GetMethod(nameof(TestClassAttribute.GetTestMethodAttribute))!;
        selectionHook.IsVirtual.Should().BeTrue();
        selectionHook.IsFinal.Should().BeFalse();
    }

    public void TestMethodAttribute_RemainsExtensible()
    {
        typeof(TestMethodAttribute).IsSealed.Should().BeFalse("custom test methods remain supported");
        MethodInfo executionHook = typeof(TestMethodAttribute).GetMethod(nameof(TestMethodAttribute.ExecuteAsync))!;
        executionHook.IsVirtual.Should().BeTrue();
        executionHook.IsFinal.Should().BeFalse();
    }

    public void LifecyclePolicies_PreserveInheritanceFixtureAndParallelSelection()
    {
        new ClassInitializeAttribute().InheritanceBehavior.Should().Be(InheritanceBehavior.None);
        new ClassCleanupAttribute().InheritanceBehavior.Should().Be(InheritanceBehavior.None);
        new ClassInitializeAttribute(InheritanceBehavior.BeforeEachDerivedClass).InheritanceBehavior
            .Should().Be(InheritanceBehavior.BeforeEachDerivedClass);
        new ClassCleanupAttribute(InheritanceBehavior.BeforeEachDerivedClass).InheritanceBehavior
            .Should().Be(InheritanceBehavior.BeforeEachDerivedClass);
        typeof(ClassInitializeAttribute).GetCustomAttribute<AttributeUsageAttribute>()!.Inherited.Should().BeFalse();
        typeof(ClassCleanupAttribute).GetCustomAttribute<AttributeUsageAttribute>()!.Inherited.Should().BeFalse();

        var parallel = new ParallelizeAttribute();
        parallel.Workers.Should().Be(0);
        parallel.Scope.Should().Be(ExecutionScope.ClassLevel);
        parallel.Workers = 3;
        parallel.Scope = ExecutionScope.MethodLevel;
        parallel.Workers.Should().Be(3);
        parallel.Scope.Should().Be(ExecutionScope.MethodLevel);

        var provider = new AssemblyFixtureProviderAttribute(typeof(OwnedFixture));
        var fixture = (OwnedFixture)Activator.CreateInstance(provider.FixtureType)!;
        fixture.Disposed.Should().BeFalse();
        fixture.Dispose();
        fixture.Disposed.Should().BeTrue();
        Action nullType = () => _ = new AssemblyFixtureProviderAttribute(null!);
        nullType.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("fixtureType");
        AttributeUsageAttribute usage = typeof(AssemblyFixtureProviderAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
        usage.Inherited.Should().BeFalse();
    }

    public void CategoryAndPropertyMetadata_PreserveDuplicatesNullAndOrdinalData()
    {
        TestCategoryAttribute[] categories = [new("Nightly"), new("nightly"), new("Nightly"), new(string.Empty)];
        categories.SelectMany(category => category.TestCategories).Should().Equal("Nightly", "nightly", "Nightly", string.Empty);
        new TestCategoryAttribute(null!).TestCategories.Should().ContainSingle().Which.Should().BeNull();

        TestPropertyAttribute[] properties = [new("Owner", "one"), new("Owner", "two"), new("owner", "three")];
        properties.Select(property => (property.Name, property.Value)).Should().Equal(
            ("Owner", "one"), ("Owner", "two"), ("owner", "three"));
        var invalidDiscoveryMetadata = new TestPropertyAttribute(null!, null!);
        invalidDiscoveryMetadata.Name.Should().BeNull();
        invalidDiscoveryMetadata.Value.Should().BeNull();
    }

    public void PriorityMetadata_UsesInvariantValuesUnderCustomNegativeSign()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        try
        {
            CultureInfo.CurrentCulture = culture;
            (int Value, string Text)[] cases = [(-37, "-37"), (0, "0"), (int.MaxValue, "2147483647")];
            foreach ((int value, string text) in cases)
            {
                var priority = new PriorityAttribute(value);
                priority.Priority.Should().Be(value);
                priority.Name.Should().Be("Priority");
                priority.Value.Should().Be(text);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    public void TimeoutPolicy_BoundariesAndExplicitCooperativeOverrideRemainDistinct()
    {
        (int Value, bool Valid)[] cases = [(-1, false), (0, false), (1, true), (int.MaxValue, true)];
        foreach ((int value, bool valid) in cases)
        {
            var timeout = new TimeoutAttribute(value);
            timeout.Timeout.Should().Be(value);
            timeout.HasCorrectTimeout.Should().Be(valid);
            timeout.CooperativeCancellation.Should().BeFalse();
            timeout.IsCooperativeCancellationSet.Should().BeFalse();
            timeout.CooperativeCancellation = false;
            timeout.CooperativeCancellation.Should().BeFalse();
            timeout.IsCooperativeCancellationSet.Should().BeTrue();
            timeout.CooperativeCancellation = true;
            timeout.CooperativeCancellation.Should().BeTrue();
            timeout.IsCooperativeCancellationSet.Should().BeTrue();
        }
    }

    private sealed class OwnedFixture : IDisposable
    {
        public OwnedFixture()
        {
        }

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    public void OwnerMetadata_PreservesOpaqueNullAndDuplicateValues()
    {
        (string? Input, string Expected)[] cases =
        [
            ("primary", "primary"),
            ("PRIMARY", "PRIMARY"),
            ("primary", "primary"),
            (null, string.Empty),
            (string.Empty, string.Empty),
        ];
        var properties = new List<TestPropertyAttribute>();
        foreach ((string? input, string expected) in cases)
        {
            var owner = new OwnerAttribute(input);
            owner.Owner.Should().Be(expected);
            owner.Name.Should().Be("Owner");
            owner.Value.Should().Be(expected);
            properties.Add(owner);
        }

        properties.Select(property => property.Value).Should().Equal("primary", "PRIMARY", "primary", string.Empty, string.Empty);
    }
}
