// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Telemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class LocalFileTelemetryClientTests
{
    [TestMethod]
    public void TrackEvent_WritesJsonLine_WithEventPropertiesAndMetrics()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"mtp-telemetry-{Guid.NewGuid():N}.jsonl");
        try
        {
            var client = new LocalFileTelemetryClient(filePath, "session-42", "Windows 11");
            client.TrackEvent(
                "dotnet/testingplatform/host/consoletesthostexit",
                new Dictionary<string, string> { ["is ci"] = "true" },
                new Dictionary<string, double> { ["run start"] = 1234.5 });

            string[] lines = File.ReadAllLines(filePath);
            Assert.HasCount(1, lines);

            string line = lines[0];
            Assert.Contains("\"eventName\":\"dotnet/testingplatform/host/consoletesthostexit\"", line);
            Assert.Contains("\"sessionId\":\"session-42\"", line);
            Assert.Contains("\"osVersion\":\"Windows 11\"", line);
            Assert.Contains("\"is ci\":\"true\"", line);
            Assert.Contains("\"run start\":1234.5", line);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void TrackEvent_AppendsOneLinePerEvent()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"mtp-telemetry-{Guid.NewGuid():N}.jsonl");
        try
        {
            var client = new LocalFileTelemetryClient(filePath, "s", "os");
            client.TrackEvent("first", [], []);
            client.TrackEvent("second", [], []);

            string[] lines = File.ReadAllLines(filePath);
            Assert.HasCount(2, lines);
            Assert.Contains("\"eventName\":\"first\"", lines[0]);
            Assert.Contains("\"eventName\":\"second\"", lines[1]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void TrackEvent_EscapesSpecialCharacters()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"mtp-telemetry-{Guid.NewGuid():N}.jsonl");
        try
        {
            var client = new LocalFileTelemetryClient(filePath, null, "os");
            client.TrackEvent(
                "evt",
                new Dictionary<string, string> { ["quote\"backslash\\"] = "line\nbreak" },
                []);

            string content = File.ReadAllText(filePath);
            Assert.Contains("quote\\\"backslash\\\\", content);
            Assert.Contains("line\\nbreak", content);

            // The raw newline must be escaped so the record stays on a single line.
            Assert.HasCount(1, File.ReadAllLines(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void Factory_WithLocalExportPath_CreatesLocalFileClient()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"mtp-telemetry-{Guid.NewGuid():N}.jsonl");
        var factory = new AppInsightTelemetryClientFactory(filePath);

        ITelemetryClient client = factory.Create("session", "os");

        Assert.IsInstanceOfType<LocalFileTelemetryClient>(client);
    }
}
