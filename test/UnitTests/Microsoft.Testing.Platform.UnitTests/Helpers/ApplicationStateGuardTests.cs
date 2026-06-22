// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ApplicationStateGuardTests
{
    [TestMethod]
    public void Ensure_WithMessage_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => ApplicationStateGuard.Ensure(false, "boom"));

        Assert.AreEqual("boom", exception.Message);
    }

    [TestMethod]
    public void Unreachable_ReturnsUnreachableException()
    {
        // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so binding to the type
        // directly would compare against this test assembly's copy and fail due to type identity mismatch across
        // assemblies. Assert on the full type name instead.
        Exception exception = ApplicationStateGuard.Unreachable();

        Assert.AreEqual("System.Diagnostics.UnreachableException", exception.GetType().FullName);
        Assert.Contains("thought to be unreachable", exception.Message);
    }
}
