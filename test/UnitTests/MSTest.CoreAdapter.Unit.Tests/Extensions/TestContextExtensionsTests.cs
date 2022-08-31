// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

extern alias FrameworkV1;
extern alias FrameworkV2;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Moq;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class TestContextExtensionsTests
{
    [TestMethod]
    public void GetAndClearDiagnosticMessagesShouldReturnTestContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();

        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns("foo");

        Assert.AreEqual("foo", mockTestContext.Object.GetAndClearDiagnosticMessages());
    }

    [TestMethod]
    public void GetAndClearDiagnosticMessagesShouldClearContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();
        var message = "foobar";
        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns(() => { return message; });
        mockTestContext.Setup(tc => tc.ClearDiagnosticMessages()).Callback(() => message = string.Empty);

        // First call.
        mockTestContext.Object.GetAndClearDiagnosticMessages();
        message = "bar";

        Assert.AreEqual("bar", mockTestContext.Object.GetAndClearDiagnosticMessages());
    }
}
