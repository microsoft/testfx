// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(_testCaseDiscoverySink.Tests.Count == 0);
    }

    public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
    {
        _testCaseDiscoverySink.SendTestCase(null);

        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(_testCaseDiscoverySink.Tests.Count == 0);
    }

    public void SendTestCaseShouldAddTheTestCaseToTests()
    {
        TestCase tc = new("TAttribute", new Uri("executor://TestExecutorUri"), "A");
        _testCaseDiscoverySink.SendTestCase(tc);

        Verify(_testCaseDiscoverySink.Tests is not null);
        Verify(_testCaseDiscoverySink.Tests.Count == 1);
        Verify(tc == _testCaseDiscoverySink.Tests.ToArray()[0]);
    }
}
