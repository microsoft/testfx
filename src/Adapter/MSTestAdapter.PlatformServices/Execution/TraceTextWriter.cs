// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class TraceTextWriter : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.WriteTrace(value);
        }
    }

    public override void Write(string? value)
    {
        if (TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.WriteTrace(value);
        }
    }
}
