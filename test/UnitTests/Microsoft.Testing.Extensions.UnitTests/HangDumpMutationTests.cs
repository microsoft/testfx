// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.ComponentModel;
using System.IO.Pipes;
using System.Linq.Expressions;

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Win32.SafeHandles;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpActivityIndicatorMutationTests
{
    [TestMethod]
    public async Task ConsumeAsync_PreCanceledToken_ThrowsBeforeInspectingTheMessage()
    {
        using HangDumpActivityIndicator indicator = CreateIndicator(Mock.Of<ILogger>(), DateTimeOffset.UtcNow);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => indicator.ConsumeAsync(null!, Mock.Of<IData>(), cancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task ConsumeAsync_TracksOnlyRunningAttemptsAndEmitsTraceDiagnostics()
    {
        var messages = new List<string>();
        Mock<ILogger> logger = CreateRecordingLogger(messages, traceEnabled: true);
        DateTimeOffset start = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        using HangDumpActivityIndicator indicator = CreateIndicator(logger.Object, start);
        SetPrivateField(indicator, "_exitSignalActivityIndicatorAsync", true);

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreEqual(1, GetInProgressCount(indicator));
        Assert.Contains("New in-progress test 'Test uid'", messages);

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", DiscoveredTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreEqual(1, GetInProgressCount(indicator), "A non-terminal update must not remove a running test.");
        Assert.IsNotEmpty(messages.Where(message => message.Contains("Signal for action node Test uid", StringComparison.Ordinal)));

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", new FailedTestNodeStateProperty(), new RetryAttemptProperty(1, isSuperseded: true)),
            CancellationToken.None);

        Assert.AreEqual(1, GetInProgressCount(indicator), "A superseded retry attempt must remain tracked while the retry runs.");

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreEqual(0, GetInProgressCount(indicator));
        Assert.IsNotEmpty(messages.Where(message => message.Contains("Test removed from in-progress list 'Test uid'", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConsumeAsync_SignalsOnlyForNonInProgressStates()
    {
        var messages = new List<string>();
        Mock<ILogger> logger = CreateRecordingLogger(messages, traceEnabled: true);
        using HangDumpActivityIndicator indicator = CreateIndicator(logger.Object, DateTimeOffset.UtcNow);
        SetPrivateField(indicator, "_exitSignalActivityIndicatorAsync", true);

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.IsEmpty(messages.Where(message => message.Contains("Signal for action node", StringComparison.Ordinal)));
        messages.Clear();

        await indicator.ConsumeAsync(
            null!,
            CreateUpdate("uid", DiscoveredTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.HasCount(1, messages.Where(message => message.Contains("Signal for action node Test uid", StringComparison.Ordinal)));
    }

    private static HangDumpActivityIndicator CreateIndicator(ILogger logger, DateTimeOffset utcNow)
    {
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger);
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        return new(
            new TestCommandLineOptions([]),
            Mock.Of<IEnvironment>(),
            Mock.Of<ITask>(),
            loggerFactory.Object,
            clock.Object);
    }

    private static Mock<ILogger> CreateRecordingLogger(List<string> messages, bool traceEnabled)
    {
        Mock<ILogger> logger = new();
        logger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(traceEnabled);
        logger
            .Setup(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => messages.Add(message))
            .Returns(Task.CompletedTask);
        return logger;
    }

    private static TestNodeUpdateMessage CreateUpdate(string uid, params IProperty[] properties)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = $"Test {uid}",
                Properties = new PropertyBag(properties),
            });

    private static int GetInProgressCount(HangDumpActivityIndicator indicator)
    {
        object state = GetPrivateField<object>(indicator, "_testsCurrentExecutionState");
        return (int)state.GetType().GetProperty("Count")!.GetValue(state)!;
    }

    private static void SetPrivateField<T>(object owner, string fieldName, T value)
        => owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);

    private static T GetPrivateField<T>(object owner, string fieldName)
        => (T)owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}

[TestClass]
public sealed class HangDumpRegistrationMutationTests
{
    [TestMethod]
    public void HangDumpCommandLineProvider_HasStableIdentityAndCompleteDumpTypes()
    {
        var provider = new HangDumpCommandLineProvider();
        string[] dumpTypes = (string[])typeof(HangDumpCommandLineProvider)
            .GetField("HangDumpTypeOptions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;

        Assert.AreEqual("HangDumpCommandLineProvider", provider.Uid);
#if NETCOREAPP
        string[] expectedDumpTypes = ["Mini", "Heap", "Full", "Triage", "None"];
#else
        string[] expectedDumpTypes = ["Mini", "Heap", "Full", "None"];
#endif
        Assert.AreSequenceEqual(expectedDumpTypes, dumpTypes);
    }

    [TestMethod]
    public async Task AddHangDumpProvider_RegistersEveryControllerAndTestHostRole()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        IList environmentVariableFactories = GetPrivateList(builder.TestHostControllers, "_environmentVariableProviderFactories");
        IList lifetimeHandlerFactories = GetPrivateList(builder.TestHostControllers, "_lifetimeHandlerFactories");
        IList commandLineFactories = GetPrivateList(builder.CommandLine, "_commandLineProviderFactory");
        IList dataConsumerFactories = GetPrivateList(builder.TestHost, "_dataConsumersCompositeServiceFactories");
        IList sessionHandlerFactories = GetPrivateList(builder.TestHost, "_testSessionLifetimeHandlerCompositeFactories");
        int environmentVariableCount = environmentVariableFactories.Count;
        int lifetimeHandlerCount = lifetimeHandlerFactories.Count;
        int commandLineCount = commandLineFactories.Count;
        int dataConsumerCount = dataConsumerFactories.Count;
        int sessionHandlerCount = sessionHandlerFactories.Count;

        builder.AddHangDumpProvider();

        Assert.HasCount(environmentVariableCount + 1, environmentVariableFactories.Cast<object>());
        Assert.HasCount(lifetimeHandlerCount + 1, lifetimeHandlerFactories.Cast<object>());
        Assert.HasCount(commandLineCount + 1, commandLineFactories.Cast<object>());
        Assert.HasCount(dataConsumerCount + 1, dataConsumerFactories.Cast<object>());
        Assert.HasCount(sessionHandlerCount + 1, sessionHandlerFactories.Cast<object>());
        Assert.AreSame(dataConsumerFactories[dataConsumerCount], sessionHandlerFactories[sessionHandlerCount]);

        var commandLineFactory = (Delegate)commandLineFactories[commandLineCount]!;
        Assert.IsInstanceOfType<HangDumpCommandLineProvider>(commandLineFactory.DynamicInvoke(new ServiceProvider()));
    }

    private static IList GetPrivateList(object owner, string fieldName)
        => (IList)owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}

[TestClass]
public sealed class HangDumpLifetimeMutationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BeforeStartAndConsumerHandshake_InitializeEveryFieldAndSerializer()
    {
        var messages = new List<string>();
        Mock<ILogger> logger = CreateRecordingLogger(messages);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        Mock<ITask> task = new();
        task.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var endpoint = new NamedPipeServerEndpoint($"hang_{Guid.NewGuid():N}");
        var handler = new HangDumpProcessLifetimeHandler(
            endpoint,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            new TestCommandLineOptions([]),
            task.Object,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IConfiguration>(),
            Mock.Of<IProcessHandler>(),
            clock.Object,
            new ServiceProvider());

        try
        {
            Assert.IsFalse(GetPrivateField<ManualResetEventSlim>(handler, "_waitConsumerPipeName").IsSet);
            Assert.AreEqual("Full", GetPrivateField<string>(handler, "_dumpType"));

            await handler.BeforeTestHostProcessStartAsync(TestContext.CancellationToken);

            Assert.AreEqual(TimeSpan.FromMinutes(30), GetPrivateField<TimeSpan?>(handler, "_activityTimerValue"));
            Assert.IsNull(GetPrivateField<DateTimeOffset?>(handler, "_deadlineDumpAt"));
            Assert.Contains("Hang dump timeout setup 00:30:00.", messages);

            object server = GetPrivateField<object>(handler, "_singleConnectionNamedPipeServer");
            Type voidResponseType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Models.VoidResponse");
            AssertSerializerRegistrations(
                server,
                voidResponseType,
                typeof(ConsumerPipeNameRequest),
                typeof(ActivitySignalRequest));

            const string ConsumerPipeName = "hang_consumer_pipe";
            object response = await InvokeCallbackAsync(handler, new ConsumerPipeNameRequest(ConsumerPipeName));

            Assert.AreSame(HangDumpLinkedTypes.GetStaticMember(voidResponseType, "CachedInstance"), response);
            Assert.Contains($"Consumer pipe name received '{ConsumerPipeName}'", messages);
            Assert.IsTrue(GetPrivateField<ManualResetEventSlim>(handler, "_waitConsumerPipeName").IsSet);

            object client = GetPrivateField<object>(handler, "_namedPipeClient");
            Assert.IsFalse(GetPrivateFieldFromHierarchy<bool>(client, "_exitProcessOnConnectionLoss"));
            AssertSerializerRegistrations(
                client,
                typeof(GetInProgressTestsResponse),
                typeof(GetInProgressTestsRequest),
                voidResponseType);
        }
        finally
        {
            handler.Dispose();
        }
    }

    [TestMethod]
    public void GetDumpFileNames_QuotesOnlyWindowsPathsThatContainSpaces()
    {
        const string Plain = "hangdump.dmp";
        const string WithSpaces = "results directory/hangdump.dmp";

        HangDumpProcessLifetimeHandler.DumpFileNames plain = HangDumpProcessLifetimeHandler.GetDumpFileNames(Plain);
        HangDumpProcessLifetimeHandler.DumpFileNames withSpaces = HangDumpProcessLifetimeHandler.GetDumpFileNames(WithSpaces);

        Assert.AreEqual(Plain, plain.WriteDumpFileName);
        Assert.AreEqual(Plain, plain.ArtifactDumpFileName);
        Assert.AreEqual(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"\"{WithSpaces}\"" : WithSpaces,
            withSpaces.WriteDumpFileName);
        Assert.AreEqual(WithSpaces, withSpaces.ArtifactDumpFileName);
    }

    [TestMethod]
    public void GetDiskInfo_ReportsEveryDriveAndEveryReadyDriveMetric()
    {
        string diskInfo = (string)typeof(HangDumpProcessLifetimeHandler)
            .GetMethod("GetDiskInfo", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        DriveInfo[] drives = DriveInfo.GetDrives();
        int readyDriveCount = drives.Count(drive => drive.IsReady);

        foreach (DriveInfo drive in drives)
        {
            Assert.Contains($"Drive {drive.Name}", diskInfo);
        }

        Assert.AreEqual(readyDriveCount, CountOccurrences(diskInfo, "  Available free space: "));
        Assert.AreEqual(readyDriveCount, CountOccurrences(diskInfo, "  Total free space: "));
        Assert.AreEqual(readyDriveCount, CountOccurrences(diskInfo, "  Total size: "));
    }

    [TestMethod]
    public void TryGetProcessById_WhenProcessExists_ReturnsTheSameProcess()
    {
        IProcess expected = Mock.Of<IProcess>();
        Mock<IProcessHandler> processHandler = new();
        processHandler.Setup(x => x.GetProcessById(42)).Returns(expected);

        IProcess? actual = HangDumpProcessLifetimeHandler.TryGetProcessById(processHandler.Object, 42);

        Assert.AreSame(expected, actual);
        processHandler.Verify(x => x.GetProcessById(42), Times.Once);
    }

    private static Mock<ILogger> CreateRecordingLogger(List<string> messages)
    {
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => messages.Add(message))
            .Returns(Task.CompletedTask);
        return logger;
    }

    private static async Task<object> InvokeCallbackAsync(HangDumpProcessLifetimeHandler handler, object request)
    {
        MethodInfo callback = typeof(HangDumpProcessLifetimeHandler)
            .GetMethod("CallbackAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var callbackTask = (Task)callback.Invoke(handler, [request])!;
        await callbackTask;
        return callbackTask.GetType().GetProperty(nameof(Task<>.Result))!.GetValue(callbackTask)!;
    }

    private static void AssertSerializerRegistrations(object pipe, params Type[] messageTypes)
    {
        IDictionary serializersByType = GetPrivateFieldFromHierarchy<IDictionary>(pipe, "_typeSerializer");
        IDictionary serializersById = GetPrivateFieldFromHierarchy<IDictionary>(pipe, "_idSerializer");

        Assert.HasCount(messageTypes.Length, serializersByType.Keys.Cast<object>());
        Assert.HasCount(messageTypes.Length, serializersById.Keys.Cast<object>());
        foreach (Type messageType in messageTypes)
        {
            Assert.Contains(messageType, serializersByType.Keys.Cast<object>(), messageType.FullName);
        }
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        int startIndex = 0;
        while ((startIndex = value.IndexOf(token, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += token.Length;
        }

        return count;
    }

    private static T GetPrivateField<T>(object owner, string fieldName)
        => (T)owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

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

        throw new InvalidOperationException($"Field '{fieldName}' was not found.");
    }
}

[TestClass]
public sealed class HangDumpSharedHelperMutationTests
{
    private const string DeadlineVariableName = "TESTINGPLATFORM_DEADLINE";

    [TestMethod]
    public void DeadlineHelper_RecognizesEverySupportedFormatAndRejectsMalformedExplicitOffsets()
    {
        AssertDeadline("2030-01-02T03:04:05Z", new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        AssertDeadline("2030-01-02T03:04:05.1234567+02:00", new DateTimeOffset(2030, 1, 2, 1, 4, 5, TimeSpan.Zero).AddTicks(1_234_567));

        IEnvironment malformed = CreateEnvironment(DeadlineVariableName, "2030-13-02T03:04:05+02:00");
        (bool malformedSuccess, DateTimeOffset malformedResult) = TryGetDeadline(malformed);
        Assert.IsFalse(malformedSuccess);
        Assert.AreEqual(default, malformedResult);

        IEnvironment unset = CreateEnvironment(DeadlineVariableName, null);
        (bool unsetSuccess, DateTimeOffset unsetResult) = TryGetDeadline(unset);
        Assert.IsFalse(unsetSuccess);
        Assert.AreEqual(default, unsetResult);

        IEnvironment noOffset = CreateEnvironment(DeadlineVariableName, "2030-01-02T03:04:05");
        (bool noOffsetSuccess, DateTimeOffset noOffsetResult) = TryGetDeadline(noOffset);
        Assert.IsFalse(noOffsetSuccess);
        Assert.AreEqual(default, noOffsetResult);

        AssertDeadline("2030-01-02T03:04:05-02:00", new DateTimeOffset(2030, 1, 2, 5, 4, 5, TimeSpan.Zero));
    }

    [TestMethod]
    [DataRow("1ms", 0.001)]
    [DataRow("1mil", 0.001)]
    [DataRow("1milliseconds", 0.001)]
    [DataRow("1s", 1)]
    [DataRow("1m", 60)]
    [DataRow("1h", 3600)]
    [DataRow("1d", 86400)]
    public void TimeSpanParser_ExplicitSuffixSelectsTheExactUnit(string input, double expectedSeconds)
    {
        Type parserType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeSpanParser");
        MethodInfo tryParse = parserType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "TryParse" && method.GetParameters().Length == 2);
        object?[] arguments = [input, TimeSpan.Zero];

        bool success = (bool)tryParse.Invoke(null, arguments)!;

        Assert.IsTrue(success);
        var result = (TimeSpan)arguments[1]!;
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [TestMethod]
    public void TimeoutHelper_DefaultsAreInitializedAndConsistent()
    {
        Type timeoutType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.TimeoutHelper");
        var timeout = (TimeSpan)HangDumpLinkedTypes.GetStaticMember(timeoutType, "DefaultHangTimeSpanTimeout");
        double timeoutSeconds = (double)HangDumpLinkedTypes.GetStaticMember(timeoutType, "DefaultHangTimeoutSeconds");

        Assert.IsGreaterThan(TimeSpan.Zero, timeout);
        Assert.AreEqual(timeout.TotalSeconds, timeoutSeconds);
    }

    private static void AssertDeadline(string raw, DateTimeOffset expected)
    {
        IEnvironment environment = CreateEnvironment(DeadlineVariableName, raw);
        (bool success, DateTimeOffset actual) = TryGetDeadline(environment);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, actual);
    }

    private static (bool Success, DateTimeOffset Deadline) TryGetDeadline(IEnvironment environment)
    {
        Type helperType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.Helpers.DeadlineHelper");
        MethodInfo method = helperType.GetMethod(
            "TryGetDeadline",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        object?[] arguments = [environment, default(DateTimeOffset)];

        bool success = (bool)method.Invoke(null, arguments)!;

        return (success, (DateTimeOffset)arguments[1]!);
    }

    private static IEnvironment CreateEnvironment(string variableName, string? value)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(variableName)).Returns(value);
        return environment.Object;
    }
}

[TestClass]
public sealed class HangDumpNamedPipeMutationTests
{
    private const string PackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NamedPipeClient_GetPipeOptions_DistinguishesAppContainerAndRegularNames()
    {
        Type clientType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type securityType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");
        string sandboxedPrefix = (string)HangDumpLinkedTypes.GetStaticMember(securityType, "SandboxedApplicationPipeNamePrefix");

        Assert.AreEqual(
            PipeOptions.Asynchronous,
            HangDumpLinkedTypes.InvokeStatic(clientType, "GetPipeOptions", sandboxedPrefix + "pipe"));

        PipeOptions expectedRegular = PipeOptions.Asynchronous;
#if NET
        expectedRegular |= PipeOptions.CurrentUserOnly;
#endif
        Assert.AreEqual(expectedRegular, HangDumpLinkedTypes.InvokeStatic(clientType, "GetPipeOptions", "pipe"));
    }

    [TestMethod]
    public void NamedPipeClient_RegisterAndDispose_StoresBothIndexesAndDisposesEveryResource()
    {
        Type clientType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type serializerType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Serializers.VoidResponseSerializer");
        Type responseType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Models.VoidResponse");
        object client = HangDumpLinkedTypes.Create(clientType, "pipe");
        object serializer = HangDumpLinkedTypes.Create(serializerType);
        MethodInfo registerSerializer = clientType.GetMethod(
            "RegisterSerializer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        registerSerializer.Invoke(client, [serializer, responseType]);
        IDictionary serializersByType = GetPrivateFieldFromHierarchy<IDictionary>(client, "_typeSerializer");
        IDictionary serializersById = GetPrivateFieldFromHierarchy<IDictionary>(client, "_idSerializer");
        SemaphoreSlim gate = GetPrivateFieldFromHierarchy<SemaphoreSlim>(client, "_lock");
        MemoryStream serializationBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_serializationBuffer");
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_messageBuffer");
        int serializerId = (int)serializerType.GetProperty("Id")!.GetValue(serializer)!;

        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(client, "_exitProcessOnConnectionLoss"));
        Assert.AreSame(serializer, serializersByType[responseType]);
        Assert.AreSame(serializer, serializersById[serializerId]);

        ((IDisposable)client).Dispose();

        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(client, "_disposed"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => gate.Wait(0, TestContext.CancellationToken));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = serializationBuffer.Length);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = messageBuffer.Length);
        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public async Task NamedPipeConnection_ReadsSplitHeaderAndClearsMessageBuffer()
    {
        Type clientType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeClient");
        Type serializerType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Serializers.VoidResponseSerializer");
        Type responseType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.Models.VoidResponse");
        object client = HangDumpLinkedTypes.Create(clientType, "pipe");
        object serializer = HangDumpLinkedTypes.Create(serializerType);
        clientType
            .GetMethod("RegisterSerializer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(client, [serializer, responseType]);
        int serializerId = (int)serializerType.GetProperty("Id")!.GetValue(serializer)!;
        byte[] frame = [.. BitConverter.GetBytes(sizeof(int)), .. BitConverter.GetBytes(serializerId)];
        using var stream = new SplitFirstReadStream(frame);

        object? response = await InvokeTaskWithResultAsync(
            client,
            "ReadNextMessageAsync",
            stream,
            TestContext.CancellationToken,
            int.MaxValue);

        Assert.AreSame(HangDumpLinkedTypes.GetStaticMember(responseType, "CachedInstance"), response);
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(client, "_messageBuffer");
        Assert.AreEqual(0L, messageBuffer.Length);
        ((IDisposable)client).Dispose();
    }

    [TestMethod]
    public void NamedPipeServer_GetPipeName_UsesThePlatformSpecificShape()
    {
        object pipeName = GetPipeName(@"folder\name");
        string name = GetPipeNameValue(pipeName);

        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.IsTrue(Path.IsPathRooted(name));
            Assert.EndsWith("testingplatform.pipe.folder.name", name.Replace('\\', '.'));
        }
        else
        {
            Assert.AreEqual("testingplatform.pipe.folder.name", name);
        }
    }

    [TestMethod]
    public void NamedPipeServer_AuthorizationChangesPipeNameOnlyForNonEmptySupportedIdentityLists()
    {
        Type securityType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");
        bool isSupported = (bool)HangDumpLinkedTypes.GetStaticMember(securityType, "IsSupported");
        object pipeName = GetPipeName(Guid.NewGuid().ToString("N"));
        string originalName = GetPipeNameValue(pipeName);

        object noIdentities = CreateServer(pipeName, Mock.Of<ILogger>(), authorizedSecurityIdentities: null);
        string noIdentitiesName = GetPipeNameValue(
            noIdentities.GetType().GetProperty("PipeName")!.GetValue(noIdentities)!);
        ((IDisposable)noIdentities).Dispose();

        object emptyIdentities = CreateServer(pipeName, Mock.Of<ILogger>(), Array.Empty<string>());
        string emptyIdentitiesName = GetPipeNameValue(
            emptyIdentities.GetType().GetProperty("PipeName")!.GetValue(emptyIdentities)!);
        ((IDisposable)emptyIdentities).Dispose();

        object packageIdentity = CreateServer(pipeName, Mock.Of<ILogger>(), [PackageSid]);
        string packageIdentityName = GetPipeNameValue(
            packageIdentity.GetType().GetProperty("PipeName")!.GetValue(packageIdentity)!);
        ((IDisposable)packageIdentity).Dispose();

        Assert.AreEqual(originalName, noIdentitiesName);
        Assert.AreEqual(originalName, emptyIdentitiesName);
        string expectedPackageIdentityName = isSupported
            ? (string)HangDumpLinkedTypes.InvokeStatic(
                securityType,
                "GetPipeNameForSandboxedApplication",
                originalName)!
            : originalName;
        Assert.AreEqual(expectedPackageIdentityName, packageIdentityName);
    }

    [TestMethod]
    public async Task NamedPipeServer_WaitConnection_LogsTheExactPipeNameBeforeCancellation()
    {
        object pipeName = GetPipeName(Guid.NewGuid().ToString("N"));
        string pipeNameValue = GetPipeNameValue(pipeName);
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        object server = CreateServer(pipeName, logger.Object);

        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => InvokeTaskAsync(server, "WaitConnectionAsync", cancellationTokenSource.Token));
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
    public void NamedPipeServer_DisposeWithoutConnection_DisposesBuffersAndRecordsState()
    {
        object server = CreateServer(
            GetPipeName(Guid.NewGuid().ToString("N")),
            Mock.Of<ILogger>());
        MemoryStream serializationBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(server, "_serializationBuffer");
        MemoryStream messageBuffer = GetPrivateFieldFromHierarchy<MemoryStream>(server, "_messageBuffer");

        ((IDisposable)server).Dispose();

        Assert.IsTrue(GetPrivateFieldFromHierarchy<bool>(server, "_disposed"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = serializationBuffer.Length);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = messageBuffer.Length);
        ((IDisposable)server).Dispose();
    }

    [TestMethod]
    public void NamedPipeServer_DisposeAfterConnection_WaitsForTheReadLoopToExit()
    {
        object server = CreateServer(GetPipeName(Guid.NewGuid().ToString("N")), Mock.Of<ILogger>());
        TaskCompletionSource<bool> loopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateFieldFromHierarchy(server, "_loopTask", loopCompletion.Task);
        server.GetType().GetProperty("WasConnected")!.SetValue(server, true);
        using ManualResetEventSlim disposeInvoked = new(false);
        Exception? disposeException = null;
        var disposalThread = new Thread(
            () =>
            {
                disposeInvoked.Set();
                try
                {
                    ((IDisposable)server).Dispose();
                }
                catch (Exception ex)
                {
                    disposeException = ex;
                }
            });
        disposalThread.Start();

        try
        {
            disposeInvoked.Wait(TestContext.CancellationToken);
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => GetPrivateFieldFromHierarchy<bool>(server, "_disposed"),
                    TimeSpan.FromSeconds(30)),
                "The server did not enter disposal.");
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => !disposalThread.IsAlive
                        || (disposalThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(30)),
                "Disposal did not reach a blocking wait.");
            Assert.IsTrue(disposalThread.IsAlive, "Dispose returned before the injected loop task completed.");
        }
        finally
        {
            loopCompletion.TrySetResult(true);
        }

        Assert.IsTrue(disposalThread.Join(TimeSpan.FromSeconds(30)), "Dispose did not finish after the loop task completed.");
        Assert.IsNull(disposeException);
    }

    [TestMethod]
    public async Task NamedPipeServer_ConnectedClient_RunsLoopAndLogsLifecycle()
    {
        object pipeName = GetPipeName(Guid.NewGuid().ToString("N"));
        string pipeNameValue = GetPipeNameValue(pipeName);
        ConcurrentQueue<string> messages = [];
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => messages.Enqueue(message))
            .Returns(Task.CompletedTask);
        object server = CreateServer(pipeName, logger.Object);
        Type clientType = server.GetType().Assembly.GetType(
            "Microsoft.Testing.Platform.IPC.NamedPipeClient",
            throwOnError: true)!;
        object client = HangDumpLinkedTypes.Create(clientType, pipeNameValue);

        try
        {
            Task waitConnection = InvokeTaskAsync(server, "WaitConnectionAsync", TestContext.CancellationToken);
            await InvokeTaskAsync(client, "ConnectAsync", TestContext.CancellationToken);
            await waitConnection;

            Assert.IsTrue((bool)server.GetType().GetProperty("WasConnected")!.GetValue(server)!);
            Assert.Contains($"Client connected to {pipeNameValue}", messages);

            Task loopTask = GetPrivateFieldFromHierarchy<Task>(server, "_loopTask");
            ((IDisposable)client).Dispose();
            Task completed = await Task.WhenAny(
                loopTask,
                Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.AreSame(loopTask, completed);
            await loopTask;
            Assert.Contains($"Client disconnected from pipe '{pipeNameValue}', exiting read loop", messages);
        }
        finally
        {
            ((IDisposable)client).Dispose();
            ((IDisposable)server).Dispose();
        }
    }

    [TestMethod]
    public async Task NamedPipeServer_DisposedAfterAccept_DoesNotStartTheReadLoop()
    {
        object pipeName = GetPipeName(Guid.NewGuid().ToString("N"));
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        object server = CreateServer(pipeName, logger.Object);
        Type clientType = server.GetType().Assembly.GetType(
            "Microsoft.Testing.Platform.IPC.NamedPipeClient",
            throwOnError: true)!;
        object client = HangDumpLinkedTypes.Create(clientType, GetPipeNameValue(pipeName));
        SetPrivateFieldFromHierarchy(server, "_disposed", true);

        try
        {
            Task waitConnection = InvokeTaskAsync(server, "WaitConnectionAsync", TestContext.CancellationToken);
            await InvokeTaskAsync(client, "ConnectAsync", TestContext.CancellationToken);
            await waitConnection;

            Assert.IsNull(GetPrivateFieldFromHierarchy<Task?>(server, "_loopTask"));
            Assert.IsFalse((bool)server.GetType().GetProperty("WasConnected")!.GetValue(server)!);
        }
        finally
        {
            ((IDisposable)client).Dispose();
            SetPrivateFieldFromHierarchy(server, "_disposed", false);
            ((IDisposable)server).Dispose();
        }
    }

    [TestMethod]
    public void NamedPipeServerFactory_CreateEndpoint_UsesCompactGuidFormat()
    {
        Type factoryType = HangDumpLinkedTypes.Get("Microsoft.Testing.Extensions.NamedPipeServerFactory");
        object endpoint = HangDumpLinkedTypes.InvokeStatic(factoryType, "CreateEndpoint")!;
        string pipeName = (string)endpoint.GetType().GetProperty("PipeName")!.GetValue(endpoint)!;

        Assert.MatchesRegex(new Regex(@"(?:^|[\\/])testingplatform\.pipe\.[0-9a-f]{32}$", RegexOptions.CultureInvariant), pipeName);
    }

    private static object GetPipeName(string name)
        => HangDumpLinkedTypes.InvokeStatic(
            HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer"),
            "GetPipeName",
            name)!;

    private static string GetPipeNameValue(object pipeName)
        => (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;

    private static object CreateServer(
        object pipeName,
        ILogger logger,
        IReadOnlyList<string>? authorizedSecurityIdentities = null)
    {
        Type serverType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServer");
        ConstructorInfo constructor = serverType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor =>
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                return parameters.Length == 8
                    && parameters[0].ParameterType.FullName == "Microsoft.Testing.Platform.IPC.PipeNameDescription";
            });
        ParameterInfo[] constructorParameters = constructor.GetParameters();
        object environment = HangDumpLinkedTypes.CreateImplementation(
            constructorParameters[2].ParameterType,
            "Microsoft.Testing.Platform.Helpers.SystemEnvironment");
        object task = HangDumpLinkedTypes.CreateImplementation(
            constructorParameters[4].ParameterType,
            "Microsoft.Testing.Platform.Helpers.SystemTask");
        return constructor.Invoke(
            [
                pipeName,
                HangDumpLinkedTypes.CreatePipeCallback(constructorParameters[1].ParameterType),
                environment,
                logger,
                task,
                1,
                authorizedSecurityIdentities,
                CancellationToken.None,
            ]);
    }

    private static Task InvokeTaskAsync(object owner, string methodName, params object?[] arguments)
        => (Task)HangDumpLinkedTypes.InvokeInstance(owner, methodName, arguments)!;

    private static async Task<object?> InvokeTaskWithResultAsync(
        object owner,
        string methodName,
        params object?[] arguments)
    {
        var task = (Task)HangDumpLinkedTypes.InvokeInstance(owner, methodName, arguments)!;
        await task;
        return task.GetType().GetProperty(nameof(Task<>.Result))!.GetValue(task);
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

        throw new InvalidOperationException($"Field '{fieldName}' was not found.");
    }

    private static void SetPrivateFieldFromHierarchy<T>(object owner, string fieldName, T value)
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

        throw new InvalidOperationException($"Field '{fieldName}' was not found.");
    }

    private sealed class SplitFirstReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        private bool _isFirstRead = true;

