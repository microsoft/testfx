// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

public class TestCaseDiscoverySinkTests : TestContainer
{
    private TestCaseDiscoverySink _testCaseDiscoverySink;

    public TestCaseDiscoverySinkTests()
    {
        _testCaseDiscoverySink = new TestCaseDiscoverySink();
    }

    public void TestCaseDiscoverySinkConstructorShouldInitializeTests()
    {
        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(0 == _testCaseDiscoverySink.Tests.Count);
    }

    public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
    {
        _testCaseDiscoverySink.SendTestCase(null);

        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(0 == _testCaseDiscoverySink.Tests.Count);
    }

    public void SendTestCaseShouldAddTheTestCaseToTests()
    {
        TestCase tc = new("T", new Uri("executor://TestExecutorUri"), "A");
        _testCaseDiscoverySink.SendTestCase(tc);

        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(1 == _testCaseDiscoverySink.Tests.Count);
        Verify(tc == _testCaseDiscoverySink.Tests.ToArray()[0]);
    }
}
