// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ReflectHelperContinuationTests : TestContainer
{
    public void AssemblySettingsAndAttributeActionsUseTheRegisteredMetadataAndState()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        var services = new TestablePlatformServiceProvider();
        services.SetupMockReflectionOperations();
        Assembly assembly = typeof(ReflectHelperContinuationTests).Assembly;
        var parallel = new ParallelizeAttribute { Workers = 3, Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel };
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, It.IsAny<Type>())).Returns([]);
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, typeof(ParallelizeAttribute))).Returns([parallel]);
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, typeof(DiscoverInternalsAttribute))).Returns([new DiscoverInternalsAttribute()]);
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, typeof(TestDataSourceDiscoveryAttribute)))
            .Returns([new TestDataSourceDiscoveryAttribute(TestDataSourceDiscoveryOption.DuringExecution)]);
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, typeof(TestDataSourceOptionsAttribute)))
            .Returns([new TestDataSourceOptionsAttribute(TestDataSourceUnfoldingStrategy.Fold)]);
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, typeof(DoNotParallelizeAttribute)))
            .Returns([new DoNotParallelizeAttribute()]);
        MethodInfo member = typeof(ReflectHelperContinuationTests).GetMethod(nameof(AssemblySettingsAndAttributeActionsUseTheRegisteredMetadataAndState))!;
        services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(member))
            .Returns([new TestCategoryAttribute("Smoke"), new TestCategoryAttribute("Boundary")]);
        try
        {
            PlatformServiceProvider.Instance = services;
            ParallelizeAttribute selected = ReflectHelper.GetParallelizeAttribute(assembly)!;
            selected.Workers.Should().Be(3);
            selected.Scope.Should().Be(Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel);
            ReflectHelper.HasDiscoverInternalsAttribute(assembly).Should().BeTrue();
            ReflectHelper.GetTestDataSourceDiscoveryOption(assembly).Should().Be(TestDataSourceDiscoveryOption.DuringExecution);
            ReflectHelper.GetTestDataSourceOptions(assembly)!.UnfoldingStrategy.Should().Be(TestDataSourceUnfoldingStrategy.Fold);
            ReflectHelper.IsDoNotParallelizeSet(assembly).Should().BeTrue();
            List<string> state = ["before"];
            ReflectHelper.PerformActionOnAttribute<TestCategoryAttribute, List<string>>(member, (attribute, values) => values!.AddRange(attribute.TestCategories), state);
            state.Should().Equal("before", "Smoke", "Boundary");
            ReflectHelper.Instance.GetAttributes<TestCategoryAttribute>(member).SelectMany(attribute => attribute.TestCategories)
                .Should().Equal("Smoke", "Boundary");

            services.MockReflectionOperations.Setup(value => value.GetCustomAttributes(assembly, It.IsAny<Type>())).Returns([]);
            ReflectHelper.GetParallelizeAttribute(assembly).Should().BeNull();
            ReflectHelper.HasDiscoverInternalsAttribute(assembly).Should().BeFalse();
            ReflectHelper.GetTestDataSourceDiscoveryOption(assembly).Should().BeNull();
            ReflectHelper.GetTestDataSourceOptions(assembly).Should().BeNull();
            ReflectHelper.IsDoNotParallelizeSet(assembly).Should().BeFalse();
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
        }
    }
}
