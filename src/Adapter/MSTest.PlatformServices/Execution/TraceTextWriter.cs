// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.PlatformServices.Execution;

internal sealed class TraceTextWriter : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteTrace(value);
        }
    }

    public override void Write(string? value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteTrace(value);
        }
    }
}
