// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO.Pipes;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Microsoft.Win32.SafeHandles;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetrySharedHelperMutationTests
{
    [TestMethod]
    public void PathContainment_ComparisonAndComparerPreservePlatformCaseRules()
    {
        MethodInfo getComparison = typeof(PathContainment).GetMethod(
            "GetComparison",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo getComparer = typeof(RetryArtifactProcessor).GetMethod(
            "GetPathComparer",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.AreEqual(StringComparison.OrdinalIgnoreCase, getComparison.Invoke(null, [true]));
        Assert.AreEqual(StringComparison.Ordinal, getComparison.Invoke(null, [false]));
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, getComparer.Invoke(null, [true]));
        Assert.AreSame(StringComparer.Ordinal, getComparer.Invoke(null, [false]));
    }

    [DataRow(null, false, 0)]
    [DataRow("", false, 0)]
    [DataRow("   ", false, 0)]
    [DataRow("invalid", false, 0)]
    [DataRow("200", true, 0.2)]
    [DataRow("1ms", true, 0.001)]
    [DataRow("1mil", true, 0.001)]
    [DataRow("1milliseconds", true, 0.001)]
    [DataRow("1s", true, 1)]
    [DataRow("1m", true, 60)]
    [DataRow("1h", true, 3600)]
    [DataRow("1d", true, 86400)]
    [TestMethod]
    public void TimeSpanParser_HandlesEveryBranch(string? input, bool expectedSuccess, double expectedSeconds)
    {
        bool success = TryParseTimeSpan(input, out TimeSpan result);

        Assert.AreEqual(expectedSuccess, success);
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [TestMethod]
    public void TimeSpanParser_RequireSuffixAndUnrepresentableNumberAreRejected()
    {
        Assert.IsFalse(TryParseTimeSpanRequireSuffix("200", out TimeSpan noSuffix));
        Assert.AreEqual(TimeSpan.Zero, noSuffix);

        Assert.IsFalse(TryParseTimeSpan(new string('9', 400), out TimeSpan tooLarge));
        Assert.AreEqual(TimeSpan.Zero, tooLarge);
    }

    [TestMethod]
    public void TimeoutHelper_DefaultsArePositiveAndConsistent()
    {
        Type timeoutType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeoutHelper");
        var timeout = (TimeSpan)RetryLinkedTypes.GetStaticMember(timeoutType, "DefaultHangTimeSpanTimeout");
        double timeoutSeconds = (double)RetryLinkedTypes.GetStaticMember(timeoutType, "DefaultHangTimeoutSeconds");

        Assert.IsGreaterThan(TimeSpan.Zero, timeout);
        Assert.AreEqual(timeout.TotalSeconds, timeoutSeconds);
    }

    [TestMethod]
    public void RetryArtifactManifest_RejectsMissingOrLeadingSeparatorAndPreservesExactPayload()
    {
        AssertSplit("path", expectedSuccess: false, expectedPath: null, expectedKind: null);
        AssertSplit("\tkind", expectedSuccess: false, expectedPath: null, expectedKind: null);
        AssertSplit("path\t", expectedSuccess: true, expectedPath: "path", expectedKind: string.Empty);
        AssertSplit("path\tkind", expectedSuccess: true, expectedPath: "path", expectedKind: "kind");
    }

    private static void AssertSplit(
        string line,
        bool expectedSuccess,
        string? expectedPath,
        string? expectedKind)
    {
        Type manifestType = RetryLinkedTypes.Get("Microsoft.Testing.Extensions.RetryArtifactManifest");
        object?[] arguments = [line, null, null];
        bool success = (bool)RetryLinkedTypes.InvokeStatic(
            manifestType,
            "TrySplitEntry",
            arguments)!;

        Assert.AreEqual(expectedSuccess, success);
        Assert.AreEqual(expectedPath, arguments[1]);
        Assert.AreEqual(expectedKind, arguments[2]);
    }

    private static bool TryParseTimeSpan(string? input, out TimeSpan result)
    {
        Type parserType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeSpanParser");
        Type unitType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeSpanDefaultUnit");
        object milliseconds = Enum.Parse(unitType, "Milliseconds");
        MethodInfo tryParse = parserType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "TryParse"
                && method.GetParameters() is [_, { ParameterType: var parameterType }, _]
                && parameterType == unitType);
        object?[] arguments = [input, milliseconds, TimeSpan.Zero];

        bool success = (bool)tryParse.Invoke(null, arguments)!;
        result = (TimeSpan)arguments[2]!;
        return success;
    }

    private static bool TryParseTimeSpanRequireSuffix(string input, out TimeSpan result)
    {
        Type parserType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeSpanParser");
        MethodInfo tryParse = parserType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "TryParseRequireSuffix" && method.GetParameters().Length == 2);
        object?[] arguments = [input, TimeSpan.Zero];

        bool success = (bool)tryParse.Invoke(null, arguments)!;
        result = (TimeSpan)arguments[1]!;
        return success;
    }
}

