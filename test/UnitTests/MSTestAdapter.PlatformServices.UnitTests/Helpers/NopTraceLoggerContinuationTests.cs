// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class NopTraceLoggerContinuationTests : TestContainer
{
    public void DisabledLoggerNeverFormatsUserValuesOrExceptions()
    {
        ITraceLogger logger = NopTraceLogger.Instance;
        var hostile = new HostileDiagnosticValue();
        Action log = () =>
        {
            logger.Verbose("invalid {", hostile);
            logger.Info("{0}", hostile);
            logger.Warning("{0}", hostile);
            logger.Error("{0}", hostile);
            logger.Verbose("trace");
            logger.Info("info");
            logger.Warning("warning");
            logger.Error("error");
            logger.Error(new HostileDiagnosticException());
#if NETFRAMEWORK
            logger.SetupRemoteEqtTraceListeners(null);
#endif
        };

        log.Should().NotThrow();
        hostile.Evaluations.Should().Be(0);
        logger.IsVerboseEnabled.Should().BeFalse();
        logger.IsInfoEnabled.Should().BeFalse();
        logger.IsWarningEnabled.Should().BeFalse();
        logger.IsErrorEnabled.Should().BeFalse();
    }

    private sealed class HostileDiagnosticValue
    {
        internal int Evaluations { get; private set; }

        public override string ToString()
        {
            Evaluations++;
            throw new InvalidOperationException("Diagnostic values must not be formatted by a disabled logger.");
        }
    }

    private sealed class HostileDiagnosticException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("Diagnostic exceptions must not be formatted.");
    }
}
