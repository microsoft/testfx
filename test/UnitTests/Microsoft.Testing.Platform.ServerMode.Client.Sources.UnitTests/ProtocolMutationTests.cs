// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

#if NETCOREAPP
using System.Buffers;
using System.Runtime.InteropServices;
#endif
using System.Net.Sockets;
using System.Reflection;

using ClientAssertionFailureProperty = serverclient::Microsoft.Testing.Platform.Extensions.Messages.AssertionFailureProperty;
using ClientLogger = serverclient::Microsoft.Testing.Platform.Logging.ILogger;
using ClientLogLevel = serverclient::Microsoft.Testing.Platform.Logging.LogLevel;
using ClientTestNode = serverclient::Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

#if NETCOREAPP
[TestClass]
#endif
public sealed class ProtocolMutationTests
{
#if NETCOREAPP
    [TestMethod]
    public void PlatformResourceLabels_AreStable()
    {
        Assert.AreEqual(
            "Server log message",
            GetStaticProperty<string>(
                "Microsoft.Testing.Platform.Resources.PlatformResources",
                "ServerLogMessageDisplayName"));
        Assert.AreEqual(
            "This data represents a server log message",
            GetStaticProperty<string>(
                "Microsoft.Testing.Platform.Resources.PlatformResources",
                "ServerLogMessageDescription"));
    }

    [TestMethod]
    public void ServerCapabilities_TestCoverageMessagesRemainUnsupported()
        => Assert.IsFalse(ServerTestingCapabilities.SupportsTestCoverageMessages);

