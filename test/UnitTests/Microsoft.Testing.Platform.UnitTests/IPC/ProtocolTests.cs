// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ProtocolTests
{
    [TestMethod]
    public void TestResultMessagesSerializeDeserialize()
    {
        var success = new SuccessfulTestResultMessage("uid", "displayName", 1, 100, "reason", "standardOutput", "errorOutput", "sessionUid");
        var fail = new FailedTestResultMessage("uid", "displayName", 2, 200, "reason", [new ExceptionMessage("errorMessage", "errorType", "stackTrace")], "standardOutput", "errorOutput", "sessionUid");
        var message = new TestResultMessages("executionId", "instanceId", [success], [fail]);

        var stream = new MemoryStream();
        new TestResultMessagesSerializer().Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestResultMessages)new TestResultMessagesSerializer().Deserialize(stream);
        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
#if NET8_0_OR_GREATER
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(message), System.Text.Json.JsonSerializer.Serialize(actual));
#endif
        Assert.AreEqual(message.FailedTestMessages?[0].Exceptions?[0].ErrorMessage, actual.FailedTestMessages?[0].Exceptions?[0].ErrorMessage);
    }
}