[TestClass]
public sealed class RetryNamedPipeMutationTests
{
    private const string PackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NamedPipeClient_DefaultsToExitOnConnectionLossAndSelectsExactOptions()
    {
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type securityType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");
        string sandboxedPrefix = (string)RetryLinkedTypes.GetStaticMember(securityType, "SandboxedApplicationPipeNamePrefix");
        TargetInvocationException nullNameException = Assert.ThrowsExactly<TargetInvocationException>(
            () => RetryLinkedTypes.Create(clientType, [null]));
        Assert.IsInstanceOfType<ArgumentNullException>(nullNameException.InnerException);

        object client = RetryLinkedTypes.Create(clientType, "pipe", RetryLinkedTypes.CreateImplementation(
            clientType.GetConstructors().Single(constructor => constructor.GetParameters().Length == 2).GetParameters()[1].ParameterType,
            "Microsoft.Testing.Platform.Helpers.SystemEnvironment"));

        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(client, "_exitProcessOnConnectionLoss"));
        Assert.AreEqual(
            PipeOptions.Asynchronous,
            RetryLinkedTypes.InvokeStatic(clientType, "GetPipeOptions", sandboxedPrefix + "pipe"));

        PipeOptions expectedRegular = PipeOptions.Asynchronous;
#if NET
        expectedRegular |= PipeOptions.CurrentUserOnly;
#endif
        Assert.AreEqual(expectedRegular, RetryLinkedTypes.InvokeStatic(clientType, "GetPipeOptions", "pipe"));

        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public void NamedPipeClient_DisposeRecordsStateAndDisposesEveryOwnedResource()
    {
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        object client = RetryLinkedTypes.Create(clientType, "pipe");
        SemaphoreSlim gate = GetPrivateFieldFromHierarchy<SemaphoreSlim>(client, "_lock");
        MemoryStream serializationBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_serializationBuffer");
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_messageBuffer");

        ((IDisposable)client).Dispose();
        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(client, "_disposed"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => gate.Wait(0, TestContext.CancellationToken));
        Assert.ThrowsExactly<ObjectDisposedException>(() => gate.Wait(0, TestContext.CancellationToken));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = serializationBuffer.Length);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = messageBuffer.Length);
        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public async Task NamedPipeClient_CanceledSemaphoreWaitStopsBeforeRequestSerialization()
    {
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type requestType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.GetListOfFailedTestsRequest");
        Type responseType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Models.VoidResponse");
        object client = RetryLinkedTypes.Create(clientType, "pipe");
        SemaphoreSlim gate = GetPrivateFieldFromHierarchy<SemaphoreSlim>(client, "_lock");
        await gate.WaitAsync(TestContext.CancellationToken);
        MethodInfo requestReply = clientType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "RequestReplyAsync" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(requestType, responseType);
        object request = RetryLinkedTypes.Create(requestType);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            var task = (Task)requestReply.Invoke(client, [request, cancellation.Token])!;
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        }
        finally
        {
            gate.Release();
            ((IDisposable)client).Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeClient_RequestReplyWritesRequestAndReceivesResponse()
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type requestType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.GetListOfFailedTestsRequest");
        Type requestSerializerType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.GetListOfFailedTestsRequestSerializer");
        Type responseType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Models.VoidResponse");
        Type responseSerializerType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Serializers.VoidResponseSerializer");
        object pipeName = RetryLinkedTypes.InvokeStatic(serverType, "GetPipeName", Guid.NewGuid().ToString("N"))!;
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;
        object server = CreateServer(pipeName, Mock.Of<ILogger>());
        object client = RetryLinkedTypes.Create(clientType, pipeNameValue);
        MethodInfo registerServer = serverType.GetMethod(
            "RegisterSerializer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        MethodInfo registerClient = clientType.GetMethod(
            "RegisterSerializer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        object requestSerializer = RetryLinkedTypes.Create(requestSerializerType);
        object responseSerializer = RetryLinkedTypes.Create(responseSerializerType);
        NamedPipeClientStream pipeStream = GetPrivateFieldFromHierarchy<NamedPipeClientStream>(
            client,
            "_namedPipeClientStream");
        SafePipeHandle? clientHandle = null;
        registerServer.Invoke(server, [requestSerializer, requestType]);
        registerServer.Invoke(server, [responseSerializer, responseType]);
        registerClient.Invoke(client, [requestSerializer, requestType]);
        registerClient.Invoke(client, [responseSerializer, responseType]);

        try
        {
            Task connection = InvokeTaskAsync(server, "WaitConnectionAsync", TestContext.CancellationToken);
            await InvokeTaskAsync(client, "ConnectAsync", TestContext.CancellationToken);
            await connection;
            clientHandle = pipeStream.SafePipeHandle;

            MethodInfo requestReply = clientType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name == "RequestReplyAsync" && method.IsGenericMethodDefinition)
                .MakeGenericMethod(requestType, responseType);
            var responseTask = (Task)requestReply.Invoke(
                client,
                [RetryLinkedTypes.Create(requestType), TestContext.CancellationToken])!;
            await responseTask;

            Assert.AreSame(
                RetryLinkedTypes.GetStaticMember(responseType, "CachedInstance"),
                responseTask.GetType().GetProperty("Result")!.GetValue(responseTask));
        }
        finally
        {
            ((IDisposable)client).Dispose();
            Assert.IsNotNull(clientHandle);
            Assert.IsTrue(clientHandle!.IsClosed);
            ((IDisposable)server).Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeConnection_WriteMessageBuildsExactFrameAndFlushes()
    {
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type serializerType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.FailedTestRequestSerializer");
        Type requestType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.FailedTestRequest");
        object client = RetryLinkedTypes.Create(clientType, "pipe");
        object serializer = RetryLinkedTypes.Create(serializerType);
        object request = RetryLinkedTypes.Create(requestType, "uid", "display");
        using var stream = new TrackingMemoryStream();

        await InvokeTaskAsync(client, "WriteMessageAsync", stream, serializer, request, TestContext.CancellationToken);

        byte[] frame = stream.ToArray();
        Assert.AreEqual(frame.Length - sizeof(int), BitConverter.ToInt32(frame, 0));
        Assert.AreEqual((int)serializerType.GetProperty("Id")!.GetValue(serializer)!, BitConverter.ToInt32(frame, sizeof(int)));
        Assert.IsTrue(stream.FlushCalled);
        Assert.AreEqual(0L, GetPrivateFieldFromHierarchy<MemoryStream>(client, "_messageBuffer").Position);
        Assert.AreEqual(0L, GetPrivateFieldFromHierarchy<MemoryStream>(client, "_serializationBuffer").Position);

        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public async Task NamedPipeConnection_ReadsSplitFramesRejectsInvalidBoundariesAndClearsBuffer()
    {
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type serializerType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.FailedTestRequestSerializer");
        Type responseType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.FailedTestRequest");
        object client = RetryLinkedTypes.Create(clientType, "pipe");
        object serializer = RetryLinkedTypes.Create(serializerType);
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_messageBuffer");
        clientType
            .GetMethod("RegisterSerializer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(client, [serializer, responseType]);
        int serializerId = (int)serializerType.GetProperty("Id")!.GetValue(serializer)!;
        object request = RetryLinkedTypes.Create(responseType, "uid", "display");
        using var body = new MemoryStream();
        RetryLinkedTypes.InvokeInstance(serializer, "Serialize", request, body);
        int payloadLength = sizeof(int) + checked((int)body.Length);
        byte[] validFrame =
        [
            .. BitConverter.GetBytes(payloadLength),
            .. BitConverter.GetBytes(serializerId),
            .. body.ToArray(),
        ];

        using (var stream = new StrictSplitReadStream(validFrame))
        {
            object? response = await InvokeTaskWithResultAsync(
                client,
                "ReadNextMessageAsync",
                stream,
                TestContext.CancellationToken,
                validFrame.Length);
            Assert.IsNotNull(response);
            Assert.AreEqual("uid", responseType.GetProperty("Uid")!.GetValue(response));
            Assert.AreEqual("display", responseType.GetProperty("DisplayName")!.GetValue(response));
        }

        // Recreate the stale-buffer state that existed before the reset was restored: a previous longer payload
        // remains addressable while the next complete frame contains only the serializer ID. The shorter frame
        // must fail in its serializer instead of consuming the previous payload's UID and display name.
        messageBuffer.Write(validFrame, sizeof(int), payloadLength);
        messageBuffer.Position = 0;
        byte[] shortFrame =
        [
            .. BitConverter.GetBytes(sizeof(int)),
            .. BitConverter.GetBytes(serializerId),
        ];
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => InvokeTaskAsync(
            client,
            "ReadNextMessageAsync",
            new MemoryStream(shortFrame),
            TestContext.CancellationToken,
            shortFrame.Length));

        using (var empty = new MemoryStream())
        {
            Assert.IsNull(await InvokeTaskWithResultAsync(
                client,
                "ReadNextMessageAsync",
                empty,
                TestContext.CancellationToken,
                int.MaxValue));
        }

        using (var partialHeader = new MemoryStream([1, 2]))
        {
            Assert.IsNull(await InvokeTaskWithResultAsync(
                client,
                "ReadNextMessageAsync",
                partialHeader,
                TestContext.CancellationToken,
                int.MaxValue));
        }

        IOException invalidFrame = await Assert.ThrowsExactlyAsync<IOException>(() => InvokeTaskAsync(
            client,
            "ReadNextMessageAsync",
            new MemoryStream(BitConverter.GetBytes(sizeof(int) - 1)),
            TestContext.CancellationToken,
            int.MaxValue));
        Assert.AreEqual(
            $"The transport returned an invalid frame payload length of {sizeof(int) - 1} bytes.",
            invalidFrame.Message);
        await AssertInvocationThrowsAsync<IOException>(
            client,
            "ReadNextMessageAsync",
            new MemoryStream(BitConverter.GetBytes(sizeof(int) + 1)),
            TestContext.CancellationToken,
            2 * sizeof(int));

        Assert.AreEqual(0L, messageBuffer.Length);
        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public async Task NamedPipeServer_ConnectDisconnectLogsExactMessagesAndRecordsState()
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        Type clientType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        object pipeName = RetryLinkedTypes.InvokeStatic(serverType, "GetPipeName", Guid.NewGuid().ToString("N"))!;
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;
        ConcurrentQueue<string> messages = [];
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => messages.Enqueue(message))
            .Returns(Task.CompletedTask);
        object server = CreateServer(pipeName, logger.Object);
        object client = RetryLinkedTypes.Create(clientType, pipeNameValue);

        try
        {
            Task connection = InvokeTaskAsync(server, "WaitConnectionAsync", TestContext.CancellationToken);
            await InvokeTaskAsync(client, "ConnectAsync", TestContext.CancellationToken);
            await connection;

            Assert.IsTrue((bool)serverType.GetProperty("WasConnected")!.GetValue(server)!);
            Assert.Contains($"Waiting for connection for the pipe name {pipeNameValue}", messages);
            Assert.Contains($"Client connected to {pipeNameValue}", messages);

            var dispose = Task.Run(((IDisposable)server).Dispose, TestContext.CancellationToken);
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => GetPrivateFieldFromHierarchy<bool>(server, "_disposed"),
                    TimeSpan.FromSeconds(30)));
            Assert.IsFalse(dispose.IsCompleted);

            ((IDisposable)client).Dispose();
            Task loopTask = GetPrivateFieldFromHierarchy<Task>(server, "_loopTask");
            await loopTask;
            await dispose;
            Assert.Contains($"Client disconnected from pipe '{pipeNameValue}', exiting read loop", messages);
            Assert.HasCount(3, messages);
        }
        finally
        {
            ((IDisposable)client).Dispose();
            ((IDisposable)server).Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeServer_PreCanceledWaitLogsAndDoesNotReportConnection()
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        object pipeName = RetryLinkedTypes.InvokeStatic(serverType, "GetPipeName", Guid.NewGuid().ToString("N"))!;
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        object server = CreateServer(pipeName, logger.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => InvokeTaskAsync(server, "WaitConnectionAsync", cancellation.Token));
            Assert.IsFalse((bool)serverType.GetProperty("WasConnected")!.GetValue(server)!);
            logger.Verify(
                x => x.LogAsync(
                    LogLevel.Debug,
                    $"Waiting for connection for the pipe name {pipeNameValue}",
                    null,
                    LoggingExtensions.Formatter),
                Times.Once);
        }
        finally
        {
            ((IDisposable)server).Dispose();
        }
    }

    [TestMethod]
    public void NamedPipeServer_PlatformNameSecurityDecisionAndDisposalAreExact()
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        Type securityType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");
        object pipeName = RetryLinkedTypes.InvokeStatic(serverType, "GetPipeName", @"folder\name")!;
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;

        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.IsTrue(Path.IsPathRooted(pipeNameValue));
        }
        else
        {
            Assert.AreEqual("testingplatform.pipe.folder.name", pipeNameValue);
        }

        object unqualifiedPipeName = RetryLinkedTypes.InvokeStatic(
            serverType,
            "GetPipeName",
            Guid.NewGuid().ToString("N"))!;
        string originalName = (string)unqualifiedPipeName.GetType().GetProperty("Name")!.GetValue(unqualifiedPipeName)!;
        object server = CreateServer(unqualifiedPipeName, Mock.Of<ILogger>(), authorizedSecurityIdentities: null);
        object serverPipeName = serverType.GetProperty("PipeName")!.GetValue(server)!;
        Assert.AreEqual(
            originalName,
            serverPipeName.GetType().GetProperty("Name")!.GetValue(serverPipeName));
        MemoryStream serializationBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(server, "_serializationBuffer");
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(server, "_messageBuffer");

        ((IDisposable)server).Dispose();

        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(server, "_disposed"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = serializationBuffer.Length);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = messageBuffer.Length);
        ((IDisposable)server).Dispose();

        object emptyIdentities = CreateServer(unqualifiedPipeName, Mock.Of<ILogger>(), []);
        object emptyPipeName = serverType.GetProperty("PipeName")!.GetValue(emptyIdentities)!;
        Assert.AreEqual(originalName, emptyPipeName.GetType().GetProperty("Name")!.GetValue(emptyPipeName));
        ((IDisposable)emptyIdentities).Dispose();

        object packageIdentity = CreateServer(unqualifiedPipeName, Mock.Of<ILogger>(), [PackageSid]);
        object packagePipeName = serverType.GetProperty("PipeName")!.GetValue(packageIdentity)!;
        string expectedPackageName = (bool)RetryLinkedTypes.GetStaticMember(securityType, "IsSupported")
            ? (string)RetryLinkedTypes.InvokeStatic(
                securityType,
                "GetPipeNameForSandboxedApplication",
                originalName)!
            : originalName;
        Assert.AreEqual(expectedPackageName, packagePipeName.GetType().GetProperty("Name")!.GetValue(packagePipeName));
        ((IDisposable)packageIdentity).Dispose();
    }

    [TestMethod]
    public async Task NamedPipeServer_DisposeWithConnectedPendingLoopWaitsForCompletion()
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        object pipeName = RetryLinkedTypes.InvokeStatic(
            serverType,
            "GetPipeName",
            Guid.NewGuid().ToString("N"))!;
        object server = CreateServer(pipeName, Mock.Of<ILogger>());
        var loopCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateFieldFromHierarchy(server, "<WasConnected>k__BackingField", true);
        SetPrivateFieldFromHierarchy(server, "_loopTask", loopCompletion.Task);

        var dispose = Task.Run(((IDisposable)server).Dispose, TestContext.CancellationToken);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => GetPrivateFieldFromHierarchy<bool>(server, "_disposed"),
            TimeSpan.FromSeconds(30)));
        Assert.IsFalse(
            dispose.Wait(TimeSpan.FromMilliseconds(100), TestContext.CancellationToken),
            "Dispose returned before the connected read loop completed.");

        loopCompletion.SetResult(true);
        await dispose;
    }

    private static object CreateServer(
        object pipeName,
        ILogger logger,
        IReadOnlyList<string>? authorizedSecurityIdentities = null)
    {
        Type serverType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        ConstructorInfo constructor = serverType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 8);
        ParameterInfo[] parameters = constructor.GetParameters();
        return constructor.Invoke(
            [
                pipeName,
                RetryLinkedTypes.CreatePipeCallback(parameters[1].ParameterType),
                RetryLinkedTypes.CreateImplementation(parameters[2].ParameterType, "Microsoft.Testing.Platform.Helpers.SystemEnvironment"),
                logger,
                RetryLinkedTypes.CreateImplementation(parameters[4].ParameterType, "Microsoft.Testing.Platform.Helpers.SystemTask"),
                1,
                authorizedSecurityIdentities,
                CancellationToken.None,
            ]);
    }

    private static Task AssertInvocationThrowsAsync<TException>(
        object owner,
        string methodName,
        params object?[] arguments)
        where TException : Exception
        => Assert.ThrowsExactlyAsync<TException>(
            async () => await InvokeTaskAsync(owner, methodName, arguments));

    private static Task InvokeTaskAsync(object owner, string methodName, params object?[] arguments)
        => (Task)RetryLinkedTypes.InvokeInstance(owner, methodName, arguments)!;

    private static async Task<object?> InvokeTaskWithResultAsync(
        object owner,
        string methodName,
        params object?[] arguments)
    {
        var task = (Task)RetryLinkedTypes.InvokeInstance(owner, methodName, arguments)!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static T GetPrivateFieldFromHierarchy<T>(object owner, string fieldName)
    {
        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                return (T)field.GetValue(owner)!;
            }
        }

        throw new MissingFieldException(owner.GetType().FullName, fieldName);
    }

    private static void SetPrivateFieldFromHierarchy(object owner, string fieldName, object? value)
    {
        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                field.SetValue(owner, value);
                return;
            }
        }

        throw new MissingFieldException(owner.GetType().FullName, fieldName);
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public bool FlushCalled { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCalled = true;
            return base.FlushAsync(cancellationToken);
        }
    }

    private sealed class StrictSplitReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool _splitHeader = true;

