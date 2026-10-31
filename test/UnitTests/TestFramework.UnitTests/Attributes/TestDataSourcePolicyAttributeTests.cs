// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class TestDataSourcePolicyAttributeTests : TestContainer
{
    public void DataSourcePolicies_PreserveDocumentedAndUnknownOptionsAndAssemblyUsage()
    {
        TestDataSourceDiscoveryOption[] discoveryOptions =
        [
            TestDataSourceDiscoveryOption.DuringExecution,
            TestDataSourceDiscoveryOption.DuringDiscovery,
            (TestDataSourceDiscoveryOption)99,
        ];
        foreach (TestDataSourceDiscoveryOption option in discoveryOptions)
        {
            var attribute = new TestDataSourceDiscoveryAttribute(option);
            ((int)attribute.DiscoveryOption).Should().Be((int)option);
        }

        TestDataSourceUnfoldingStrategy[] strategies =
        [
            TestDataSourceUnfoldingStrategy.Auto,
            TestDataSourceUnfoldingStrategy.Unfold,
            TestDataSourceUnfoldingStrategy.Fold,
            (TestDataSourceUnfoldingStrategy)255,
        ];
        foreach (TestDataSourceUnfoldingStrategy strategy in strategies)
        {
            var attribute = new TestDataSourceOptionsAttribute(strategy);
            ((byte)attribute.UnfoldingStrategy).Should().Be((byte)strategy);
        }

        AttributeUsageAttribute discoveryUsage = typeof(TestDataSourceDiscoveryAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        AttributeUsageAttribute optionsUsage = typeof(TestDataSourceOptionsAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        discoveryUsage.ValidOn.Should().Be(AttributeTargets.Assembly);
        discoveryUsage.AllowMultiple.Should().BeFalse();
        discoveryUsage.Inherited.Should().BeTrue();
        optionsUsage.ValidOn.Should().Be(AttributeTargets.Assembly);
        optionsUsage.AllowMultiple.Should().BeFalse();
        optionsUsage.Inherited.Should().BeFalse();
    }
}
