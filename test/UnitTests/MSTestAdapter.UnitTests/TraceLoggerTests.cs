// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.VSTestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class TraceLoggerTests : TestContainer
{
    public void TraceLoggersShouldHaveInfiniteLifetime()
    {
        MarshalByRefObject[] traceLoggers =
        [
            (MarshalByRefObject)EqtTraceLogger.Instance,
            (MarshalByRefObject)NopTraceLogger.Instance,
            new MTPTraceLogger(new Mock<ILogger>().Object),
        ];

        foreach (MarshalByRefObject traceLogger in traceLoggers)
        {
            traceLogger.InitializeLifetimeService().Should().BeNull();
        }
    }
}
#endif
