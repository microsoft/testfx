// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests.IPC;

[TestClass]
public sealed class DotnetTestHttpClientTests
{
    private static readonly Uri Endpoint = new("https://127.0.0.1:12345/dotnettest");
    private const string Token = "test-only-256-bit-style-token-value";
    private const int RequestSerializerId = 201;
    private const int ResponseSerializerId = 202;
    private readonly TestContext _testContext;

    public DotnetTestHttpClientTests(TestContext testContext)
        => _testContext = testContext;

    [TestMethod]
    public async Task RequestReplyAsync_PostsFullFrameWithBearerAuthentication()
    {
        byte[]? requestBody = null;
        AuthenticationHeaderValue? authorization = null;
        MediaTypeHeaderValue? contentType = null;
        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            authorization = request.Headers.Authorization;
            contentType = request.Content.Headers.ContentType;
            return CreateResponse(TestResponseFrame());
        });

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        _ = await client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        Assert.IsNotNull(requestBody);
        Assert.IsGreaterThanOrEqualTo(8, requestBody.Length);
        Assert.AreEqual(requestBody.Length - sizeof(int), BitConverter.ToInt32(requestBody, 0));
        Assert.AreEqual(RequestSerializerId, BitConverter.ToInt32(requestBody, sizeof(int)));
        Assert.AreEqual("Bearer", authorization?.Scheme);
        Assert.AreEqual(Token, authorization?.Parameter);
        Assert.AreEqual("application/octet-stream", contentType?.MediaType);
    }

    [TestMethod]
    public async Task RequestReplyAsync_SerializesConcurrentRequests()
    {
        int activeRequests = 0;
        int maximumActiveRequests = 0;
        var firstRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requestCount = 0;
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            int active = Interlocked.Increment(ref activeRequests);
            maximumActiveRequests = Math.Max(maximumActiveRequests, active);
            int currentRequest = Interlocked.Increment(ref requestCount);
            if (currentRequest == 1)
            {
                firstRequestEntered.SetResult();
                await releaseFirstRequest.Task.WaitAsync(cancellationToken);
            }

            Interlocked.Decrement(ref activeRequests);
            return CreateResponse(TestResponseFrame());
        });

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        Task<TestResponse> first = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);
        await firstRequestEntered.Task;

        Task<TestResponse> second = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        Assert.AreEqual(1, requestCount);
        Assert.IsFalse(second.IsCompleted);
        releaseFirstRequest.SetResult();
        await Task.WhenAll(first, second);
        Assert.AreEqual(1, maximumActiveRequests);
        Assert.AreEqual(2, requestCount);
    }

    [TestMethod]
    public async Task RequestReplyAsync_PropagatesCancellation()
    {
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateHttpMessageHandler(async (_, _) => await pendingResponse.Task);

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                cancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task RequestReplyAsync_CancellationDoesNotAllowOverlappingSend()
    {
        int requestCount = 0;
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.SetResult();
            return pendingResponse.Task;
        });

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource();
        Task<TestResponse> activeRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            cancellationTokenSource.Token);
        await requestStarted.Task;
        Task<TestResponse> waitingRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => activeRequest);
        await Assert.ThrowsAsync<OperationCanceledException>(() => waitingRequest);
        Assert.AreEqual(1, requestCount);
        Assert.IsFalse(client.IsConnected);
        pendingResponse.SetResult(CreateResponse(TestResponseFrame()));
    }

    [TestMethod]
    public async Task RequestReplyAsync_ResponseBodyCancellationDoesNotAllowOverlappingSend()
    {
        int requestCount = 0;
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            var content = new StreamContent(new CancellationBlockingReadStream(readStarted));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource();
        Task<TestResponse> activeRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            cancellationTokenSource.Token);
        await readStarted.Task;
        Task<TestResponse> waitingRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => activeRequest);
        await Assert.ThrowsAsync<OperationCanceledException>(() => waitingRequest);
        Assert.AreEqual(1, requestCount);
        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task RequestReplyAsync_FailureCancelsQueuedRequest()
    {
        int requestCount = 0;
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.SetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        Task<TestResponse> activeRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);
        await requestStarted.Task;
        Task<TestResponse> waitingRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        releaseResponse.SetResult();

        await Assert.ThrowsExactlyAsync<IOException>(() => activeRequest);
        await Assert.ThrowsAsync<OperationCanceledException>(() => waitingRequest);
        Assert.AreEqual(1, requestCount);
        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task RequestReplyAsync_UsesCallerCancellationInsteadOfHttpClientTimeout()
    {
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(100, cancellationToken);
            return CreateResponse(TestResponseFrame());
        });

        using DotnetTestHttpClient client = CreateClient(handler, TimeSpan.FromMilliseconds(10));
        await client.ConnectAsync(_testContext.CancellationToken);

        TestResponse response = await client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);
        Assert.AreSame(TestResponse.Instance, response);
    }

    [TestMethod]
    public async Task RequestReplyAsync_DisposeCancelsActiveRequestAndWaiter()
    {
        var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestEntered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateResponse(TestResponseFrame());
        });

        DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);
        Task<TestResponse> activeRequest = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);
        await requestEntered.Task;
        Task<TestResponse> waiter = client.RequestReplyAsync<TestRequest, TestResponse>(
            TestRequest.Instance,
            _testContext.CancellationToken);

        client.Dispose();

        await Assert.ThrowsAsync<OperationCanceledException>(() => activeRequest);
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiter);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(int.MaxValue)]
    public async Task RequestReplyAsync_RejectsInvalidChunkedFramePayloadLength(int payloadLength)
    {
        byte[] body = BitConverter.GetBytes(payloadLength);
        var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(CreateChunkedResponse(body)));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains("invalid response payload length", exception.Message);
    }

    [TestMethod]
    public async Task RequestReplyAsync_RejectsNonSuccessWithoutLeakingToken()
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = Token,
            }));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains("401", exception.Message);
        Assert.DoesNotContain(Token, exception.ToString());
    }

    [TestMethod]
    [DataRow(HttpStatusCode.TemporaryRedirect)]
    [DataRow(HttpStatusCode.PermanentRedirect)]
    public async Task RequestReplyAsync_RejectsRedirectResponses(HttpStatusCode statusCode)
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Headers = { Location = new Uri("https://redirect.example/dotnettest") },
            }));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains(((int)statusCode).ToString(CultureInfo.InvariantCulture), exception.Message);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("text/plain")]
    [DataRow("application/test-only-256-bit-style-token-value")]
    public async Task RequestReplyAsync_RejectsUnexpectedResponseContentType(string? mediaType)
    {
        HttpResponseMessage response = CreateResponse(TestResponseFrame());
        response.Content.Headers.ContentType = mediaType is null ? null : new MediaTypeHeaderValue(mediaType);
        var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains("content type", exception.Message);
        Assert.Contains(mediaType is null ? "no content type" : "unexpected content type", exception.Message);
        if (mediaType is not null)
        {
            Assert.DoesNotContain(mediaType, exception.Message);
        }
    }

    [TestMethod]
    public async Task RequestReplyAsync_RejectsMissingResponseContent()
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = null! }));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains("no content type", exception.Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RequestReplyAsync_RejectsMissingOrMultipleResponseFrames(bool appendSecondFrame)
    {
        byte[] body = appendSecondFrame
            ? [.. TestResponseFrame(), .. TestResponseFrame()]
            : [];
        var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(CreateResponse(body)));

        using DotnetTestHttpClient client = CreateClient(handler);
        await client.ConnectAsync(_testContext.CancellationToken);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));
        Assert.Contains(appendSecondFrame ? "more than one" : "invalid response frame length", exception.Message);
    }

    [TestMethod]
    public async Task RequestReplyAsync_RequiresConnectAndRejectsUseAfterDispose()
    {
        var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(CreateResponse(TestResponseFrame())));
        DotnetTestHttpClient client = CreateClient(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            client.RequestReplyAsync<TestRequest, TestResponse>(
                TestRequest.Instance,
                _testContext.CancellationToken));

        client.Dispose();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => client.ConnectAsync(_testContext.CancellationToken));
    }

    private static DotnetTestHttpClient CreateClient(HttpMessageHandler handler, TimeSpan? httpClientTimeout = null)
    {
        var httpClient = new HttpClient(handler);
        if (httpClientTimeout is not null)
        {
            httpClient.Timeout = httpClientTimeout.Value;
        }

        var client = new DotnetTestHttpClient(Endpoint, Token, httpClient, disposeHttpClient: true);
        client.RegisterSerializer(new TestRequestSerializer(), typeof(TestRequest));
        client.RegisterSerializer(new TestResponseSerializer(), typeof(TestResponse));
        return client;
    }

    private static HttpResponseMessage CreateResponse(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    private static HttpResponseMessage CreateChunkedResponse(byte[] body)
    {
        var content = new StreamContent(new NonSeekableReadStream(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    private static byte[] TestResponseFrame()
        => [.. BitConverter.GetBytes(sizeof(int)), .. BitConverter.GetBytes(ResponseSerializerId)];

    private sealed class TestRequest : IRequest
    {
        public static readonly TestRequest Instance = new();
    }

    private sealed class TestResponse : IResponse
    {
        public static readonly TestResponse Instance = new();
    }

    private sealed class TestRequestSerializer : NamedPipeSerializer<TestRequest>, INamedPipeSerializer
    {
        public override int Id => RequestSerializerId;

        protected override TestRequest DeserializeCore(Stream _)
            => TestRequest.Instance;

        protected override void SerializeCore(TestRequest _, Stream __)
        {
        }
    }

    private sealed class TestResponseSerializer : NamedPipeSerializer<TestResponse>, INamedPipeSerializer
    {
        public override int Id => ResponseSerializerId;

        protected override TestResponse DeserializeCore(Stream _)
            => TestResponse.Instance;

        protected override void SerializeCore(TestResponse _, Stream __)
        {
        }
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => sendAsync(request, cancellationToken);
    }

    private sealed class NonSeekableReadStream(byte[] body) : Stream
    {
        private readonly MemoryStream _innerStream = new(body);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set
            {
                _ = value;
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _innerStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class CancellationBlockingReadStream(TaskCompletionSource readStarted) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set
            {
                _ = value;
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            readStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