#if NETCOREAPP
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer = LimitRead(buffer);
            return base.ReadAsync(buffer, cancellationToken);
        }
#endif

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            count = LimitRead(count);
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NETCOREAPP
        private Memory<byte> LimitRead(Memory<byte> buffer)
        {
            int count = LimitRead(buffer.Length);
            return buffer[..count];
        }
#endif

        private int LimitRead(int count)
        {
            int remaining = checked((int)(Length - Position));
            if (count > remaining && remaining > 0)
            {
                throw new InvalidOperationException("The transport requested more bytes than remain in the frame.");
            }

            if (_splitHeader && count > 1)
            {
                _splitHeader = false;
                return 1;
            }

            return count;
        }
    }
}

[TestClass]
public sealed class RetryNamedPipeSecurityMutationTests
{
    private const string PackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";
    private const string OwnerSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    private static readonly Type SecurityType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");

    [TestMethod]
    public void SecurityIdentityAndDescriptorValidationUseEveryExactComponent()
    {
        Assert.AreEqual(
            @"LOCAL\pipe",
            RetryLinkedTypes.InvokeStatic(SecurityType, "GetPipeNameForSandboxedApplication", "pipe"));
        Assert.AreEqual(
            @"LOCAL\pipe",
            RetryLinkedTypes.InvokeStatic(SecurityType, "GetPipeNameForSandboxedApplication", @"LOCAL\pipe"));
        Assert.IsTrue((bool)RetryLinkedTypes.InvokeStatic(SecurityType, "IsAuthorizableSandboxedApplicationIdentity", PackageSid)!);
        Assert.IsFalse((bool)RetryLinkedTypes.InvokeStatic(
            SecurityType,
            "IsAuthorizableSandboxedApplicationIdentity",
            "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-notanumber")!);

        string descriptor = (string)RetryLinkedTypes.InvokeStatic(
            SecurityType,
            "BuildSecurityDescriptor",
            OwnerSid,
            new[] { PackageSid })!;
        Assert.AreEqual(
            $"O:{OwnerSid}G:{OwnerSid}D:P(A;;0x1f019f;;;{OwnerSid})(A;;0x12019b;;;{PackageSid})",
            descriptor);
    }

