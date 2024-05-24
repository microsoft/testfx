// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class TestContextExtensionsTests : TestContainer
{
    public void GetAndClearDiagnosticMessagesShouldReturnTestContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();

        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns("foo");

        Verify(mockTestContext.Object.GetAndClearDiagnosticMessages() == "foo");
    }

    public void GetAndClearDiagnosticMessagesShouldClearContextMessages()
    {
        Mock<ITestContext> mockTestContext = new();
        string message = "foobar";
        mockTestContext.Setup(tc => tc.GetDiagnosticMessages()).Returns(() => message);
        mockTestContext.Setup(tc => tc.ClearDiagnosticMessages()).Callback(() => message = string.Empty);

        // First call.
        mockTestContext.Object.GetAndClearDiagnosticMessages();
        message = "bar";

        Verify(mockTestContext.Object.GetAndClearDiagnosticMessages() == "bar");
    }
}
