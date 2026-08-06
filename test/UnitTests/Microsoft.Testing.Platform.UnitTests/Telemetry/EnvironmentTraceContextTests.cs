// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class EnvironmentTraceContextTests
{
    private const string ValidTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [TestMethod]
    [DataRow(ValidTraceParent)]
    [DataRow("01-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00")]
    public void IsValidTraceParent_WithWellFormedValue_ReturnsTrue(string traceParent)
        => Assert.IsTrue(EnvironmentTraceContext.IsValidTraceParent(traceParent));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("garbage")]
    // Wrong length.
    [DataRow("00-0af7651916cd43dd8448eb211c80319c-b7ad6b716920333-01")]
    // Missing separators.
    [DataRow("000af7651916cd43dd8448eb211c80319cb7ad6b716920333101")]
    // Non-hex character in the trace id.
    [DataRow("00-0zf7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")]
    // Uppercase hex: System.Diagnostics requires lowercase and silently starts a new trace otherwise.
    [DataRow("00-0AF7651916CD43DD8448EB211C80319C-B7AD6B7169203331-01")]
    // Version 'ff' is forbidden by the W3C specification and rejected by System.Diagnostics.
    [DataRow("ff-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")]
    // All-zero trace id is invalid per the W3C specification.
    [DataRow("00-00000000000000000000000000000000-b7ad6b7169203331-01")]
    // All-zero span id is invalid per the W3C specification.
    [DataRow("00-0af7651916cd43dd8448eb211c80319c-0000000000000000-01")]
    public void IsValidTraceParent_WithMalformedValue_ReturnsFalse(string? traceParent)
        => Assert.IsFalse(EnvironmentTraceContext.IsValidTraceParent(traceParent));

    [TestMethod]
    public void TryGetParentId_ReadsTraceParentFromEnvironment()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TRACEPARENT")).Returns(ValidTraceParent);

        Assert.AreEqual(ValidTraceParent, EnvironmentTraceContext.TryGetParentId(environment.Object));
    }

    [TestMethod]
    public void TryGetParentId_PrefersTraceParentOverTheTestingPlatformSpecificVariable()
    {
        const string otherTraceParent = "00-11111111111111111111111111111111-2222222222222222-01";
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TRACEPARENT")).Returns(ValidTraceParent);
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_TRACEPARENT")).Returns(otherTraceParent);

        Assert.AreEqual(ValidTraceParent, EnvironmentTraceContext.TryGetParentId(environment.Object));
    }

    [TestMethod]
    public void TryGetParentId_FallsBackToTheTestingPlatformSpecificVariable()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TRACEPARENT")).Returns((string?)null);
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_TRACEPARENT")).Returns(ValidTraceParent);

        Assert.AreEqual(ValidTraceParent, EnvironmentTraceContext.TryGetParentId(environment.Object));
    }

    [TestMethod]
    public void TryGetParentId_WithMalformedValue_ReturnsNull()
    {
        // A malformed variable must not poison the trace: System.Diagnostics silently drops invalid parent ids,
        // which is much harder to diagnose than simply starting a new root trace.
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TRACEPARENT")).Returns("not-a-traceparent");

        Assert.IsNull(EnvironmentTraceContext.TryGetParentId(environment.Object));
    }

    [TestMethod]
    public void TryGetTraceState_ReturnsNullWhenUnset()
    {
        Mock<IEnvironment> environment = new();

        Assert.IsNull(EnvironmentTraceContext.TryGetTraceState(environment.Object));
    }

    [TestMethod]
    public void TryGetTraceState_ReturnsTrimmedValue()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("TRACESTATE")).Returns("  vendor=value  ");

        Assert.AreEqual("vendor=value", EnvironmentTraceContext.TryGetTraceState(environment.Object));
    }
}
