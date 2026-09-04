// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Client;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// End-to-end tests for <see cref="MtpServerClient"/> driven against <see cref="FakeMtpServer"/> over a real
/// loopback TCP connection. The net8.0 leg exercises the System.Text.Json formatter and the net462 leg the
/// Jsonite formatter, so every test runs against both shipping serialization paths.
/// </summary>
[TestClass]
public sealed class MtpServerClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // A representative server-initiated request method. The platform has no such constant yet; the client
    // dispatches any server request generically, so a literal is sufficient to exercise the decline/handle path.
    private const string ClientAttachDebuggerMethod = "client/attachDebugger";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InitializeAsync_DecodesServerCapabilities()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = server.ConnectClient(new MtpServerClientOptions
        {
            IsStateful = true,
        });

        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.AreEqual(4242, capabilities.ServerProcessId);
        Assert.AreEqual("FakeMtpServer", capabilities.ServerName);
        Assert.AreEqual("1.2.3", capabilities.ServerVersion);
        Assert.AreEqual(JsonRpcProtocolVersions.Current, capabilities.ProtocolVersion);
        Assert.IsTrue(capabilities.SupportsDiscovery);
        Assert.IsTrue(capabilities.MultiRequestSupport);
        Assert.IsFalse(capabilities.VSTestProviderSupport);
        Assert.IsTrue(capabilities.SupportsAttachments);
        Assert.IsFalse(capabilities.MultiConnectionProvider);
        Assert.AreSame(capabilities, client.Capabilities);

        InitializeRequestArgs initializeArgs = GetSingleRequestParams<InitializeRequestArgs>(server, JsonRpcMethods.Initialize);
        Assert.AreSequenceEqual(JsonRpcProtocolVersions.Supported, initializeArgs.ProtocolVersions);
        Assert.IsTrue(initializeArgs.Capabilities.IsStateful);
    }

    [TestMethod]
    public void SerializeClientCapabilities_UndeclaredStatefulness_OmitsProperty()
    {
        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(
            new ClientCapabilities(DebuggerProvider: false, IsStateful: null));
        var testingCapabilities = (IDictionary<string, object?>)serialized[JsonRpcStrings.Testing]!;

        Assert.IsFalse(testingCapabilities.ContainsKey(JsonRpcStrings.IsStateful));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SerializeClientCapabilities_DeclaredStatefulness_IncludesProperty(bool isStateful)
    {
        IDictionary<string, object?> serialized = SerializerUtilities.Serialize(
            new ClientCapabilities(DebuggerProvider: false, IsStateful: isStateful));
        var testingCapabilities = (IDictionary<string, object?>)serialized[JsonRpcStrings.Testing]!;

        Assert.AreEqual(isStateful, testingCapabilities[JsonRpcStrings.IsStateful]);
    }

    [TestMethod]
    public async Task InitializeAsync_LegacyServerWithoutProtocolVersion_Succeeds()
    {
        using FakeMtpServer server = new();
        server.InitializeResponse = server.InitializeResponse with { ProtocolVersion = null };
        using MtpServerClient client = server.ConnectClient();

        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.IsNull(capabilities.ProtocolVersion);
    }

    [TestMethod]
    public async Task InitializeAsync_LegacyServerWithoutSupportedVersion_Throws()
    {
        using FakeMtpServer server = new();
        server.InitializeResponse = server.InitializeResponse with { ProtocolVersion = null };
        using MtpServerClient client = server.ConnectClient(new MtpServerClientOptions
        {
            SupportedProtocolVersions = ["2.0.0"],
        });

        MtpServerClientException exception = await AssertThrowsAsync<MtpServerClientException>(
            () => client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(JsonRpcProtocolVersions.V1, exception.Message);
        Assert.IsNull(client.Capabilities);
    }

    [TestMethod]
    public async Task InitializeAsync_UnsupportedNegotiatedProtocolVersion_Throws()
    {
        using FakeMtpServer server = new();
        server.InitializeResponse = server.InitializeResponse with { ProtocolVersion = "2.0.0" };
        using MtpServerClient client = server.ConnectClient();

        MtpServerClientException exception = await AssertThrowsAsync<MtpServerClientException>(
            () => client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains("2.0.0", exception.Message);
        Assert.IsNull(client.Capabilities);
    }

    [TestMethod]
    public async Task InitializeAsync_EmptySupportedVersions_AcceptsLegacyVersion()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = server.ConnectClient(new MtpServerClientOptions
        {
            SupportedProtocolVersions = [],
        });

        MtpServerCapabilities capabilities = await WithTimeoutAsync(
            client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.AreEqual(JsonRpcProtocolVersions.V1, capabilities.ProtocolVersion);
    }

    [TestMethod]
    public async Task InitializeAsync_NonStringProtocolVersion_Throws()
    {
        using FakeMtpServer server = new();
        server.InitializeResponseOverride = new Dictionary<string, object?>
        {
            [JsonRpcStrings.ProcessId] = 4242,
            [JsonRpcStrings.ServerInfo] = new Dictionary<string, object?>
            {
                [JsonRpcStrings.Name] = "FakeMtpServer",
                [JsonRpcStrings.Version] = "1.2.3",
            },
            [JsonRpcStrings.Capabilities] = new Dictionary<string, object?>
            {
                [JsonRpcStrings.Testing] = new Dictionary<string, object?>
                {
                    [JsonRpcStrings.SupportsDiscovery] = true,
                    [JsonRpcStrings.MultiRequestSupport] = true,
                    [JsonRpcStrings.VSTestProviderSupport] = false,
                    [JsonRpcStrings.AttachmentsSupport] = true,
                    [JsonRpcStrings.MultiConnectionProvider] = false,
                },
            },
            [JsonRpcStrings.ProtocolVersion] = 1,
        };
        using MtpServerClient client = server.ConnectClient();

        MtpServerClientException exception = await AssertThrowsAsync<MtpServerClientException>(
            () => client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(JsonRpcStrings.ProtocolVersion, exception.Message);
        Assert.IsNull(client.Capabilities);
    }

    [TestMethod]
    public async Task DiscoverTestsAsync_All_SendsDiscoverRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.DiscoverTestsAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(
            JsonRpcMethods.TestingDiscoverTests,
            server.ReceivedRequestMethods,
            "Expected the client to have sent a testing/discoverTests request.");
    }

    [TestMethod]
    public async Task DiscoverTestsAsync_WithUids_SendsDiscoverRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.DiscoverTestsAsync(["uid-1", "uid-2"], TestContext.CancellationToken)).ConfigureAwait(false);

        DiscoverRequestArgs args = GetSingleRequestParams<DiscoverRequestArgs>(server, JsonRpcMethods.TestingDiscoverTests);
        Assert.IsNotNull(args.TestNodes, "Expected the discover request to carry the requested UID list.");
        Assert.AreSequenceEqual(
            ["uid-1", "uid-2"],
            args.TestNodes.Select(node => node.Uid.Value),
            "Expected the exact requested UIDs to reach the wire, in order.");
        Assert.IsNull(args.GraphFilter, "A UID-based discover must not send a graph filter.");
    }

    [TestMethod]
    public async Task DiscoverTestsWithFilterAsync_SendsDiscoverRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.DiscoverTestsWithFilterAsync("/*/*/*/MyTestClass/*", TestContext.CancellationToken)).ConfigureAwait(false);

        DiscoverRequestArgs args = GetSingleRequestParams<DiscoverRequestArgs>(server, JsonRpcMethods.TestingDiscoverTests);
        Assert.AreEqual("/*/*/*/MyTestClass/*", args.GraphFilter, "Expected the graph filter to reach the wire.");
        Assert.IsNull(args.TestNodes, "A filter-based discover must not send an explicit UID list.");
    }

    [TestMethod]
    public async Task RunTestsAsync_All_SendsRunRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        MtpRunResult result = await WithTimeoutAsync(client.RunTestsAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(JsonRpcMethods.TestingRunTests, server.ReceivedRequestMethods);
        Assert.IsEmpty(result.Artifacts);
    }

    [TestMethod]
    public async Task RunTestsAsync_WithUids_SendsRunRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.RunTestsAsync(["uid-1"], TestContext.CancellationToken)).ConfigureAwait(false);

        RunRequestArgs args = GetSingleRequestParams<RunRequestArgs>(server, JsonRpcMethods.TestingRunTests);
        Assert.IsNotNull(args.TestNodes, "Expected the run request to carry the requested UID list.");
        Assert.AreSequenceEqual(
            ["uid-1"],
            args.TestNodes.Select(node => node.Uid.Value),
            "Expected the exact requested UID to reach the wire.");
        Assert.IsNull(args.GraphFilter, "A UID-based run must not send a graph filter.");
    }

    [TestMethod]
    public async Task RunTestsWithFilterAsync_SendsRunRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.RunTestsWithFilterAsync("/*/*/*/MyTestClass/*", TestContext.CancellationToken)).ConfigureAwait(false);

        RunRequestArgs args = GetSingleRequestParams<RunRequestArgs>(server, JsonRpcMethods.TestingRunTests);
        Assert.AreEqual("/*/*/*/MyTestClass/*", args.GraphFilter, "Expected the graph filter to reach the wire.");
        Assert.IsNull(args.TestNodes, "A filter-based run must not send an explicit UID list.");
    }

    [TestMethod]
    public async Task RunTestsAsync_MapsArtifactsToAttachments()
    {
        using FakeMtpServer server = new()
        {
            RunResponse = new RunResponseArgs([new Artifact(
                Uri: "file:///c:/artifacts/report.trx",
                Producer: "TRX",
                Type: "trx",
                DisplayName: "Test Report",
                Description: "The run's TRX report.")]),
        };
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        MtpRunResult result = await WithTimeoutAsync(client.RunTestsAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.HasCount(1, result.Artifacts);
        MtpAttachment attachment = result.Artifacts[0];
        Assert.AreEqual("file:///c:/artifacts/report.trx", attachment.Uri);
        Assert.AreEqual("TRX", attachment.Producer);
        Assert.AreEqual("trx", attachment.Type);
        Assert.AreEqual("Test Report", attachment.DisplayName);
        Assert.AreEqual("The run's TRX report.", attachment.Description);
    }

    [TestMethod]
    public async Task TestNodesUpdated_DiscoveredNode_DecodesNodeAndRunId()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        var runId = Guid.NewGuid();
        await server.SendDiscoveredTestNodeAsync(runId, "Ns.Class.TestA", "Test A").ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        Assert.AreEqual(runId, args.RunId);
        Assert.HasCount(1, args.Changes);
        MtpTestNodeUpdate update = args.Changes[0];
        Assert.AreEqual("Ns.Class.TestA", update.Uid);
        Assert.AreEqual("Test A", update.DisplayName);
        Assert.AreEqual("action", update.NodeType);
        Assert.AreEqual("discovered", update.ExecutionState);
    }

    [TestMethod]
    public async Task TestNodesUpdated_PassedNode_DecodesExecutionState()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        await server.SendPassedTestNodeAsync(Guid.NewGuid(), "Ns.Class.TestPass", "Test Pass").ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        Assert.AreEqual("passed", args.Changes[0].ExecutionState);
    }

    [TestMethod]
    public async Task TestNodesUpdated_FailedNode_DecodesErrorMessage()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        await server.SendFailedTestNodeAsync(Guid.NewGuid(), "Ns.Class.TestFail", "Test Fail", "Expected 1 but got 2.").ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        MtpTestNodeUpdate update = args.Changes[0];
        Assert.AreEqual("failed", update.ExecutionState);
        Assert.AreEqual("Expected 1 but got 2.", update.ErrorMessage);
    }

    [TestMethod]
    public async Task TestNodesUpdated_PassedNodeWithDetails_DecodesOutputAndLocation()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        await server.SendPassedTestNodeWithDetailsAsync(
            Guid.NewGuid(),
            "Ns.Class.TestDetail",
            "Test Detail",
            standardOutput: "hello stdout",
            standardError: "hello stderr",
            filePath: "/repo/src/Class.cs",
            lineStart: 41,
            lineEnd: 47).ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        // Exercises the convenience accessors on both serialization paths (net8 System.Text.Json, net462
        // Jsonite). The line numbers arrive as JSON numbers, so this also covers the numeric coercion.
        MtpTestNodeUpdate update = args.Changes[0];
        Assert.AreEqual("passed", update.ExecutionState);
        Assert.AreEqual("hello stdout", update.StandardOutput);
        Assert.AreEqual("hello stderr", update.StandardError);
        Assert.AreEqual("/repo/src/Class.cs", update.FilePath);
        Assert.AreEqual(41, update.LineStart);
        Assert.AreEqual(47, update.LineEnd);
    }

    [TestMethod]
    public async Task TestNodesUpdated_MultibyteDisplayName_SurvivesRoundTrip()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // A UID and display name whose UTF-8 byte count exceeds their UTF-16 char count, including a
        // surrogate-pair emoji. The transport declares Content-Length in UTF-8 *bytes*, so if either the
        // server write or the client read miscounted using chars the framing would truncate or desynchronize
        // and this exact round-trip would fail (or the read loop would hang and time out). ASCII-only payloads
        // never exercise this, so this test guards the byte/char boundary the transport framing depends on.
        // "Ns.Class.Tëst_日本語_✓_🧪_Метод" and "Tëst 日本語 ✓ 🧪 Метод".
        const string uid = "Ns.Class.T\u00EBst_\u65E5\u672C\u8A9E_\u2713_\U0001F9EA_\u041C\u0435\u0442\u043E\u0434";
        const string displayName = "T\u00EBst \u65E5\u672C\u8A9E \u2713 \U0001F9EA \u041C\u0435\u0442\u043E\u0434";

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        var runId = Guid.NewGuid();
        await server.SendDiscoveredTestNodeAsync(runId, uid, displayName).ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        Assert.AreEqual(runId, args.RunId);
        MtpTestNodeUpdate update = args.Changes[0];
        Assert.AreEqual(uid, update.Uid, "The multibyte UID must survive the transport byte-exact.");
        Assert.AreEqual(displayName, update.DisplayName, "The multibyte display name must survive the transport byte-exact.");
    }

    [TestMethod]
    public async Task TestNodesUpdated_LargeMultibytePayload_SurvivesRoundTrip()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // A multi-KB standardOutput built from a multibyte unit ("日本語-✓-🧪-"). It is far larger than a single
        // socket read, so the client's read loop must accumulate the exact UTF-8 byte count across multiple
        // reads. A char-vs-byte length mistake anywhere on the read path corrupts or truncates the tail, so
        // asserting the full string round-trips exactly guards the large-frame and the multibyte paths at once.
        string unit = "\u65E5\u672C\u8A9E-\u2713-\U0001F9EA-";
        string largeOutput = string.Concat(Enumerable.Repeat(unit, 5000));

        Task<MtpTestNodeUpdateEventArgs> updateTask = WaitForEventAsync<MtpTestNodeUpdateEventArgs>(h => client.TestNodesUpdated += h);
        await server.SendPassedTestNodeWithDetailsAsync(
            Guid.NewGuid(),
            "Ns.Class.TestLarge",
            "Test Large",
            standardOutput: largeOutput,
            standardError: "stderr",
            filePath: "/repo/src/Class.cs",
            lineStart: 1,
            lineEnd: 2).ConfigureAwait(false);

        MtpTestNodeUpdateEventArgs args = await WithTimeoutAsync(updateTask).ConfigureAwait(false);

        MtpTestNodeUpdate update = args.Changes[0];
        Assert.AreEqual(largeOutput.Length, update.StandardOutput?.Length, "The large multibyte payload must not be truncated.");
        Assert.AreEqual(largeOutput, update.StandardOutput, "The large multibyte payload must survive the transport byte-exact.");
    }

    [TestMethod]
    public async Task TestNodesUpdated_CompletionSentinel_IsSkipped()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        var updates = new List<MtpTestNodeUpdateEventArgs>();
        var realUpdateReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.TestNodesUpdated += (_, e) =>
        {
            lock (updates)
            {
                updates.Add(e);
            }

            realUpdateReceived.TrySetResult(true);
        };

        var runId = Guid.NewGuid();

        // The completion sentinel (null Changes) is delivered first; ordered TCP delivery guarantees the client
        // has processed and skipped it by the time the real update raises the event.
        await server.SendTestNodesCompletionAsync(runId).ConfigureAwait(false);
        await server.SendDiscoveredTestNodeAsync(runId, "Ns.Class.TestA", "Test A").ConfigureAwait(false);

        await WithTimeoutAsync(realUpdateReceived.Task).ConfigureAwait(false);

        lock (updates)
        {
            Assert.HasCount(1, updates);
            Assert.AreEqual("Ns.Class.TestA", updates[0].Changes[0].Uid);
        }
    }

    [TestMethod]
    public async Task LogReceived_DecodesLevelAndMessage()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpLogEventArgs> logTask = WaitForEventAsync<MtpLogEventArgs>(h => client.LogReceived += h);
        await server.SendLogAsync("hello from the server").ConfigureAwait(false);

        MtpLogEventArgs args = await WithTimeoutAsync(logTask).ConfigureAwait(false);

        Assert.AreEqual("Information", args.Level);
        Assert.AreEqual("hello from the server", args.Message);
    }

    [TestMethod]
    public async Task TelemetryReceived_DecodesEventNameAndMetrics()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpTelemetryEventArgs> telemetryTask = WaitForEventAsync<MtpTelemetryEventArgs>(h => client.TelemetryReceived += h);
        await server.SendTelemetryAsync("run/complete", new Dictionary<string, object> { ["testCount"] = 7 }).ConfigureAwait(false);

        MtpTelemetryEventArgs args = await WithTimeoutAsync(telemetryTask).ConfigureAwait(false);

        Assert.AreEqual("run/complete", args.EventName);
        Assert.IsTrue(args.Metrics.TryGetValue("testCount", out object? value));
        Assert.AreEqual(7L, Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task AttachmentsReceived_DecodesAttachments()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpAttachmentsEventArgs> attachmentsTask = WaitForEventAsync<MtpAttachmentsEventArgs>(h => client.AttachmentsReceived += h);
        await server.SendAttachmentAsync(
            uri: "file:///c:/coverage/coverage.cobertura.xml",
            producer: "CodeCoverage",
            type: "coverage",
            displayName: "Coverage",
            description: "Cobertura coverage report.").ConfigureAwait(false);

        MtpAttachmentsEventArgs args = await WithTimeoutAsync(attachmentsTask).ConfigureAwait(false);

        Assert.HasCount(1, args.Attachments);
        MtpAttachment attachment = args.Attachments[0];
        Assert.AreEqual("file:///c:/coverage/coverage.cobertura.xml", attachment.Uri);
        Assert.AreEqual("CodeCoverage", attachment.Producer);
        Assert.AreEqual("coverage", attachment.Type);
        Assert.AreEqual("Coverage", attachment.DisplayName);
        Assert.AreEqual("Cobertura coverage report.", attachment.Description);
    }

    [TestMethod]
    public async Task ExitAsync_SendsExitNotification()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.ExitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        await server.WaitForNotificationAsync(JsonRpcMethods.Exit, DefaultTimeout).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RunTestsAsync_Cancellation_SendsCancelRequestAndThrows()
    {
        using FakeMtpServer server = new() { WithholdRunResponse = true };
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);
        using var cts = new CancellationTokenSource();

        Task<MtpRunResult> runTask = client.RunTestsAsync(cts.Token);

        // Let the request go out and register as pending before cancelling, so the connection emits the
        // $/cancelRequest notification instead of failing synchronously.
        await Task.Delay(100, TestContext.CancellationToken).ConfigureAwait(false);
