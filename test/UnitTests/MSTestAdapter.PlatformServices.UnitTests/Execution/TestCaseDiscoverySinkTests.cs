// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestCaseDiscoverySinkTests : TestContainer
{
    private readonly TestCaseDiscoverySink _testCaseDiscoverySink;

    public TestCaseDiscoverySinkTests() => _testCaseDiscoverySink = new TestCaseDiscoverySink();

    public void TestCaseDiscoverySinkConstructorShouldInitializeTests()
    {
        _testCaseDiscoverySink.Tests.Should().NotBeNull();
        _testCaseDiscoverySink.Tests.Count.Should().Be(0);
    }

    public void SendTestElementShouldAddTheMaterializedTestCaseToTests()
    {
        var testElement = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null));

        _testCaseDiscoverySink.SendTestElement(testElement);

        _testCaseDiscoverySink.Tests.Should().NotBeNull();
        _testCaseDiscoverySink.Tests.Count.Should().Be(1);
        _testCaseDiscoverySink.Tests.ToArray()[0].FullyQualifiedName.Should().Be("C.M");
    }

    public void SendTestElementShouldAddEachTestCaseInOrder()
    {
        var testElement1 = new UnitTestElement(new TestMethod("M1", "C", "A", displayName: null));
        var testElement2 = new UnitTestElement(new TestMethod("M2", "C", "A", displayName: null));

        _testCaseDiscoverySink.SendTestElement(testElement1);
        _testCaseDiscoverySink.SendTestElement(testElement2);

        _testCaseDiscoverySink.Tests.Count.Should().Be(2);
        _testCaseDiscoverySink.Tests.ToArray()[0].FullyQualifiedName.Should().Be("C.M1");
        _testCaseDiscoverySink.Tests.ToArray()[1].FullyQualifiedName.Should().Be("C.M2");
    }
}
