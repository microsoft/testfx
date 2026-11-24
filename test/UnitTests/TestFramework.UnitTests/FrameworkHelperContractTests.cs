// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class FrameworkHelperContractTests : TestContainer
{
    public void ApplicationStateGuard_ProducesExactCallerDiagnosticAndThrowableInstance()
    {
        Exception error = ApplicationStateGuard.Unreachable("task-owned.cs", 37);
        Action action = () => throw error;
        action.Should().Throw<Exception>().Which.Should().BeSameAs(error);
        error.GetType().FullName.Should().Be("System.Diagnostics.UnreachableException");
        error.Message.Should().Be("This program location is thought to be unreachable. File='task-owned.cs' Line=37");
        error.InnerException.Should().BeNull();
        ApplicationStateGuard.Unreachable(null, 0).Message
            .Should().Be("This program location is thought to be unreachable. File='' Line=0");
    }

    public void StringValidationHelpers_DistinguishNullEmptyWhitespaceAndText()
    {
        (string? Input, bool Empty, bool WhiteSpace)[] cases =
        [
            (null, true, true),
            (string.Empty, true, true),
            (" ", false, true),
            ("\t\r\n", false, true),
            ("text", false, false),
            (" text ", false, false),
            ("\0", false, false),
        ];
        foreach ((string? input, bool empty, bool whiteSpace) in cases)
        {
            StringEx.IsNullOrEmpty(input).Should().Be(empty);
            StringEx.IsNullOrWhiteSpace(input).Should().Be(whiteSpace);
        }
    }
}
