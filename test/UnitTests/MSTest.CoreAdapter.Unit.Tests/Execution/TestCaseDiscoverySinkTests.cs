// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class TestCaseDiscoverySinkTests
{
    private TestCaseDiscoverySink _testCaseDiscoverySink;

    [TestInitialize]
    public void TestInit() => _testCaseDiscoverySink = new TestCaseDiscoverySink();

    [TestMethod]
    public void TestCaseDiscoverySinkConstructorShouldInitializeTests()
    {
        Assert.IsNotNull(_testCaseDiscoverySink.Tests);
        Assert.AreEqual(0, _testCaseDiscoverySink.Tests.Count);
    }

    [TestMethod]
    public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
    {
        _testCaseDiscoverySink.SendTestCase(null);

        Assert.IsNotNull(_testCaseDiscoverySink.Tests);
        Assert.AreEqual(0, _testCaseDiscoverySink.Tests.Count);
    }

    [TestMethod]
    public void SendTestCaseShouldAddTheTestCaseToTests()
    {
        TestCase tc = new("T", new Uri("executor://TestExecutorUri"), "A");
        _testCaseDiscoverySink.SendTestCase(tc);

        Assert.IsNotNull(_testCaseDiscoverySink.Tests);
        Assert.AreEqual(1, _testCaseDiscoverySink.Tests.Count);
        Assert.AreEqual(tc, _testCaseDiscoverySink.Tests.ToArray()[0]);
    }
}