    [TestMethod]
    public void ErrorCodes_MatchTheJsonRpcAndTestingProtocol()
    {
        Assert.AreEqual(-32700, ErrorCodes.ParseError);
        Assert.AreEqual(-32600, ErrorCodes.InvalidRequest);
        Assert.AreEqual(-32601, ErrorCodes.MethodNotFound);
        Assert.AreEqual(-32602, ErrorCodes.InvalidParams);
        Assert.AreEqual(-32603, ErrorCodes.InternalError);
        Assert.AreEqual(-32002, ErrorCodes.ServerNotInitialized);
        Assert.AreEqual(-32899, ErrorCodes.LspErrorRangeStart);
        Assert.AreEqual(-32800, ErrorCodes.RequestCanceled);
        Assert.AreEqual(-32800, ErrorCodes.LspErrorRangeEnd);
        Assert.AreEqual(-31700, ErrorCodes.TestingPlatformErrorRangeStart);
        Assert.AreEqual(-31699, ErrorCodes.ProtocolVersionNotSupported);
        Assert.AreEqual(-31000, ErrorCodes.TestingPlatformErrorRangeEnd);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("-0", 0)]
    [DataRow("10", 10)]
    [DataRow("-42", -42)]
    [DataRow("2147483647", int.MaxValue)]
    [DataRow("-2147483648", int.MinValue)]
    [DataRow(".0", 0)]
    [DataRow("1.0", 1)]
    [DataRow("1e2", 100)]
    [DataRow("1E2", 100)]
    [DataRow("e1", 0)]
    [DataRow("-1e1", -10)]
    [DataRow("1.5e2", 150)]
    [DataRow("214748364e1", 2_147_483_640)]
    [DataRow("-214748364e1", -2_147_483_640)]
    [DataRow("2e9", 2_000_000_000)]
    [DataRow("100e-2", 1)]
    [DataRow("1200e-2", 12)]
    public void RpcIdParser_IntegralJsonNumber_ReturnsExactInt32(string value, int expected)
    {
        (bool parsed, int actual) = TryParseNumericId(value);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    [DataRow("1.1")]
    [DataRow(".1")]
    [DataRow("1.5e0")]
    [DataRow("10e-2")]
    [DataRow("1e-2")]
    [DataRow("1e+")]
    [DataRow("1e11")]
    public void RpcIdParser_NonIntegralOrOutOfRangeJsonNumber_ReturnsFalse(string value)
    {
        (bool parsed, int actual) = TryParseNumericId(value);

        Assert.IsFalse(parsed);
        Assert.AreEqual(default, actual);
    }

    [TestMethod]
    public void ExceptionFlattener_NullMessageAndException_ReturnsNoEntries()
        => Assert.IsEmpty(FlattenException(null, null));

    [TestMethod]
    public void ExceptionFlattener_ExplicitMessage_TakesPrecedenceOverExceptionMessage()
    {
        object[] flattened = FlattenException("reported failure", new InvalidOperationException("exception failure"));

        object entry = Assert.ContainsSingle(flattened);
        Assert.AreEqual("reported failure", GetInstanceProperty<string?>(entry, "ErrorMessage"));
    }

    [TestMethod]
    public void ExceptionFlattener_WhitespaceMessage_UsesExceptionMessage()
    {
        object[] flattened = FlattenException(" ", new InvalidOperationException("exception failure"));

        object entry = Assert.ContainsSingle(flattened);
        Assert.AreEqual("exception failure", GetInstanceProperty<string?>(entry, "ErrorMessage"));
    }

    [TestMethod]
    public void ExceptionFlattener_NestedException_IncludesInnerException()
    {
        var exception = new InvalidOperationException("outer", new ArgumentException("inner"));

        object[] flattened = FlattenException(null, exception);

        Assert.HasCount(2, flattened);
        Assert.AreEqual("outer", GetInstanceProperty<string?>(flattened[0], "ErrorMessage"));
        Assert.AreEqual("inner", GetInstanceProperty<string?>(flattened[1], "ErrorMessage"));
    }

    [TestMethod]
    public void AssertionFailureProperty_OneSidedValues_AreAccepted()
    {
        var expectedOnly = new ClientAssertionFailureProperty("expected", null);
        var actualOnly = new ClientAssertionFailureProperty(null, "actual");

        Assert.AreEqual("expected", expectedOnly.Expected);
        Assert.IsNull(expectedOnly.Actual);
        Assert.IsNull(actualOnly.Expected);
        Assert.AreEqual("actual", actualOnly.Actual);
    }

    [TestMethod]
    public void LoggingFormatter_WithoutException_ReturnsStateOnly()
        => Assert.AreEqual("message", FormatLog("message", null));

    [TestMethod]
    public void LoggingFormatter_WithException_AppendsFullExceptionDetails()
    {
        var exception = new InvalidOperationException("failure");

        string formatted = FormatLog("message", exception);

        Assert.AreEqual(
            $"message{Environment.NewLine}------Exception detail------{Environment.NewLine}{exception}",
            formatted);
    }

    [TestMethod]
    public void FormatterUtilities_ClientResponseType_UsesRegisteredDeserializer()
    {
        object formatter = CreateSourceFormatter();
        object json = formatter.GetType()
            .GetField("_json", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(formatter)!;
        var deserializers = (System.Collections.IDictionary)json.GetType()
            .GetField("_deserializers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(json)!;

        Assert.IsTrue(deserializers.Contains(GetClientType("Microsoft.Testing.Platform.ServerMode.RunResponseArgs")));
    }

    [TestMethod]
    public async Task TcpRead_ZeroContentLength_IsPassedToFormatter()
    {
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream(Encoding.ASCII.GetBytes("Content-Length: 0\r\n\r\n")),
            new MemoryStream(),
            formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(message);
        Assert.AreEqual(0, formatter.DeserializedByteCount);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("-1")]
    public async Task TcpRead_InvalidContentLength_DoesNotInvokeFormatter(string contentLength)
    {
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream(Encoding.ASCII.GetBytes($"Content-Length: {contentLength}\r\n\r\nx")),
            new MemoryStream(),
            formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        Assert.AreEqual(0, formatter.DeserializeCalls);
    }

    [TestMethod]
    public async Task TcpRead_EmptyLineBeforeContentLength_IsAccepted()
    {
        var formatter = new CapturingFormatter();
        byte[] frame = BuildFrame("x", leadingHeaders: "\r\n", newline: "\r\n");
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(frame), new MemoryStream(), formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(message);
        Assert.AreEqual("x", formatter.DeserializedText);
    }

    [TestMethod]
    public async Task TcpRead_BareLineFeeds_AreAccepted()
    {
        var formatter = new CapturingFormatter();
        byte[] frame = BuildFrame("x", leadingHeaders: null, newline: "\n");
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(frame), new MemoryStream(), formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(message);
        Assert.AreEqual("x", formatter.DeserializedText);
    }

    [TestMethod]
    public async Task TcpRead_LeadingUtf8Preamble_IsRemovedFromFirstHeader()
    {
        var formatter = new CapturingFormatter();
        byte[] frame = BuildFrame("x", leadingHeaders: null, newline: "\r\n");
        byte[] input = [.. Encoding.UTF8.GetPreamble(), .. frame];
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(input), new MemoryStream(), formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(message);
        Assert.AreEqual("x", formatter.DeserializedText);
    }

    [TestMethod]
    public async Task TcpRead_Utf8PreambleOnSecondHeader_IsNotRemoved()
    {
        var formatter = new CapturingFormatter();
        byte[] body = Encoding.UTF8.GetBytes("x");
        byte[] input =
        [
            .. Encoding.ASCII.GetBytes("X-First: value\r\n"),
            .. Encoding.UTF8.GetPreamble(),
            .. Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"),
            .. body,
        ];
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(input), new MemoryStream(), formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        Assert.AreEqual(0, formatter.DeserializeCalls);
    }

    [TestMethod]
    public async Task TcpRead_MissingContentLengthAfterEmptyLine_DoesNotConsumeBody()
    {
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream(Encoding.ASCII.GetBytes("\r\nx")),
            new MemoryStream(),
            formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        Assert.AreEqual(0, formatter.DeserializeCalls);
    }

    [TestMethod]
    public async Task TcpRead_PartialContentLengthHeader_ReachesEndOfFrameBeforeDisconnecting()
    {
        var stream = new CountingReadStream(Encoding.ASCII.GetBytes("Content-Length: 1"));
        using TcpMessageHandler handler = CreateHandler(stream, new MemoryStream(), new CapturingFormatter());

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        Assert.AreEqual(4, stream.ReadCalls);
    }

    [TestMethod]
    public async Task TcpRead_BodyAcrossBufferRefills_IsCopiedExactly()
    {
        string body = new('x', 5000);
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream(BuildFrame(body, leadingHeaders: null, newline: "\r\n")),
            new MemoryStream(),
            formatter);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(message);
        Assert.AreEqual(body, formatter.DeserializedText);
    }

    [TestMethod]
    public async Task TcpRead_BodyAcrossBufferRefills_StopsBeforeFollowingFrame()
    {
        string body = new('x', 5000);
        byte[] firstFrame = BuildFrame(body, leadingHeaders: null, newline: "\r\n");
        byte[] secondFrame = BuildFrame("y", leadingHeaders: null, newline: "\r\n");
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream([.. firstFrame, .. secondFrame]),
            new MemoryStream(),
            formatter);

        RpcMessage? first = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.AreEqual(body, formatter.DeserializedText);

        RpcMessage? second = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreEqual("y", formatter.DeserializedText);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task TcpRead_DelayedHeaderRead_DoesNotCaptureSynchronizationContext()
    {
        var stream = new GatedReadStream(BuildFrame("x", leadingHeaders: null, newline: "\r\n"));
        using TcpMessageHandler handler = CreateHandler(stream, new MemoryStream(), new CapturingFormatter());

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.ReadAsync(CancellationToken.None),
            stream.Release);
    }

    [TestMethod]
    public async Task TcpRead_DelayedBodyRead_DoesNotCaptureSynchronizationContext()
    {
        byte[] header = Encoding.ASCII.GetBytes("Content-Length: 1\r\n\r\n");
        var stream = new TwoStageGatedReadStream(header, Encoding.ASCII.GetBytes("x"));
        using TcpMessageHandler handler = CreateHandler(stream, new MemoryStream(), new CapturingFormatter());

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.ReadAsync(CancellationToken.None),
            stream.Release);
    }

