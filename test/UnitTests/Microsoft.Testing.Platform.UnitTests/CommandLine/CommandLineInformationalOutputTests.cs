// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class CommandLineInformationalOutputTests
{
    private const string IncompleteResponse = "The test host exited without completing the expected help or discovery protocol. No complete informational result was received.";

    [TestMethod]
    public async Task Help_OrdinalOrderingNullFlagsDescriptionsAndEmptyExtensionFallbackAreExact()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new(Path.Combine("a folder", "Consumer.exe"), ["--help"], text);
        _ = output.AcceptHandshake(CreateHandshake(help: true));
        await output.WriteHelpAsync(
        [
            new("z-last", "second\r\nthird", false, true),
            new("A-first", null, null, true),
            new("hidden", "not visible", true, false),
        ]);
        output.ValidateCompletion();
        string expected = string.Join(
            Environment.NewLine,
            [
                "Usage Consumer.exe [option providers] [extension option providers]", string.Empty,
                "Execute a .NET Test Application.", string.Empty, "Options:",
                "    --A-first", "        ", string.Empty, "    --z-last", "        second", "        third", string.Empty,
                string.Empty, "Extension options:", "    No extension registered.", string.Empty, string.Empty,
            ]);
        Assert.AreEqual(expected, text.ToString());
    }

    [TestMethod]
    public async Task JsonDiscovery_ProjectsRealNodeTraitsWithoutImmediateTextAndInvalidRowsStayInvalid()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--list-tests", "json"], text);
        HandshakeMessage response = output.AcceptHandshake(CreateHandshake(help: false));
        Assert.AreEqual("True", response.Properties![HandshakeMessagePropertyNames.IsIDE]);
        output.ObserveSession(new(SessionEventTypes.TestSessionStart, "session", "execution"));
        await output.WriteDiscoveredTestsAsync(new("execution", "instance",
        [
            new("uid", "Actual name", null, null, null, null, null, null, [new("Category", "Fast"), new("Owner", "Team")]),
            new(null, "missing uid", null, null, null, null, null, null, []),
            new("missing-name", null, null, null, null, null, null, null, []),
        ]));
        output.ObserveSession(new(SessionEventTypes.TestSessionEnd, "session", "execution"));
        var nodes = (List<TestNode>)typeof(CommandLineInformationalOutput)
            .GetField("_jsonTests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)!;
        TestNode node = Assert.ContainsSingle(nodes);
        Assert.AreEqual("uid", node.Uid.Value);
        Assert.AreEqual("Actual name", node.DisplayName);
        Assert.AreSequenceEqual(
            [("Owner", "Team"), ("Category", "Fast")],
            node.Properties.OfType<TestMetadataProperty>().Select(property => (property.Key, property.Value)));
        Assert.AreEqual(string.Empty, text.ToString());
        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion);
        Assert.AreEqual(IncompleteResponse, failure.Message);
    }

    [TestMethod]
    public async Task Discovery_DuplicateHandshakeAndPostCompletionRowsCannotBecomeCompleteAgain()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--list-tests"], text);
        _ = output.AcceptHandshake(CreateHandshake(help: false));
        HandshakeMessage duplicate = output.AcceptHandshake(CreateHandshake(help: false));
        Assert.AreEqual(string.Empty, duplicate.Properties![HandshakeMessagePropertyNames.SupportedProtocolVersions]);
        _ = output.AcceptHandshake(CreateHandshake(help: false));
        output.ObserveSession(new(SessionEventTypes.TestSessionStart, "session", "execution"));
        output.ObserveSession(new(SessionEventTypes.TestSessionEnd, "session", "execution"));
        await output.WriteDiscoveredTestsAsync(new("execution", "instance",
            [new("uid", "must not print", null, null, null, null, null, null, [])]));
        Assert.AreEqual(string.Empty, text.ToString());
        Assert.AreEqual(
            IncompleteResponse,
            Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion).Message);
    }

    [TestMethod]
    public async Task Help_UsesActualHostOptionsAndHidesInternalOptions()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--help"], text);
        HandshakeMessage reply = output.AcceptHandshake(CreateHandshake(help: true));
        Assert.AreEqual("1.4.0", reply.Properties![HandshakeMessagePropertyNames.SupportedProtocolVersions]);
        await output.WriteHelpAsync(
        [
            new("hidden", "Internal.", true, true),
            new("help", "Show help.", false, true),
            new("filter", "Select tests.", false, false),
        ]);
        output.ValidateCompletion();

        Assert.AreEqual(
            """
            Usage Tests.exe [option providers] [extension option providers]

            Execute a .NET Test Application.

            Options:
                --help
                    Show help.


            Extension options:
                --filter
                    Select tests.



            """.ReplaceLineEndings(),
            text.ToString().ReplaceLineEndings());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public async Task Discovery_RelaysResultsAndRequiresSessionCompletion(int count)
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--list-tests"], text);
        _ = output.AcceptHandshake(CreateHandshake(help: false));
        output.ObserveSession(new(SessionEventTypes.TestSessionStart, "session", "execution"));
        DiscoveredTestMessage[] tests = count == 0
            ? []
            : [new("first", "First", null, null, null, null, null, null, []), new("second", "Second", null, null, null, null, null, null, [])];
        await output.WriteDiscoveredTestsAsync(new("execution", "instance", tests));
        _ = Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion);
        output.ObserveSession(new(SessionEventTypes.TestSessionEnd, "session", "execution"));
        output.ValidateCompletion();
        string expected = count == 0
            ? string.Empty
            : """
                First
                Second

              """;
        Assert.AreEqual(expected.ReplaceLineEndings(), text.ToString().ReplaceLineEndings());
    }

    [TestMethod]
    public async Task Completion_RejectsZeroExitWithoutConnection()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--help"], text);
        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(output.CompleteAsync);
        Assert.AreEqual(string.Empty, text.ToString());
    }

    [TestMethod]
    [DataRow(false, "1.0.0", true)]
    [DataRow(true, "99.0.0", true)]
    public async Task Handshake_RejectsWrongModeOrUnsupportedProtocol(bool help, string version, bool peerHelp)
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", [peerHelp ? "--help" : "--list-tests"], text);
        HandshakeMessage reply = output.AcceptHandshake(CreateHandshake(help, version));
        Assert.AreEqual(string.Empty, reply.Properties![HandshakeMessagePropertyNames.SupportedProtocolVersions]);
        _ = Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion);
    }

    [TestMethod]
    public async Task Discovery_RejectsPartialOrMismatchedSession()
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--list-tests"], text);
        _ = output.AcceptHandshake(CreateHandshake(help: false));
        output.ObserveSession(new(SessionEventTypes.TestSessionStart, "expected", "execution"));
        _ = Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion);
        output.ObserveSession(new(SessionEventTypes.TestSessionEnd, "other", "execution"));
        _ = Assert.ThrowsExactly<InvalidOperationException>(output.ValidateCompletion);
    }

    private static HandshakeMessage CreateHandshake(bool help, string version = "1.0.0;1.4.0")
        => new(new()
        {
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = version,
            [HandshakeMessagePropertyNames.ExecutionMode] = help ? HandshakeMessageExecutionModes.Help : HandshakeMessageExecutionModes.Discover,
        });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PipeProtocol_CompletedDiscoveryWritesExactFinalResult(bool json)
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", json ? ["--list-tests", "json"] : ["--list-tests"], text);
        using (InformationalWirePeer peer = new(output.PipeName))
        {
            await peer.ConnectAsync();
            HandshakeMessage reply = await peer.HandshakeAsync(CreateHandshake(help: false));
            Assert.AreEqual("1.4.0", reply.Properties![HandshakeMessagePropertyNames.SupportedProtocolVersions]);
            Assert.AreEqual(json ? "True" : "False", reply.Properties[HandshakeMessagePropertyNames.IsIDE]);
            await peer.SendAsync(new TestSessionEvent(SessionEventTypes.TestSessionStart, "session", "execution"), new Microsoft.Testing.Platform.IPC.Serializers.TestSessionEventSerializer(), 8);
            await peer.SendAsync(new DisplayMessage("execution", "instance", DisplayMessageLevels.Information, "note"), new Microsoft.Testing.Platform.IPC.Serializers.DisplayMessageSerializer(), 12);
            await peer.SendAsync(
                new DiscoveredTestMessages("execution", "instance", [new("uid-17", "Visible case", null, null, null, null, null, null, [])]),
                new Microsoft.Testing.Platform.IPC.Serializers.DiscoveredTestMessagesSerializer(),
                5);
            await peer.SendAsync(new TestSessionEvent(SessionEventTypes.TestSessionEnd, "session", "execution"), new Microsoft.Testing.Platform.IPC.Serializers.TestSessionEventSerializer(), 8);
        }

        await output.CompleteAsync();

        string expected = json
            ? """
              note

              {
                "schemaVersion": 1,
                "tests": [
                  {
                    "uid": "uid-17",
                    "displayName": "Visible case"
                  }
                ]
              }
              """
            : """
              note
                Visible case
              Discovered 1 test(s) in assembly
              """;
        Assert.AreEqual(expected.ReplaceLineEndings() + Environment.NewLine, text.ToString().ReplaceLineEndings());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PipeProtocol_InvalidOrderOrUnexpectedRequestNeverCompletes(bool beforeHandshake)
    {
        using StringWriter text = new();
        await using CommandLineInformationalOutput output = new("Tests.exe", ["--help"], text);
        using (InformationalWirePeer peer = new(output.PipeName))
        {
            await peer.ConnectAsync();
            if (beforeHandshake)
            {
                await peer.SendAsync(new DisplayMessage(null, null, DisplayMessageLevels.Error, "must not print"), new Microsoft.Testing.Platform.IPC.Serializers.DisplayMessageSerializer(), 12);
            }

            _ = await peer.HandshakeAsync(CreateHandshake(help: true));
            if (!beforeHandshake)
            {
                await peer.SendAsync(new TestHostProcessPIDRequest(17), new Microsoft.Testing.Platform.IPC.Serializers.TestHostProcessPIDRequestSerializer(), 2);
            }

            await peer.SendAsync(new CommandLineOptionMessages("Tests", []), new Microsoft.Testing.Platform.IPC.Serializers.CommandLineOptionMessagesSerializer(), 3);
        }

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(output.CompleteAsync);
        Assert.AreEqual(IncompleteResponse, failure.Message);
        Assert.DoesNotContain("must not print", text.ToString());
        Assert.AreEqual(
            """
            Usage Tests.exe [option providers] [extension option providers]

            Execute a .NET Test Application.

            Options:

            Extension options:
                No extension registered.


            """.ReplaceLineEndings(),
            text.ToString().ReplaceLineEndings());
    }

    private sealed class InformationalWirePeer(string pipeName) : IDisposable
    {
        private readonly System.IO.Pipes.NamedPipeClientStream _stream = new(
            ".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);

        public Task ConnectAsync() => _stream.ConnectAsync(CancellationToken.None);

        public async Task<HandshakeMessage> HandshakeAsync(HandshakeMessage request)
        {
            var serializer = new Microsoft.Testing.Platform.IPC.Serializers.HandshakeMessageSerializer();
            byte[] response = await ExchangeAsync(request, serializer, 9);
            Assert.AreEqual(9, BitConverter.ToInt32(response, 0));
            using MemoryStream payload = new(response, sizeof(int), response.Length - sizeof(int));
            return (HandshakeMessage)ProtocolSerializerTestHelper.Deserialize(serializer, payload);
        }

        public async Task SendAsync<TMessage>(TMessage message, object serializer, int serializerId)
        {
            byte[] response = await ExchangeAsync(message, serializer, serializerId);
            Assert.AreSequenceEqual(new byte[] { 0, 0, 0, 0 }, response);
        }

        public void Dispose() => _stream.Dispose();

        private async Task<byte[]> ExchangeAsync<TMessage>(TMessage message, object serializer, int serializerId)
        {
            using MemoryStream payload = new();
            // Reuse the canonical helper only to bridge source-linked and shipping serializer base types.
            // All assertions concern the actual framed pipe replies and externally rendered output.
            ProtocolSerializerTestHelper.Serialize(serializer, message, payload);
            byte[] frame = [.. BitConverter.GetBytes(checked((int)payload.Length + sizeof(int))), .. BitConverter.GetBytes(serializerId), .. payload.ToArray()];
            await _stream.WriteAsync(frame);
            await _stream.FlushAsync();
            byte[] header = new byte[sizeof(int)];
            await _stream.ReadExactlyAsync(header);
            byte[] response = new byte[BitConverter.ToInt32(header, 0)];
            await _stream.ReadExactlyAsync(response);
            return response;
        }
    }
}

#endif
