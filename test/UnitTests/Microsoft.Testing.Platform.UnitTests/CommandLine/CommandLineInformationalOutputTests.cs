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
}

#endif
