// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Extensions;

public class ExceptionExtensionsTests : TestContainer
{
    public void GetExceptionMessageShouldReturnExceptionMessage()
    {
        Exception ex = new("something bad happened");
        Verify(ex.GetExceptionMessage() == "something bad happened");
    }

    public void GetExceptionMessageShouldReturnInnerExceptionMessageAsWell()
    {
        Exception ex = new("something bad happened", new Exception("inner exception", new Exception("the real exception")));
        string expectedMessage = string.Concat(
            "something bad happened",
            Environment.NewLine,
            "inner exception",
            Environment.NewLine,
            "the real exception");

        Verify(expectedMessage == ex.GetExceptionMessage());
    }
}
