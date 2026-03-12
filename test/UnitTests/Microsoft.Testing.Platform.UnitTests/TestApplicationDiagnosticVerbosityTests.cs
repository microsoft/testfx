// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestApplicationDiagnosticVerbosityTests
{
    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenLowercaseValue_ReturnsParsedValue()
    {
        bool hasValue = TestApplication.TryParseDiagnosticVerbosity("trace", out LogLevel parsedLogLevel);

        Assert.IsTrue(hasValue);
        Assert.AreEqual(LogLevel.Trace, parsedLogLevel);
    }

    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenNullValue_ReturnsFalse()
    {
        bool hasValue = TestApplication.TryParseDiagnosticVerbosity(null, out LogLevel parsedLogLevel);

        Assert.IsFalse(hasValue);
        Assert.AreEqual(LogLevel.None, parsedLogLevel);
    }

    [TestMethod]
    public void TryParseDiagnosticVerbosity_WhenInvalidValue_ThrowsNotSupportedException()
        => Assert.ThrowsExactly<NotSupportedException>(() => _ = TestApplication.TryParseDiagnosticVerbosity("invalid", out _));
}
