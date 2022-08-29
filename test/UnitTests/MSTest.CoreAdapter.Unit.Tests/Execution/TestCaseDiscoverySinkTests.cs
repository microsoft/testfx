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
    private TestCaseDiscoverySink testCaseDiscoverySink;

    [TestInitialize]
    public void TestInit()
    {
        testCaseDiscoverySink = new TestCaseDiscoverySink();
    }

    [TestMethod]
    public void TestCaseDiscoverySinkConstructorShouldInitializeTests()
    {
        Assert.IsNotNull(testCaseDiscoverySink.Tests);
        Assert.AreEqual(0, testCaseDiscoverySink.Tests.Count);
    }

    [TestMethod]
    public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
    {
        testCaseDiscoverySink.SendTestCase(null);

        Assert.IsNotNull(testCaseDiscoverySink.Tests);
        Assert.AreEqual(0, testCaseDiscoverySink.Tests.Count);
    }

    [TestMethod]
    public void SendTestCaseShouldAddTheTestCaseToTests()
    {
        TestCase tc = new("T", new Uri("executor://TestExecutorUri"), "A");
        testCaseDiscoverySink.SendTestCase(tc);

        Assert.IsNotNull(testCaseDiscoverySink.Tests);
        Assert.AreEqual(1, testCaseDiscoverySink.Tests.Count);
        Assert.AreEqual(tc, testCaseDiscoverySink.Tests.ToArray()[0]);
    }
}
