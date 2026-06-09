// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class RemoteInvocationExceptionTests
{
    [TestMethod]
    public void Constructor_PreservesRemoteErrorDetails()
    {
        RemoteInvocationException exception = new(42, "remote failure", "diagnostic data");

        Assert.AreEqual(42, exception.ErrorCode);
        Assert.AreEqual("remote failure", exception.ErrorMessage);
        Assert.AreEqual("diagnostic data", exception.ErrorData);
        Assert.Contains("42", exception.Message);
        Assert.Contains("remote failure", exception.Message);
        Assert.Contains("diagnostic data", exception.Message);
    }
}
