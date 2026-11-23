// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public sealed class UnitTestElementMutationTests : TestContainer
{
#if DEBUG && !NETFRAMEWORK
    public void Constructor_WhenTheTestMethodHasNoClassName_ReportsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        var element = new UnitTestElement(new TestMethod("M", null!, "A", displayName: null));

        assertions.Should().Equal("Full className cannot be empty");
        element.TestMethod.Name.Should().Be("M");
    }

    public void Constructor_WhenTheTestMethodHasAClassName_DoesNotAssert()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null));

        assertions.Should().BeEmpty();
    }
#endif

    public void CloneWithUpdatedSource_ReturnsACloneThatTargetsTheNewSourceAndForgetsTheCachedTestId()
    {
        var testMethod = new TestMethod("M", "C", "OldAssembly", displayName: null);
        var cachedUid = Guid.NewGuid();
        var original = new UnitTestElement(testMethod)
        {
            CachedTestNodeUid = cachedUid,
            Priority = 3,
        };

        UnitTestElement clone = original.CloneWithUpdatedSource("NewAssembly");

        clone.Should().NotBeNull();
        clone.Should().NotBeSameAs(original);
        clone.TestMethod.Should().NotBeSameAs(testMethod);
        clone.TestMethod.AssemblyName.Should().Be("NewAssembly");
        clone.TestMethod.Name.Should().Be("M");
        clone.CachedTestNodeUid.Should().BeNull("the cached identifier is derived from the assembly name that just changed");
        clone.Priority.Should().Be(3);
        original.TestMethod.Should().BeSameAs(testMethod);
        original.TestMethod.AssemblyName.Should().Be("OldAssembly");
        original.CachedTestNodeUid.Should().Be(cachedUid);
    }
}