    [TestMethod]
    public async Task TcpRead_ConnectionResetLogging_DoesNotCaptureSynchronizationContext()
    {
        var logger = new GatedLogger();
        using var handler = new TcpMessageHandler(
            new TcpClient(),
            new ConnectionResetStream(),
            new MemoryStream(),
            new CapturingFormatter(),
            logger);

        RecordingSynchronizationContext context = await StartUnderSynchronizationContextAsync(
            () => handler.ReadAsync(CancellationToken.None),
            async () =>
            {
                await logger.Started.Task.ConfigureAwait(false);
                logger.Release();
            });

        Assert.AreEqual(0, context.PostCount);
        Assert.AreEqual(0, logger.PostCount);
    }

    [TestMethod]
    public async Task TcpRead_ConnectionReset_LogsDiagnosticBeforeReturning()
    {
        var stream = new ConnectionResetStream();
        var logger = new CapturingLogger();
        using var handler = new TcpMessageHandler(
            new TcpClient(),
            stream,
            new MemoryStream(),
            new CapturingFormatter(),
            logger);

        RpcMessage? message = await handler.ReadAsync(CancellationToken.None);

        Assert.IsNull(message);
        Assert.AreEqual(
            $"TCP connection reset while reading; treating as client disconnect: {stream.Exception}",
            logger.Message);
    }

