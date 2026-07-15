// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Client;

namespace Microsoft.Testing.Platform.ServerClient.UnitTests;

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
        using MtpServerClient client = server.ConnectClient();

        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.AreEqual(4242, capabilities.ServerProcessId);
        Assert.AreEqual("FakeMtpServer", capabilities.ServerName);
        Assert.AreEqual("1.2.3", capabilities.ServerVersion);
        Assert.IsTrue(capabilities.SupportsDiscovery);
        Assert.IsTrue(capabilities.MultiRequestSupport);
        Assert.IsFalse(capabilities.VSTestProviderSupport);
        Assert.IsTrue(capabilities.SupportsAttachments);
        Assert.IsFalse(capabilities.MultiConnectionProvider);
        Assert.AreSame(capabilities, client.Capabilities);
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

        Assert.Contains(JsonRpcMethods.TestingDiscoverTests, server.ReceivedRequestMethods);
    }

    [TestMethod]
    public async Task DiscoverTestsWithFilterAsync_SendsDiscoverRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.DiscoverTestsWithFilterAsync("/*/*/*/MyTestClass/*", TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(JsonRpcMethods.TestingDiscoverTests, server.ReceivedRequestMethods);
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

        Assert.Contains(JsonRpcMethods.TestingRunTests, server.ReceivedRequestMethods);
    }

    [TestMethod]
    public async Task RunTestsWithFilterAsync_SendsRunRequest()
    {
        using FakeMtpServer server = new();
        using MtpServerClient client = await ConnectAndInitializeAsync(server).ConfigureAwait(false);

        await WithTimeoutAsync(client.RunTestsWithFilterAsync("/*/*/*/MyTestClass/*", TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.Contains(JsonRpcMethods.TestingRunTests, server.ReceivedRequestMethods);
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
            return Task.FromResult<object?>(null);
        };

        await WithTimeoutAsync(server.SendServerRequestAsync(ClientAttachDebuggerMethod)).ConfigureAwait(false);

        Assert.AreEqual(ClientAttachDebuggerMethod, observedMethod);
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
}
