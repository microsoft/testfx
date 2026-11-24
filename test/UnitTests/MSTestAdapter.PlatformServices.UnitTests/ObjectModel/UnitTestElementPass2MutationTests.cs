// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public sealed class UnitTestElementPass2MutationTests : TestContainer
{
    public void ANewElementSupportsTheExecutionActivityLeaseByDefault()
    {
        var element = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null));

        element.SupportsExecutionActivityLease.Should().BeTrue();
    }

    public void TheExecutionActivityLeaseSupportSurvivesEveryKindOfClone()
    {
        foreach (bool supportsLease in new[] { true, false })
        {
            var original = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))
            {
                SupportsExecutionActivityLease = supportsLease,
            };

            original.Clone().SupportsExecutionActivityLease.Should().Be(supportsLease, because: $"Clone must keep the value {supportsLease}");
            original.CloneWithSource("B").SupportsExecutionActivityLease.Should().Be(supportsLease, because: $"CloneWithSource must keep the value {supportsLease}");
            original.CloneWithUpdatedSource("B").SupportsExecutionActivityLease.Should().Be(supportsLease, because: $"CloneWithUpdatedSource must keep the value {supportsLease}");
        }
    }
}
