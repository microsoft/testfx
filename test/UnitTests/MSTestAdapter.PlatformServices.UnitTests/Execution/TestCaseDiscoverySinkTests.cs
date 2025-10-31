﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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

    public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
    {
        _testCaseDiscoverySink.SendTestCase(null);

        _testCaseDiscoverySink.Tests.Should().NotBeNull();
        _testCaseDiscoverySink.Tests.Count.Should().Be(0);
    }

    public void SendTestCaseShouldAddTheTestCaseToTests()
    {
        TestCase tc = new("TAttribute", new Uri("executor://TestExecutorUri"), "A");
        _testCaseDiscoverySink.SendTestCase(tc);

        _testCaseDiscoverySink.Tests.Should().NotBeNull();
        _testCaseDiscoverySink.Tests.Count.Should().Be(1);
        _testCaseDiscoverySink.Tests.ToArray()[0].Should().Be(tc);
    }
}
