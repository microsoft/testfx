// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Moq;

using TestFramework.ForTestingMSTest;

public class TestContextExtensionsTests : TestContainer
{
    public void GetAndClearDiagnosticMessagesShouldReturnTestContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();

        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns("foo");

        Verify("foo" == mockTestContext.Object.GetAndClearDiagnosticMessages());
    }

    public void GetAndClearDiagnosticMessagesShouldClearContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();
        var message = "foobar";
        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns(() => { return message; });
        mockTestContext.Setup(tc => tc.ClearDiagnosticMessages()).Callback(() => message = string.Empty);

        // First call.
        mockTestContext.Object.GetAndClearDiagnosticMessages();
        message = "bar";

        Verify("bar" == mockTestContext.Object.GetAndClearDiagnosticMessages());
    }
}
