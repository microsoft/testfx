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
        _testCaseDiscoverySink.TestElements.Should().NotBeNull();
        _testCaseDiscoverySink.TestElements.Count.Should().Be(0);
    }

    public async Task SendTestElementShouldAddTheTestElement()
    {
        var testElement = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null));

        await _testCaseDiscoverySink.SendTestElementAsync(testElement);

        _testCaseDiscoverySink.TestElements.Should().NotBeNull();
        _testCaseDiscoverySink.TestElements.Count.Should().Be(1);
        _testCaseDiscoverySink.TestElements.ToArray()[0].Should().BeSameAs(testElement);
    }

    public async Task SendTestElementShouldAddEachTestElementInOrder()
    {
        var testElement1 = new UnitTestElement(new TestMethod("M1", "C", "A", displayName: null));
        var testElement2 = new UnitTestElement(new TestMethod("M2", "C", "A", displayName: null));

        await _testCaseDiscoverySink.SendTestElementAsync(testElement1);
        await _testCaseDiscoverySink.SendTestElementAsync(testElement2);

        _testCaseDiscoverySink.TestElements.Count.Should().Be(2);
        _testCaseDiscoverySink.TestElements.ToArray()[0].Should().BeSameAs(testElement1);
        _testCaseDiscoverySink.TestElements.ToArray()[1].Should().BeSameAs(testElement2);
    }
}