#pragma warning disable VSTHRD103 // Call async methods when in an async method - CancelAsync() is .NET 8+ only and this test also targets net462.
        cts.Cancel();
#pragma warning restore VSTHRD103

        await AssertThrowsAsync<OperationCanceledException>(() => runTask).ConfigureAwait(false);
        await server.WaitForNotificationAsync(JsonRpcMethods.CancelRequest, DefaultTimeout).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RunTestsAsync_NumericStringResponseId_DoesNotCompleteNumericRequest()
    {
        using FakeMtpServer server = new() { WithholdRunResponse = true };
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpRunResult> runTask = client.RunTestsAsync(TestContext.CancellationToken);
        RequestMessage request = await server.WaitForRequestAsync(JsonRpcMethods.TestingRunTests, DefaultTimeout).ConfigureAwait(false);
        Task<MtpLogEventArgs> responseProcessed = WaitForEventAsync<MtpLogEventArgs>(handler => client.LogReceived += handler);

        await server.SendRunResponseAsync(request, useStringId: true).ConfigureAwait(false);
        await server.SendLogAsync("response barrier").ConfigureAwait(false);
        _ = await WithTimeoutAsync(responseProcessed).ConfigureAwait(false);
        Assert.IsFalse(runTask.IsCompleted);

        await server.SendRunResponseAsync(request, useStringId: false).ConfigureAwait(false);
        _ = await WithTimeoutAsync(runTask).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReadLoop_MalformedFrame_FailsPendingRequestWithClientException()
    {
        using FakeMtpServer server = new() { WithholdRunResponse = true };
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpRunResult> runTask = client.RunTestsAsync(TestContext.CancellationToken);
        await Task.Delay(100, TestContext.CancellationToken).ConfigureAwait(false);

        server.SendRawFrame("{ this is : not valid json ");

        MtpServerClientException exception = await AssertThrowsAsync<MtpServerClientException>(() => runTask).ConfigureAwait(false);

        // A malformed frame fails the read loop with the base exception, not the connection-closed subtype.
        Assert.AreEqual(typeof(MtpServerClientException), exception.GetType());
    }

    [TestMethod]
    public async Task ReadLoop_ServerDisconnect_FailsPendingRequestWithClosedException()
    {
        using FakeMtpServer server = new() { WithholdRunResponse = true };
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        Task<MtpRunResult> runTask = client.RunTestsAsync(TestContext.CancellationToken);
        await Task.Delay(100, TestContext.CancellationToken).ConfigureAwait(false);

        server.CloseConnection();

        await AssertThrowsAsync<MtpServerConnectionClosedException>(() => runTask).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ServerInitiatedRequest_NoHandler_RespondsWithNull()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // ServerRequestHandler is null by default, so the client declines with a null result.
        ResponseMessage response = await WithTimeoutAsync(server.SendServerRequestAsync(ClientAttachDebuggerMethod)).ConfigureAwait(false);

        Assert.IsNull(response.Result);
    }

    [TestMethod]
    public async Task ServerInitiatedRequest_NumericStringId_PreservesResponseIdRepresentation()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        ResponseMessage response = await WithTimeoutAsync(
            server.SendServerRequestAsync(ClientAttachDebuggerMethod, useStringId: true)).ConfigureAwait(false);

        Assert.AreEqual(response.Id.ToString(CultureInfo.InvariantCulture), response.StringId);
    }

    [TestMethod]
    public async Task ServerInitiatedRequest_WithHandler_InvokesHandler()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        string? observedMethod = null;
        client.ServerRequestHandler = (method, parameters, cancellationToken) =>
        {
            _ = parameters;
            _ = cancellationToken;
            observedMethod = method;
            return Task.FromResult<IDictionary<string, object?>?>(null);
        };

        await WithTimeoutAsync(server.SendServerRequestAsync(ClientAttachDebuggerMethod)).ConfigureAwait(false);

        Assert.AreEqual(ClientAttachDebuggerMethod, observedMethod);
    }

    [TestMethod]
    public async Task ServerInitiatedRequest_WithNonNullDictionaryResult_RoundTripsResultOverTheWire()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // The protocol's attach-debugger response is a small object (e.g. { success: bool }). Returning a
        // non-null Dictionary<string, object?> exercises the client-only response-dictionary pass-through
        // serializer on BOTH formatter paths (Jsonite on net462, System.Text.Json on net8). Without it the
        // response write throws and the server waits forever, so a regression surfaces here as a timeout.
        client.ServerRequestHandler = (method, parameters, cancellationToken) =>
        {
            _ = method;
            _ = parameters;
            _ = cancellationToken;
            return Task.FromResult<IDictionary<string, object?>?>(new Dictionary<string, object?>
            {
                ["success"] = true,
                ["detail"] = "attached",
            });
        };

        ResponseMessage response = await WithTimeoutAsync(server.SendServerRequestAsync(ClientAttachDebuggerMethod)).ConfigureAwait(false);

        Assert.IsInstanceOfType(response.Result, typeof(IDictionary<string, object?>));
        var result = (IDictionary<string, object?>)response.Result!;
        Assert.IsTrue((bool)result["success"]!, "Expected the boolean payload to survive the round trip.");
        Assert.AreEqual("attached", result["detail"], "Expected the string payload to survive the round trip.");
    }

    [TestMethod]
    public async Task ServerInitiatedRequest_WithNonDictionaryIDictionaryResult_RoundTripsResultOverTheWire()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // A handler may return ANY IDictionary<string, object?>, not just the concrete Dictionary<string,
        // object?> the response serializer is registered for. The client normalizes the result to that
        // concrete type before writing, so a SortedDictionary must still round-trip. Before that
        // normalization the serialize threw on the unregistered runtime type and the server waited forever,
        // so a regression surfaces here as a timeout.
        client.ServerRequestHandler = (method, parameters, cancellationToken) =>
        {
            _ = method;
            _ = parameters;
            _ = cancellationToken;
            return Task.FromResult<IDictionary<string, object?>?>(new SortedDictionary<string, object?>
            {
                ["success"] = true,
                ["detail"] = "attached",
            });
        };

        ResponseMessage response = await WithTimeoutAsync(server.SendServerRequestAsync(ClientAttachDebuggerMethod)).ConfigureAwait(false);

        Assert.IsInstanceOfType(response.Result, typeof(IDictionary<string, object?>));
        var result = (IDictionary<string, object?>)response.Result!;
        Assert.IsTrue((bool)result["success"]!, "Expected the boolean payload to survive the round trip.");
        Assert.AreEqual("attached", result["detail"], "Expected the string payload to survive the round trip.");
    }

    [TestMethod]
    public async Task Dispose_CalledFromNotificationHandler_DoesNotSelfWaitOnTheReadLoop()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        // A consumer may dispose the client from inside a notification handler. That handler runs on the
        // read loop's own execution flow, so the connection's Dispose must detect the re-entrancy and skip
        // synchronously waiting on the read loop from within itself. If it does not, Dispose blocks for the
        // full read-loop shutdown timeout (5s) because the loop cannot finish while it is parked in this
        // handler. Asserting the handler-triggered Dispose returns well under that timeout guards the fix;
        // before it, this elapsed jumps to ~5s.
        var disposeElapsed = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.TestNodesUpdated += (_, _) =>
        {
            var stopwatch = Stopwatch.StartNew();
            client.Dispose();
            stopwatch.Stop();
            disposeElapsed.TrySetResult(stopwatch.Elapsed);
        };

        await server.SendDiscoveredTestNodeAsync(Guid.NewGuid(), "Ns.Class.Test", "Test").ConfigureAwait(false);

        TimeSpan elapsed = await WithTimeoutAsync(disposeElapsed.Task).ConfigureAwait(false);

        Assert.IsLessThan(
            TimeSpan.FromSeconds(2),
            elapsed,
            $"Dispose from a notification handler took {elapsed.TotalMilliseconds:F0} ms; it must not self-wait on the read loop.");
    }

    [TestMethod]
    public async Task ShutdownAsync_WithBlockedNotificationHandler_ReturnsWithoutBlockingTheCaller()
    {
        using FakeMtpServer server = new();
        MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);
        var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.TestNodesUpdated += (_, _) =>
        {
            handlerEntered.TrySetResult(true);
            releaseHandler.Task.GetAwaiter().GetResult();
        };

        try
        {
            await server.SendDiscoveredTestNodeAsync(Guid.NewGuid(), "Ns.Class.Test", "Test").ConfigureAwait(false);
            await WithTimeoutAsync(handlerEntered.Task).ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();
            Task shutdown = client.ShutdownAsync();
            stopwatch.Stop();

            Assert.IsLessThan(
                TimeSpan.FromSeconds(2),
                stopwatch.Elapsed,
                $"Calling ShutdownAsync took {stopwatch.Elapsed.TotalMilliseconds:F0} ms; connection teardown must be scheduled rather than blocking the caller.");

            _ = releaseHandler.TrySetResult(true);
            await WithTimeoutAsync(shutdown).ConfigureAwait(false);
        }
        finally
        {
            _ = releaseHandler.TrySetResult(true);
            client.Dispose();
        }
    }

    private static async Task<MtpServerClient> ConnectAndInitializeAsync(FakeMtpServer server)
    {
        MtpServerClient client = server.ConnectClient();
        await WithTimeoutAsync(client.InitializeAsync()).ConfigureAwait(false);
        return client;
    }

    private static Task<T> WaitForEventAsync<T>(Action<EventHandler<T>> subscribe)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscribe((_, e) => tcs.TrySetResult(e));
        return tcs.Task;
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(DefaultTimeout)).ConfigureAwait(false);
        return completed != task
            ? throw new TimeoutException("Timed out waiting for the operation to complete.")
            : await task.ConfigureAwait(false);
    }

    private static async Task WithTimeoutAsync(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(DefaultTimeout)).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException("Timed out waiting for the operation to complete.");
        }

        await task.ConfigureAwait(false);
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await WithTimeoutAsync(action()).ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }

        Assert.Fail($"Expected an exception of type {typeof(TException).Name}, but none was thrown.");
        throw new InvalidOperationException("Unreachable.");
    }

    private static T GetSingleRequestParams<T>(FakeMtpServer server, string method)
        where T : class
    {
        RequestMessage request = server.ReceivedRequests.Single(received => received.Method == method);
        Assert.IsNotNull(request.Params, $"Expected the '{method}' request to carry deserialized params.");

        // The System.Text.Json decoder strong-types incoming request params, so Params is already T. The
        // client package's Jsonite RpcMessage decoder instead keeps them as a raw property bag (the real
        // client never receives discover/run requests, so it decodes per method itself). Normalize both
        // formatter paths to the typed args via the registered deserializer so the wire-payload assertions
        // hold identically on the net8 (STJ) and net462 (Jsonite) legs.
        if (request.Params is T typed)
        {
            return typed;
        }

        Assert.IsInstanceOfType(
            request.Params,
            typeof(IDictionary<string, object?>),
            $"Expected the '{method}' request params to be {typeof(T).Name} or a raw property bag.");
        return SerializerUtilities.Deserialize<T>((IDictionary<string, object?>)request.Params);
    }
}
