// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsHistoryClientTests
{
    [TestMethod]
    public async Task GetRunsAsync_UsesDefinitionsQueryParameterAndExpectedHeadersAsync()
    {
        Queue<HttpResponseMessage> responses = new([
            CreateRunsResponse("https://example/_apis/test/Runs/1"),
        ]);
        RecordingHttpMessageHandler handler = new(responses);
        using HttpClient httpClient = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), httpClient);

        await client.GetRunsAsync(CreateQuery(), 1, CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(1, handler.Requests);
        HttpRequestMessage request = handler.Requests[0];
        Uri requestUri = request.RequestUri!;
        Assert.Contains("definitions=123", requestUri.Query);
        Assert.AreEqual("application/json", request.Headers.Accept.Single().MediaType);
        ProductInfoHeaderValue userAgent = request.Headers.UserAgent.Single();
        Assert.AreEqual("Microsoft.Testing.Extensions.AzureDevOpsReport", userAgent.Product?.Name);
    }

    [TestMethod]
    public async Task GetRunsAsync_PagesUntilMaximumRunCountIsReachedAsync()
    {
        Queue<HttpResponseMessage> responses = new([
            CreateRunsResponse(Enumerable.Range(1, 200).Select(static i => $"https://example/_apis/test/Runs/{i}").ToArray()),
            CreateRunsResponse(Enumerable.Range(201, 50).Select(static i => $"https://example/_apis/test/Runs/{i}").ToArray()),
        ]);
        RecordingHttpMessageHandler handler = new(responses);
        using HttpClient httpClient = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), httpClient);

        IReadOnlyList<AzureDevOpsTestRun> runs = await client.GetRunsAsync(CreateQuery(), 250, CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(250, runs);
        Assert.HasCount(2, handler.Requests);
        Uri firstRequestUri = handler.Requests[0].RequestUri!;
        Assert.Contains("$top=200", firstRequestUri.Query);
        Uri secondRequestUri = handler.Requests[1].RequestUri!;
        Assert.Contains("$skip=200", secondRequestUri.Query);
        Assert.Contains("$top=50", secondRequestUri.Query);
    }

    private static AzureDevOpsHistoryQuery CreateQuery()
        => new("https://dev.azure.com/example/", "testfx", "token", "123", new DateTimeOffset(2025, 05, 01, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 05, 16, 0, 0, 0, TimeSpan.Zero));

    private static HttpResponseMessage CreateRunsResponse(params string[] runUrls)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"value\":[{string.Join(",", runUrls.Select(static runUrl => $"{{\"url\":\"{runUrl}\"}}"))}]}}"),
        };

    private sealed class RecordingHttpMessageHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = responses;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2025, 05, 16, 12, 00, 00, TimeSpan.Zero);
    }

    private sealed class TestTask : ITask
    {
        public Task Delay(int millisecondDelay)
            => Task.CompletedTask;

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task Run(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
            => function();

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken)
            => function()!;

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
            => action();

        public Task WhenAll(params Task[] tasks)
            => Task.WhenAll(tasks);
    }
}
