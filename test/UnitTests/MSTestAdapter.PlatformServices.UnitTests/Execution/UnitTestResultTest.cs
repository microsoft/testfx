// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class UnitTestResultTest : TestContainer
{
    public void UnitTestResultConstructorWithOutcomeAndErrorMessageShouldSetRequiredFields()
    {
        UnitTestResult result = new(UnitTestOutcome.Error, "DummyMessage");

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.ErrorMessage.Should().Be("DummyMessage");
    }

    public void UnitTestResultConstructorWithTestFailedExceptionShouldSetRequiredFields()
    {
        var stackTrace = new StackTraceInformation("trace", "filePath", 2, 3);
        TestFailedException ex = new(TestTools.UnitTesting.UnitTestOutcome.Error, "DummyMessage", stackTrace);

        UnitTestResult result = new(ex);

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.ErrorMessage.Should().Be("DummyMessage");
        result.ErrorStackTrace.Should().Be("trace");
        result.ErrorFilePath.Should().Be("filePath");
        result.ErrorLineNumber.Should().Be(2);
        result.ErrorColumnNumber.Should().Be(3);
    }
}