    [TestMethod]
    public async Task TcpRead_ConnectionReset_WaitsForInProgressDiagnostic()
    {
        var logger = new GatedLogger();
        using var handler = new TcpMessageHandler(
            new TcpClient(),
            new ConnectionResetStream(),
            new MemoryStream(),
            new CapturingFormatter(),
            logger);

        Task<RpcMessage?> readTask = handler.ReadAsync(CancellationToken.None);
        await logger.Started.Task.ConfigureAwait(false);

        Assert.IsFalse(readTask.IsCompleted);

        logger.Release();
        Assert.IsNull(await readTask.ConfigureAwait(false));
    }

    [TestMethod]
    public async Task TcpWrite_DelayedSerialization_DoesNotCaptureSynchronizationContext()
    {
        var formatter = new GatedSerializeFormatter("{}");
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), new MemoryStream(), formatter);

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None),
            formatter.Release);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task TcpWrite_DelayedHeaderLine_DoesNotCaptureSynchronizationContext(int headerLine)
    {
        const string Body = "{}";
        var stream = new PredicateGatedWriteStream(bytes => true);
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), stream, new ConstantFormatter(Body));
        PrefillWriterToDelayHeaderLine(handler, headerLine, Body);

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None),
            stream.Release);
    }

    [TestMethod]
    public async Task TcpWrite_DelayedHeaderFlush_DoesNotCaptureSynchronizationContext()
    {
        byte[] body = Encoding.UTF8.GetBytes("{}");
        var stream = new PredicateGatedWriteStream(bytes => !bytes.SequenceEqual(body));
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), stream, new ConstantFormatter("{}"));

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None),
            stream.Release);
    }

    [TestMethod]
    public async Task TcpWrite_DelayedBodyWrite_DoesNotCaptureSynchronizationContext()
    {
        byte[] body = Encoding.UTF8.GetBytes("{}");
        var stream = new PredicateGatedWriteStream(bytes => bytes.SequenceEqual(body));
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), stream, new ConstantFormatter("{}"));

        await AssertDoesNotPostToCurrentContextAsync(
            () => handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None),
            stream.Release);
    }

    [TestMethod]
    public void TcpRead_RentedBodyBuffer_IsReturnedToSharedPool()
    {
        const string Body = "pool-read-body-12345";
        var formatter = new CapturingFormatter();
        using TcpMessageHandler handler = CreateHandler(
            new MemoryStream(BuildFrame(Body, leadingHeaders: null, newline: "\r\n")),
            new MemoryStream(),
            formatter);

        _ = handler.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
        byte[] rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(Body));
        try
        {
            Assert.AreSame(formatter.DeserializedBuffer, rented);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [TestMethod]
    public void TcpWrite_RentedBodyBuffer_IsReturnedToSharedPool()
    {
        const string Body = "pool-write-body-12345";
        byte[] expectedBody = Encoding.UTF8.GetBytes(Body);
        var stream = new BodyBufferCapturingStream(expectedBody);
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), stream, new ConstantFormatter(Body));

        handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None).GetAwaiter().GetResult();
        byte[] rented = ArrayPool<byte>.Shared.Rent(expectedBody.Length);
        try
        {
            Assert.AreSame(stream.BodyBuffer, rented);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
#endif

    [TestMethod]
    public async Task TcpWrite_EmitsRequiredHeadersAndBody()
    {
        using var output = new MemoryStream();
        using TcpMessageHandler handler = CreateHandler(new MemoryStream(), output, new ConstantFormatter("{}"));

        await handler.WriteRequestAsync(new NotificationMessage("test", null), CancellationToken.None);

        string wire = Encoding.UTF8.GetString(output.ToArray());
        Assert.AreEqual(
            "Content-Length: 2\r\nContent-Type: application/testingplatform\r\n\r\n{}",
            wire);
    }

    [TestMethod]
    public void TcpDispose_DisposesBothStreamsAndTcpClient()
    {
        var client = new TrackingTcpClient();
        var readStream = new TrackingStream();
        var writeStream = new TrackingStream();
        var handler = new TcpMessageHandler(client, readStream, writeStream, new CapturingFormatter());

        handler.Dispose();

        Assert.IsTrue(readStream.IsDisposed);
        Assert.IsTrue(writeStream.IsDisposed);
        Assert.IsTrue(client.IsDisposed);
    }

    private static TcpMessageHandler CreateHandler(Stream input, Stream output, IMessageFormatter formatter)
        => new(new TcpClient(), input, output, formatter);

    private static (bool Parsed, int Result) TryParseNumericId(string value)
    {
        Type parserType = GetClientType("Microsoft.Testing.Platform.ServerMode.RpcIdParser");
        MethodInfo method = parserType.GetMethod(
            "TryParseNumericId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        object?[] arguments = [value, null];
        bool parsed = (bool)method.Invoke(null, arguments)!;
        return (parsed, (int)arguments[1]!);
    }

    private static object[] FlattenException(string? errorMessage, Exception? exception)
    {
        Type flattenerType = GetClientType("Microsoft.Testing.Platform.OutputDevice.Terminal.ExceptionFlattener");
        var flattened = (System.Collections.IEnumerable)flattenerType
            .GetMethod("Flatten", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, [errorMessage, exception])!;
        return [.. flattened.Cast<object>()];
    }

    private static Assembly ClientAssembly => typeof(ClientTestNode).Assembly;

    private static Type GetClientType(string name)
        => ClientAssembly.GetType(name, throwOnError: true)!;

    private static T GetStaticProperty<T>(string typeName, string propertyName)
        => (T)GetClientType(typeName)
            .GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(null)!;

    private static T GetStaticField<T>(string typeName, string fieldName)
        => (T)GetClientType(typeName)
            .GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(null)!;

    private static T GetInstanceProperty<T>(object instance, string propertyName)
        => (T)instance.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private static string FormatLog(string state, Exception? exception)
    {
        var formatter = (Delegate)GetClientType("Microsoft.Testing.Platform.Logging.LoggingExtensions")
            .GetField("Formatter", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(null)!;
        return (string)formatter.DynamicInvoke(state, exception)!;
    }

    private static object CreateSourceFormatter()
        => GetClientType("Microsoft.Testing.Platform.ServerMode.FormatterUtilities")
            .GetMethod("CreateFormatter", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, null)!;

    private static object CreateSourceNotification(string method, object? parameters)
        => Activator.CreateInstance(
            GetClientType("Microsoft.Testing.Platform.ServerMode.NotificationMessage"),
            method,
            parameters)!;

    private static class ErrorCodes
    {
        public static int ParseError => GetStaticField<int>(TypeName, nameof(ParseError));

        public static int InvalidRequest => GetStaticField<int>(TypeName, nameof(InvalidRequest));

        public static int MethodNotFound => GetStaticField<int>(TypeName, nameof(MethodNotFound));

        public static int InvalidParams => GetStaticField<int>(TypeName, nameof(InvalidParams));

        public static int InternalError => GetStaticField<int>(TypeName, nameof(InternalError));

        public static int ServerNotInitialized => GetStaticField<int>(TypeName, nameof(ServerNotInitialized));

        public static int LspErrorRangeStart => GetStaticField<int>(TypeName, nameof(LspErrorRangeStart));

        public static int RequestCanceled => GetStaticField<int>(TypeName, nameof(RequestCanceled));

        public static int LspErrorRangeEnd => GetStaticField<int>(TypeName, nameof(LspErrorRangeEnd));

        public static int TestingPlatformErrorRangeStart => GetStaticField<int>(TypeName, nameof(TestingPlatformErrorRangeStart));

        public static int ProtocolVersionNotSupported => GetStaticField<int>(TypeName, nameof(ProtocolVersionNotSupported));

        public static int TestingPlatformErrorRangeEnd => GetStaticField<int>(TypeName, nameof(TestingPlatformErrorRangeEnd));

        private const string TypeName = "Microsoft.Testing.Platform.ServerMode.ErrorCodes";
    }

    private static class ServerTestingCapabilities
    {
        public static bool SupportsTestCoverageMessages
            => GetStaticProperty<bool>(
                "Microsoft.Testing.Platform.ServerMode.ServerTestingCapabilities",
                nameof(SupportsTestCoverageMessages));
    }

    private abstract record RpcMessage;

    private sealed record NotificationMessage(string Method, object? Params) : RpcMessage;

    private interface IMessageFormatter
    {
        string Id { get; }

        object? Deserialize(ReadOnlyMemory<byte> serializedUtf8Content);

        Task<string> SerializeAsync(object obj);
    }

    private sealed class TcpMessageHandler : IDisposable
    {
        private readonly object _handler;

        public TcpMessageHandler(
            TcpClient client,
            Stream input,
            Stream output,
            IMessageFormatter formatter,
            ClientLogger? logger = null)
        {
            Type handlerType = GetClientType("Microsoft.Testing.Platform.ServerMode.TcpMessageHandler");
            object formatterProxy = FormatterDispatchProxy.Create(formatter);
            ConstructorInfo constructor = handlerType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate => candidate.GetParameters().Length == (logger is null ? 4 : 5));
            object?[] arguments = logger is null
                ? [client, input, output, formatterProxy]
                : [client, input, output, formatterProxy, logger];
            _handler = constructor.Invoke(arguments);
        }

        public StreamWriter Writer
            => (StreamWriter)_handler.GetType()
                .GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(_handler)!;

        public async Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
        {
            var task = (Task)_handler.GetType()
                .GetMethod("ReadAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .Invoke(_handler, [cancellationToken])!;
            await task.ConfigureAwait(false);
            object? result = task.GetType().GetProperty("Result")!.GetValue(task);
            return result is null ? null : new NotificationMessage("captured", null);
        }

        public Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
        {
            var notification = (NotificationMessage)message;
            object sourceMessage = CreateSourceNotification(notification.Method, notification.Params);
            return (Task)_handler.GetType()
                .GetMethod("WriteRequestAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .Invoke(_handler, [sourceMessage, cancellationToken])!;
        }

        public void Dispose() => ((IDisposable)_handler).Dispose();
    }

    private class FormatterDispatchProxy : DispatchProxy
    {
        public IMessageFormatter Formatter { get; set; } = null!;

        public static object Create(IMessageFormatter formatter)
        {
            object proxy = DispatchProxy.Create(
                GetClientType("Microsoft.Testing.Platform.ServerMode.IMessageFormatter"),
                typeof(FormatterDispatchProxy));
            ((FormatterDispatchProxy)proxy).Formatter = formatter;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Name switch
            {
                "get_Id" => Formatter.Id,
                "Deserialize" => Deserialize((ReadOnlyMemory<byte>)args![0]!),
                "SerializeAsync" => Formatter.SerializeAsync(args![0]!),
                _ => throw new NotSupportedException(targetMethod.Name),
            };

        private object Deserialize(ReadOnlyMemory<byte> content)
        {
            _ = Formatter.Deserialize(content);
            return CreateSourceNotification("captured", null);
        }
    }

    private static byte[] BuildFrame(string body, string? leadingHeaders, string newline)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        return
        [
            .. Encoding.ASCII.GetBytes(
                $"{leadingHeaders}Content-Length: {bodyBytes.Length}{newline}{newline}"),
            .. bodyBytes,
        ];
    }

#if NETCOREAPP
    private static async Task AssertDoesNotPostToCurrentContextAsync(Func<Task> action, Action release)
    {
        RecordingSynchronizationContext context = await StartUnderSynchronizationContextAsync(
            action,
            () =>
            {
                release();
                return Task.CompletedTask;
            });

        Assert.AreEqual(0, context.PostCount);
    }

    private static async Task<RecordingSynchronizationContext> StartUnderSynchronizationContextAsync(
        Func<Task> action,
        Func<Task> releaseAsync)
    {
        var context = new RecordingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task task;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            task = action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await releaseAsync().ConfigureAwait(false);
        await task.ConfigureAwait(false);
        return context;
    }

    private static void PrefillWriterToDelayHeaderLine(TcpMessageHandler handler, int headerLine, string body)
    {
        StreamWriter writer = handler.Writer;
        FieldInfo charBufferField = typeof(StreamWriter)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(char[]))
            .OrderByDescending(field => ((char[]?)field.GetValue(writer))?.Length ?? 0)
            .First();
        char[] charBuffer = (char[])charBufferField.GetValue(writer)!;
        string[] lines =
        [
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}{writer.NewLine}",
            $"Content-Type: application/testingplatform{writer.NewLine}",
            writer.NewLine,
        ];
        int charsBeforeTarget = lines.Take(headerLine - 1).Sum(line => line.Length);
        int targetLength = lines[headerLine - 1].Length;
        int remainingCapacity = charsBeforeTarget + targetLength - 1;

        writer.Write(new string('x', charBuffer.Length - remainingCapacity));
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
#endif

    private sealed class CapturingFormatter : IMessageFormatter
    {
        public string Id => nameof(CapturingFormatter);

        public int DeserializeCalls { get; private set; }

        public int DeserializedByteCount { get; private set; }

        public string? DeserializedText { get; private set; }

        public byte[]? DeserializedBuffer { get; private set; }

        public object Deserialize(ReadOnlyMemory<byte> serializedUtf8Content)
        {
            DeserializeCalls++;
            DeserializedByteCount = serializedUtf8Content.Length;
            DeserializedText = Encoding.UTF8.GetString(serializedUtf8Content.Span);
            Assert.IsTrue(MemoryMarshal.TryGetArray(serializedUtf8Content, out ArraySegment<byte> segment));
            DeserializedBuffer = segment.Array;
            return new NotificationMessage("captured", null);
        }

        public Task<string> SerializeAsync(object obj) => Task.FromResult("{}");
    }

    private sealed class ConstantFormatter(string serialized) : IMessageFormatter
    {
        public string Id => nameof(ConstantFormatter);

        public object Deserialize(ReadOnlyMemory<byte> serializedUtf8Content) => throw new NotSupportedException();

        public Task<string> SerializeAsync(object obj) => Task.FromResult(serialized);
    }

    private sealed class GatedSerializeFormatter(string serialized) : IMessageFormatter
    {
        private readonly TaskCompletionSource<string> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Id => nameof(GatedSerializeFormatter);

        public object Deserialize(ReadOnlyMemory<byte> serializedUtf8Content) => throw new NotSupportedException();

        public Task<string> SerializeAsync(object obj) => _completion.Task;

        public void Release() => _completion.TrySetResult(serialized);
    }

    private sealed class GatedReadStream(byte[] data) : Stream
    {
        private readonly TaskCompletionSource<int> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Memory<byte> _destination;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => data.Length;

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _destination = buffer;
            return new ValueTask<int>(_completion.Task);
        }

        public void Release()
        {
            data.AsSpan().CopyTo(_destination.Span);
            _completion.TrySetResult(data.Length);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TwoStageGatedReadStream(byte[] first, byte[] second) : Stream
    {
        private readonly TaskCompletionSource<int> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Memory<byte> _destination;

        private int _readIndex;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => first.Length + second.Length;

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_readIndex++ == 0)
            {
                first.AsSpan().CopyTo(buffer.Span);
                return new ValueTask<int>(first.Length);
            }

            if (_readIndex == 2)
            {
                _destination = buffer;
                return new ValueTask<int>(_completion.Task);
            }

            return new ValueTask<int>(0);
        }

        public void Release()
        {
            second.AsSpan().CopyTo(_destination.Span);
            _completion.TrySetResult(second.Length);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PredicateGatedWriteStream(Func<byte[], bool> shouldGate) : MemoryStream
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private byte[]? _pending;

        private bool _gated;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] bytes = buffer.ToArray();
            if (!_gated && shouldGate(bytes))
            {
                _gated = true;
                _pending = bytes;
                return new ValueTask(_completion.Task);
            }

            Write(bytes, 0, bytes.Length);
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            byte[] bytes = buffer.AsSpan(offset, count).ToArray();
            if (!_gated && shouldGate(bytes))
            {
                _gated = true;
                _pending = bytes;
                return _completion.Task;
            }

            Write(bytes, 0, bytes.Length);
            return Task.CompletedTask;
        }

        public void Release()
        {
            Assert.IsNotNull(_pending, "The expected write was not delayed.");
            Write(_pending, 0, _pending.Length);
            _completion.TrySetResult(true);
        }
    }

    private sealed class BodyBufferCapturingStream(byte[] expectedBody) : MemoryStream
    {
        public byte[]? BodyBuffer { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Span.SequenceEqual(expectedBody))
            {
                Assert.IsTrue(MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment));
                BodyBuffer = segment.Array;
            }

            return base.WriteAsync(buffer, cancellationToken);
        }
    }

    private sealed class ConnectionResetStream : MemoryStream
    {
        public SocketException Exception { get; } = new((int)SocketError.ConnectionReset);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(Exception);
    }

    private sealed class GatedLogger : ClientLogger
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly RecordingSynchronizationContext _context = new();

        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PostCount => _context.PostCount;

        public bool IsEnabled(ClientLogLevel logLevel) => true;

        public void Log<TState>(
            ClientLogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => throw new NotSupportedException();

        public Task LogAsync<TState>(
            ClientLogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            SynchronizationContext.SetSynchronizationContext(_context);
            Started.TrySetResult(true);
            return _completion.Task;
        }

        public void Release() => _completion.TrySetResult(true);
    }

    private sealed class CapturingLogger : ClientLogger
    {
        public string? Message { get; private set; }

        public bool IsEnabled(ClientLogLevel logLevel) => true;

        public void Log<TState>(
            ClientLogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Message = formatter(state, exception);

        public Task LogAsync<TState>(
            ClientLogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Message = formatter(state, exception);
            return Task.CompletedTask;
        }
    }
#endif

    private sealed class CountingReadStream(byte[] data) : MemoryStream(data)
    {
        public int ReadCalls { get; private set; }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NETCOREAPP
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return base.ReadAsync(buffer, cancellationToken);
        }
#endif
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingTcpClient : TcpClient
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