    [TestMethod]
    public void PipeCreationOptionsAndNativePathsUseExactFlagsCountsAndPrefixes()
    {
        AssertPipeCreationOptions(1, expectedOpenMode: 0x40080003, expectedMaxInstances: 1);
        AssertPipeCreationOptions(2, expectedOpenMode: 0x40000003, expectedMaxInstances: 2);
        AssertPipeCreationOptions(-1, expectedOpenMode: 0x40000003, expectedMaxInstances: 255);

        Assert.AreEqual(
            @"\\.\pipe\regular",
            RetryLinkedTypes.InvokeStatic(SecurityType, "GetNativePipePath", "regular", Array.Empty<string>()));

        int sessionProviderCalls = 0;
        Assert.AreEqual(
            @"Sessions\7\AppContainerNamedObjects\abc",
            RetryLinkedTypes.InvokeStatic(
                SecurityType,
                "EnsureSessionQualifiedNamedObjectPath",
                @"Sessions\7\AppContainerNamedObjects\abc",
                new Func<uint>(() =>
                {
                    sessionProviderCalls++;
                    return 99;
                })));
        Assert.AreEqual(
            @"Sessions\42\AppContainerNamedObjects\abc",
            RetryLinkedTypes.InvokeStatic(
                SecurityType,
                "EnsureSessionQualifiedNamedObjectPath",
                @"AppContainerNamedObjects\abc",
                new Func<uint>(() =>
                {
                    sessionProviderCalls++;
                    return 42;
                })));
        Assert.AreEqual(1, sessionProviderCalls);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native pipe security is Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void NativeSecurityCreatesAsyncDisconnectedStreamAndReturnsOwnerSid()
    {
        string ownerSid = (string)RetryLinkedTypes.InvokeStatic(SecurityType, "GetCurrentProcessOwnerSid")!;
        Assert.StartsWith("S-1-", ownerSid);

        using var stream = (NamedPipeServerStream)RetryLinkedTypes.InvokeStatic(
            SecurityType,
            "CreateServerStreamWithExplicitSecurityDescriptor",
            $"retry-{Guid.NewGuid():N}",
            1,
            $"D:(A;;GA;;;{ownerSid})")!;
        Assert.IsTrue(stream.IsAsync);
        Assert.IsFalse(stream.IsConnected);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native pipe security is Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void NativeSecurity_SecondSingleInstancePipeFailsWithWin32Error()
    {
        string ownerSid = (string)RetryLinkedTypes.InvokeStatic(SecurityType, "GetCurrentProcessOwnerSid")!;
        string pipeName = $"retry-{Guid.NewGuid():N}";
        using var first = (NamedPipeServerStream)RetryLinkedTypes.InvokeStatic(
            SecurityType,
            "CreateServerStreamWithExplicitSecurityDescriptor",
            pipeName,
            1,
            $"D:(A;;GA;;;{ownerSid})")!;

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => RetryLinkedTypes.InvokeStatic(
                SecurityType,
                "CreateServerStreamWithExplicitSecurityDescriptor",
                pipeName,
                1,
                $"D:(A;;GA;;;{ownerSid})"));

        Win32Exception innerException = Assert.IsInstanceOfType<Win32Exception>(exception.InnerException);
        Assert.Contains("Failed to create the named pipe", innerException.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Safe pipe handles are Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void CreateStreamWithOwnedHandle_DisposesHandleWhenConstructionFails()
    {
        using var handle = new SafePipeHandle(new IntPtr(-1), ownsHandle: true);
        Func<SafePipeHandle, NamedPipeServerStream> streamFactory =
            static _ => throw new InvalidOperationException("construction failed");

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => RetryLinkedTypes.InvokeStatic(SecurityType, "CreateStreamWithOwnedHandle", handle, streamFactory));

        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.IsTrue(handle.IsClosed);
    }

    private static void AssertPipeCreationOptions(int maxNumberOfServerInstances, uint expectedOpenMode, uint expectedMaxInstances)
    {
        object options = RetryLinkedTypes.InvokeStatic(
            SecurityType,
            "GetPipeCreationOptions",
            maxNumberOfServerInstances)!;
        Type optionsType = options.GetType();

        Assert.AreEqual(expectedOpenMode, optionsType.GetField("Item1")!.GetValue(options));
        Assert.AreEqual(0x00000008U, optionsType.GetField("Item2")!.GetValue(options));
        Assert.AreEqual(expectedMaxInstances, optionsType.GetField("Item3")!.GetValue(options));
    }
}

[TestClass]
public sealed class RetryBaseSerializerMutationTests
{
    [TestMethod]
    public void ReadStringValue_RejectsNegativeAndOversizedPayloadsAndReadsExactSize()
    {
        Type baseSerializerType = RetryLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Serializers.BaseSerializer");
        MethodInfo read = baseSerializerType.GetMethod(
            "ReadStringValue",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        InvalidDataException negative = AssertInnerException<InvalidDataException>(
            () => read.Invoke(null, [new MemoryStream(), -1]));
        Assert.AreEqual("Payload size -1 is invalid for the remaining stream.", negative.Message);

        InvalidDataException oversized = AssertInnerException<InvalidDataException>(
            () => read.Invoke(null, [new MemoryStream([1]), 2]));
        Assert.AreEqual("Payload size 2 is invalid for the remaining stream.", oversized.Message);

        using var positionedStream = new MemoryStream([1, 2, 3, 4]);
        positionedStream.Position = 3;
        AssertInnerException<InvalidDataException>(() => read.Invoke(null, [positionedStream, 2]));

        Assert.AreEqual(string.Empty, read.Invoke(null, [new MemoryStream(), 0]));
        Assert.AreEqual("x", read.Invoke(null, [new MemoryStream([(byte)'x']), 1]));
    }

    private static TException AssertInnerException<TException>(Action action)
        where TException : Exception
    {
        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(action);
        return Assert.IsInstanceOfType<TException>(exception.InnerException);
    }
}

internal static class RetryLinkedTypes
{
    private static readonly Assembly RetryAssembly = typeof(RetryOrchestrator).Assembly;

    public static Type Get(string fullName)
        => RetryAssembly.GetType(fullName, throwOnError: true)!;

    public static object Create(Type type, params object?[] arguments)
        => Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: CultureInfo.InvariantCulture)!;

    public static object CreateImplementation(Type serviceType, string implementationTypeName)
    {
        Type implementationType = serviceType.Assembly.GetType(implementationTypeName, throwOnError: true)!;
        return Create(implementationType);
    }

    public static object? InvokeStatic(Type type, string methodName, params object?[] arguments)
        => ResolveMethod(
            type,
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            arguments).Invoke(null, arguments);

    public static object? InvokeInstance(object owner, string methodName, params object?[] arguments)
        => ResolveMethod(
            owner.GetType(),
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            arguments).Invoke(owner, arguments);

    public static object GetStaticMember(Type type, string memberName)
    {
        FieldInfo? field = type.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field is not null
            ? field.GetValue(null)!
            : type.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
    }

    public static Delegate CreatePipeCallback(Type callbackType)
    {
        Type[] callbackArguments = callbackType.GetGenericArguments();
        Type requestType = callbackArguments[0];
        Type taskType = callbackArguments[1];
        Type responseType = taskType.GetGenericArguments()[0];
        Type voidResponseType = responseType.Assembly.GetType(
            "Microsoft.Testing.Platform.IPC.Models.VoidResponse",
            throwOnError: true)!;
        object response = GetStaticMember(voidResponseType, "CachedInstance");
        MethodInfo fromResult = typeof(Task)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition);
        object completedTask = fromResult.MakeGenericMethod(responseType).Invoke(null, [response])!;
        ParameterExpression request = Expression.Parameter(requestType, "request");
        return Expression.Lambda(callbackType, Expression.Constant(completedTask, taskType), request).Compile();
    }

    private static MethodInfo ResolveMethod(
        Type type,
        string methodName,
        BindingFlags bindingFlags,
        object?[] arguments)
    {
        MethodInfo[] candidates = [.. type
            .GetMethods(bindingFlags)
            .Where(method => method.Name == methodName)
            .Where(method => ParametersMatch(method.GetParameters(), arguments))
            .OrderByDescending(method => CompatibilityScore(method.GetParameters(), arguments))];

        return candidates.Length > 0
            ? candidates[0]
            : throw new MissingMethodException(
                type.FullName,
                $"{methodName}({string.Join(", ", arguments.Select(argument => argument?.GetType().FullName ?? "null"))})");
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] arguments)
    {
        if (parameters.Length != arguments.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            object? argument = arguments[i];
            if (argument is null)
            {
                if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null)
                {
                    return false;
                }
            }
            else if (!parameterType.IsInstanceOfType(argument))
            {
                return false;
            }
        }

        return true;
    }

    private static int CompatibilityScore(ParameterInfo[] parameters, object?[] arguments)
    {
        int score = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            object? argument = arguments[i];
            if (argument is not null)
            {
                score += parameters[i].ParameterType == argument.GetType() ? 2 : 1;
            }
        }

        return score;
    }
}