#if NET
        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            int count = GetReadCount(destination.Length);
            return base.ReadAsync(destination[..count], cancellationToken);
        }
#endif

        public override Task<int> ReadAsync(
            byte[] destination,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => base.ReadAsync(destination, offset, GetReadCount(count), cancellationToken);

        private int GetReadCount(int requestedCount)
        {
            if (!_isFirstRead)
            {
                return requestedCount;
            }

            _isFirstRead = false;
            return Math.Min(1, requestedCount);
        }
    }
}

[TestClass]
public sealed class HangDumpNamedPipeSecurityMutationTests
{
    private const string PackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";
    private static readonly Type SecurityType = HangDumpLinkedTypes.Get("Microsoft.Testing.Platform.IPC.NamedPipeServerSecurity");

    [TestMethod]
    public void GetPipeNameForSandboxedApplication_AddsThePrefixExactlyOnce()
    {
        Assert.AreEqual(
            @"LOCAL\testingplatform.pipe.test",
            HangDumpLinkedTypes.InvokeStatic(SecurityType, "GetPipeNameForSandboxedApplication", "testingplatform.pipe.test"));
        Assert.AreEqual(
            @"LOCAL\testingplatform.pipe.test",
            HangDumpLinkedTypes.InvokeStatic(SecurityType, "GetPipeNameForSandboxedApplication", @"LOCAL\testingplatform.pipe.test"));
    }

