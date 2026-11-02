// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class OutputPropertyTests
{
    [TestMethod]
    [DataRow("payload", "payload", true)]
    [DataRow("payload", "different", false)]
    [DataRow("payload", "PAYLOAD", false)]
    [DataRow("", "", true)]
    [DataRow(null, null, true)]
    [DataRow(null, "", false)]
    public void OutputProperties_EqualityUsesOrdinalPayloadAndPreservesDiagnostics(string? first, string? second, bool equal)
    {
        StandardOutputProperty output = new(first!);
        StandardOutputProperty otherOutput = new(second!);
        StandardErrorProperty error = new(first!);
        StandardErrorProperty otherError = new(second!);

        Assert.AreEqual(equal, output.Equals(otherOutput));
        Assert.AreEqual(equal, output.Equals((object)otherOutput));
        Assert.AreEqual(equal, error.Equals(otherError));
        Assert.AreEqual(equal, error.Equals((object)otherError));
        Assert.IsFalse(output.Equals((object?)null));
        Assert.IsFalse(error.Equals((object?)null));
        Assert.IsFalse(output.Equals(error));
        Assert.IsFalse(error.Equals(output));
        Assert.AreEqual(first, output.StandardOutput);
        Assert.AreEqual(first, error.StandardError);
        Assert.AreEqual($"StandardOutputProperty {{ StandardOutput = {first} }}", output.ToString());
        Assert.AreEqual($"StandardErrorProperty {{ StandardError = {first} }}", error.ToString());
        Assert.AreEqual(first?.GetHashCode() ?? 0, output.GetHashCode());
        Assert.AreEqual(first?.GetHashCode() ?? 0, error.GetHashCode());
    }
}