    [TestMethod]
    public void IsAuthorizableSandboxedApplicationIdentity_ValidatesEverySubAuthority()
    {
        Assert.IsTrue((bool)HangDumpLinkedTypes.InvokeStatic(SecurityType, "IsAuthorizableSandboxedApplicationIdentity", PackageSid)!);
        Assert.IsTrue((bool)HangDumpLinkedTypes.InvokeStatic(SecurityType, "IsAuthorizableSandboxedApplicationIdentity", $"{PackageSid}-1-2-3-4")!);
        Assert.IsFalse(
            (bool)HangDumpLinkedTypes.InvokeStatic(
                SecurityType,
                "IsAuthorizableSandboxedApplicationIdentity",
                "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-notanumber")!);
    }

    [TestMethod]
    public void BuildSecurityDescriptor_IncludesTheOwnerAndEveryAuthorizedPackage()
    {
        const string OwnerSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

        string descriptor = (string)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "BuildSecurityDescriptor",
            OwnerSid,
            new[] { PackageSid })!;

        Assert.AreEqual(
            $"O:{OwnerSid}G:{OwnerSid}D:P(A;;0x1f019f;;;{OwnerSid})(A;;0x12019b;;;{PackageSid})",
            descriptor);
    }

    [TestMethod]
    public void GetPipeCreationOptions_ReturnsExactNativeFlagsAndInstanceCounts()
    {
        AssertPipeCreationOptions(1, expectedOpenMode: 0x40080003, expectedMaxInstances: 1);
        AssertPipeCreationOptions(2, expectedOpenMode: 0x40000003, expectedMaxInstances: 2);
        AssertPipeCreationOptions(-1, expectedOpenMode: 0x40000003, expectedMaxInstances: 255);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Safe pipe handles are Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void CreateStreamWithOwnedHandle_WhenConstructionFails_DisposesTheHandle()
    {
        using var handle = new SafePipeHandle(new IntPtr(-1), ownsHandle: true);
        Func<SafePipeHandle, NamedPipeServerStream> streamFactory =
            static _ => throw new InvalidOperationException("construction failed");

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => HangDumpLinkedTypes.InvokeStatic(
                SecurityType,
                "CreateStreamWithOwnedHandle",
                handle,
                streamFactory));

        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.IsTrue(handle.IsClosed);
    }

    [TestMethod]
    public void EnsureSessionQualifiedNamedObjectPath_PreservesExistingPrefixAndAddsMissingPrefix()
    {
        int sessionProviderCalls = 0;

        string existing = (string)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "EnsureSessionQualifiedNamedObjectPath",
            @"Sessions\7\AppContainerNamedObjects\abc",
            new Func<uint>(() =>
            {
                sessionProviderCalls++;
                return 99;
            }))!;
        string missing = (string)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "EnsureSessionQualifiedNamedObjectPath",
            @"AppContainerNamedObjects\abc",
            new Func<uint>(() =>
            {
                sessionProviderCalls++;
                return 42;
            }))!;

        Assert.AreEqual(@"Sessions\7\AppContainerNamedObjects\abc", existing);
        Assert.AreEqual(@"Sessions\42\AppContainerNamedObjects\abc", missing);
        Assert.AreEqual(1, sessionProviderCalls);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native named-pipe security is Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void GetCurrentProcessOwnerSid_ReturnsAUsableSid()
    {
        string ownerSid = (string)HangDumpLinkedTypes.InvokeStatic(SecurityType, "GetCurrentProcessOwnerSid")!;

        Assert.StartsWith("S-1-", ownerSid);
        Assert.AreEqual(ownerSid.Trim().ToUpperInvariant(), ownerSid);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native named-pipe paths are Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void GetNativePipePath_RegularPipeUsesTheGlobalPipeNamespace()
        => Assert.AreEqual(
            @"\\.\pipe\testingplatform.pipe.test",
            HangDumpLinkedTypes.InvokeStatic(
                SecurityType,
                "GetNativePipePath",
                "testingplatform.pipe.test",
                new[] { PackageSid }));

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native named-pipe creation is Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_IsAsynchronousAndInitiallyDisconnected()
    {
        string ownerSid = (string)HangDumpLinkedTypes.InvokeStatic(SecurityType, "GetCurrentProcessOwnerSid")!;
        string descriptor = (string)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "BuildSecurityDescriptor",
            ownerSid,
            Array.Empty<string>())!;
        using var stream = (NamedPipeServerStream)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "CreateServerStreamWithExplicitSecurityDescriptor",
            $"testingplatform.pipe.test.{Guid.NewGuid():N}",
            1,
            descriptor)!;

        Assert.IsTrue(stream.IsAsync);
        Assert.IsFalse(stream.IsConnected);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Native named-pipe creation is Windows-only.")]
    [SupportedOSPlatform("windows")]
    public void CreateServerStream_WhenFirstInstanceAlreadyExists_ThrowsWin32Exception()
    {
        string ownerSid = (string)HangDumpLinkedTypes.InvokeStatic(SecurityType, "GetCurrentProcessOwnerSid")!;
        string descriptor = (string)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "BuildSecurityDescriptor",
            ownerSid,
            Array.Empty<string>())!;
        string pipeName = $"testingplatform.pipe.test.{Guid.NewGuid():N}";
        using var firstStream = (NamedPipeServerStream)HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "CreateServerStreamWithExplicitSecurityDescriptor",
            pipeName,
            1,
            descriptor)!;

        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
            () => HangDumpLinkedTypes.InvokeStatic(
                SecurityType,
                "CreateServerStreamWithExplicitSecurityDescriptor",
                pipeName,
                1,
                descriptor));

        Win32Exception innerException = Assert.IsInstanceOfType<Win32Exception>(exception.InnerException);
        Assert.Contains("Failed to create the named pipe", innerException.Message);
    }

    private static void AssertPipeCreationOptions(int maxNumberOfServerInstances, uint expectedOpenMode, uint expectedMaxInstances)
    {
        object options = HangDumpLinkedTypes.InvokeStatic(
            SecurityType,
            "GetPipeCreationOptions",
            maxNumberOfServerInstances)!;
        Type optionsType = options.GetType();
        uint openMode = (uint)optionsType.GetField("Item1")!.GetValue(options)!;
        uint pipeMode = (uint)optionsType.GetField("Item2")!.GetValue(options)!;
        uint maxInstances = (uint)optionsType.GetField("Item3")!.GetValue(options)!;

        Assert.AreEqual(expectedOpenMode, openMode);
        Assert.AreEqual(0x00000008U, pipeMode);
        Assert.AreEqual(expectedMaxInstances, maxInstances);
    }
}

internal static class HangDumpLinkedTypes
{
    private static readonly Assembly HangDumpAssembly = typeof(HangDumpProcessLifetimeHandler).Assembly;

    public static Type Get(string fullName)
        => HangDumpAssembly.GetType(fullName, throwOnError: true)!;

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
