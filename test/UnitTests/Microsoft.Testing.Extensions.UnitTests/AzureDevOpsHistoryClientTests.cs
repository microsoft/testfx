// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

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

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task GetRunsAsync_NonPositiveLimitDoesNotSendRequests(int maximum)
    {
        ScriptedHandler handler = new();
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        Assert.IsEmpty(await client.GetRunsAsync(CreateQuery(), maximum, CancellationToken.None));
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task GetRunsAsync_RepeatedContinuationStopsAndPreservesObservedRuns()
    {
        ScriptedHandler handler = new(
            () => WithContinuation(CreateRunsResponse("https://example/run/first"), "next & token"),
            () => WithContinuation(CreateRunsResponse("https://example/run/second"), "next & token"));
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        IReadOnlyList<AzureDevOpsTestRun> runs = await client.GetRunsAsync(CreateQuery(), 10, CancellationToken.None);

        Assert.AreSequenceEqual(["https://example/run/first", "https://example/run/second"], runs.Select(run => run.Url));
        Assert.HasCount(2, handler.Requests);
        Assert.Contains("continuationToken=next%20%26%20token", handler.Requests[1].RequestUri!.Query);
        Assert.DoesNotContain("$skip", handler.Requests[1].RequestUri!.Query);
    }

    [TestMethod]
    [DataRow("null", DisplayName = "Null payload")]
    [DataRow("{}", DisplayName = "Missing values")]
    [DataRow("""{"value":null}""", DisplayName = "Null values")]
    [DataRow("""{"value":[{"url":""},{"url":" "},{}]}""", DisplayName = "Invalid run URLs")]
    public async Task GetRunsAsync_EmptyAndInvalidPayloadsReturnNoRuns(string body)
    {
        ScriptedHandler handler = new(() => Response(HttpStatusCode.OK, body));
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        Assert.IsEmpty(await client.GetRunsAsync(CreateQuery(), 10, CancellationToken.None));
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task GetResultsAsync_FiltersIncompleteRowsAndPreservesDurationAndContinuation()
    {
        string body =
            """
            {"value":[
              {"automatedTestName":"Namespace.First","outcome":"Passed","durationInMs":12.5},
              {"automatedTestName":"","outcome":"Failed"},
              {"automatedTestName":"Namespace.MissingOutcome","outcome":" "},
              {"automatedTestName":"Namespace.Second","outcome":"Failed"}
            ]}
            """;
        ScriptedHandler handler = new(() => WithContinuation(Response(HttpStatusCode.OK, body), "result token"));
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        AzureDevOpsTestResultsPage page = await client.GetResultsAsync(CreateQuery(), "https://example/run///", 7, 20, null, CancellationToken.None);

        Assert.AreSequenceEqual(["Namespace.First", "Namespace.Second"], page.Results.Select(result => result.AutomatedTestName));
        Assert.AreSequenceEqual(["Passed", "Failed"], page.Results.Select(result => result.Outcome));
        Assert.AreEqual(12.5, page.Results[0].DurationMilliseconds);
        Assert.IsNull(page.Results[1].DurationMilliseconds);
        Assert.AreEqual("result token", page.ContinuationToken);
        Assert.AreEqual("https://example/run/results?outcomes=Failed,Passed&$top=20&$skip=7&api-version=7.1", handler.Requests[0].RequestUri!.OriginalString);
        Assert.AreEqual("Basic", handler.Requests[0].Headers.Authorization!.Scheme);
        Assert.AreEqual("OnRva2Vu", handler.Requests[0].Headers.Authorization!.Parameter);
    }

    [TestMethod]
    [DataRow("Namespace.MyType.MyMethod (1)", "MyMethod (1)", DisplayName = "Data-row method history")]
    [DataRow("Namespace.MyType.MyMethod (1)", "Namespace.MyType.MyMethod(1)", DisplayName = "Qualified data-row method history")]
    [DataRow("Namespace.MyType.MyMethod (Custom scenario)", "Custom scenario", DisplayName = "Custom display-name method history")]
    [DataRow("Namespace.MyType.MyMethod (MyMethodology)", "MyMethodology", DisplayName = "Custom name sharing method prefix")]
    [DataRow("Namespace.MyType.MyMethod", "MyMethod", DisplayName = "Ordinary method history")]
    [DataRow("Display name (1)", "Display name (1)", DisplayName = "Display-only identity is preserved")]
    [DataRow("Namespace.MyType.MyMethod (1)", null, DisplayName = "Missing title preserves identity")]
    [DataRow("Namespace.MyType.MyMethod (1)", "OtherMethod (1)", DisplayName = "Unrelated title preserves identity")]
    [DataRow("Namespace.MyType.Verify (empty)", "Verify (empty)", DisplayName = "Parentheses in ordinary method identity")]
    [DataRow("Namespace.MyType.Verify (empty) (1)", "Verify (empty) (1)", DisplayName = "Parentheses in data-driven method identity")]
    public async Task GetResultsAsync_PreservesPublishedIdentityAndTitle(string automatedTestName, string? testCaseTitle)
    {
        string body = JsonSerializer.Serialize(new
        {
            value = new[] { new { automatedTestName, testCaseTitle, comment = "MTP.CanonicalTestName/1:26", outcome = "Passed", durationInMs = 12.5 } },
        });
        ScriptedHandler handler = new(() => Response(HttpStatusCode.OK, body));
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        AzureDevOpsTestResultsPage page = await client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None);

        AzureDevOpsTestResult result = Assert.ContainsSingle(page.Results);
        Assert.AreEqual(automatedTestName, result.AutomatedTestName);
        Assert.AreEqual(testCaseTitle, result.TestCaseTitle);
        Assert.AreEqual("MTP.CanonicalTestName/1:26", result.Comment);
        Assert.AreEqual("Passed", result.Outcome);
        Assert.AreEqual(12.5, result.DurationMilliseconds);
    }

    [TestMethod]
    [DataRow("null", DisplayName = "Null result payload")]
    [DataRow("{}", DisplayName = "Missing result values")]
    public async Task GetResultsAsync_EmptyPayloadUsesEncodedContinuationInsteadOfSkip(string body)
    {
        ScriptedHandler handler = new(() => Response(HttpStatusCode.OK, body));
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);
        AzureDevOpsTestResultsPage page = await client.GetResultsAsync(CreateQuery(), "https://example/run", 99, 3, "a+b", CancellationToken.None);

        Assert.IsEmpty(page.Results);
        Assert.IsNull(page.ContinuationToken);
        Assert.Contains("continuationToken=a%2Bb", handler.Requests[0].RequestUri!.Query);
        Assert.DoesNotContain("$skip", handler.Requests[0].RequestUri!.Query);
    }

    [TestMethod]
    [DataRow(408)]
    [DataRow(429)]
    [DataRow(500)]
    [DataRow(502)]
    [DataRow(503)]
    [DataRow(504)]
    public async Task GetResultsAsync_TransientStatusUsesExponentialDelayAndFreshRequest(int status)
    {
        ScriptedHandler handler = new(
            () => Response((HttpStatusCode)status, string.Empty),
            () => Response((HttpStatusCode)status, string.Empty),
            () => Response(HttpStatusCode.OK, """{"value":[]}"""));
        using HttpClient http = new(handler);
        TestTask task = new();
        AzureDevOpsHistoryClient client = new(task, new TestClock(), http);

        Assert.IsEmpty((await client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None)).Results);
        Assert.AreSequenceEqual([TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000)], task.Delays);
        Assert.HasCount(3, handler.Requests);
        Assert.AreNotSame(handler.Requests[0], handler.Requests[1]);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Delta retry after")]
    [DataRow(1, DisplayName = "Future date retry after")]
    [DataRow(2, DisplayName = "Past date fallback")]
    public async Task GetResultsAsync_RetryAfterUsesClockAndFallsBackForPastDate(int kind)
    {
        TestClock clock = new();
        ScriptedHandler handler = new(
            () =>
            {
                HttpResponseMessage response = Response(HttpStatusCode.ServiceUnavailable, string.Empty);
                response.Headers.RetryAfter = kind == 0
                    ? new RetryConditionHeaderValue(TimeSpan.FromSeconds(3))
                    : new RetryConditionHeaderValue(clock.UtcNow.AddSeconds(kind == 1 ? 7 : -1));
                return response;
            },
            () => Response(HttpStatusCode.OK, """{"value":[]}"""));
        using HttpClient http = new(handler);
        TestTask task = new();
        AzureDevOpsHistoryClient client = new(task, clock, http);

        await client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None);
        Assert.AreEqual(kind == 0 ? TimeSpan.FromSeconds(3) : kind == 1 ? TimeSpan.FromSeconds(7) : TimeSpan.FromMilliseconds(500), Assert.ContainsSingle(task.Delays));
    }

    [TestMethod]
    public async Task GetResultsAsync_NetworkFailuresRetryThenPropagateOriginalException()
    {
        HttpRequestException failure = new("network unavailable");
        ScriptedHandler handler = new(() => throw failure, () => throw failure, () => throw failure);
        using HttpClient http = new(handler);
        TestTask task = new();
        AzureDevOpsHistoryClient client = new(task, new TestClock(), http);

        HttpRequestException actual = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None));
        Assert.AreSame(failure, actual);
        Assert.HasCount(3, handler.Requests);
        Assert.AreSequenceEqual([TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000)], task.Delays);
    }

    [TestMethod]
    public async Task GetResultsAsync_TimeoutExhaustionIsDistinctFromCallerCancellation()
    {
        ScriptedHandler handler = new(
            () => throw new OperationCanceledException(),
            () => throw new OperationCanceledException(),
            () => throw new OperationCanceledException());
        using HttpClient http = new(handler);
        TestTask task = new();
        AzureDevOpsHistoryClient client = new(task, new TestClock(), http);

        HttpRequestException error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None));
        Assert.AreEqual("Azure DevOps history request timed out.", error.Message);
        Assert.IsInstanceOfType<OperationCanceledException>(error.InnerException);
        Assert.HasCount(3, handler.Requests);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, cancellation.Token));
        Assert.HasCount(3, handler.Requests);
    }

    [TestMethod]
    public async Task GetResultsAsync_TerminalErrorUsesFallbackReasonAndTruncatesContentExactly()
    {
        string content = new('x', 501);
        ScriptedHandler handler = new(
            () => new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = string.Empty, Content = new StringContent(content) },
            () => new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = string.Empty, Content = new StringContent(content) },
            () => new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = string.Empty, Content = new StringContent(content) });
        using HttpClient http = new(handler);
        AzureDevOpsHistoryClient client = new(new TestTask(), new TestClock(), http);

        HttpRequestException error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => client.GetResultsAsync(CreateQuery(), "https://example/run", 0, 10, null, CancellationToken.None));
        Assert.AreEqual("Azure DevOps history request failed with status code 400 (BadRequest). " + new string('x', 500), error.Message);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };

    private static HttpResponseMessage WithContinuation(HttpResponseMessage response, string token)
    {
        response.Headers.Add("x-ms-continuationtoken", token);
        return response;
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
        public List<TimeSpan> Delays { get; } = [];

        public Task Delay(int millisecondDelay)
            => Task.CompletedTask;

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            Delays.Add(timeSpan);
            return Task.CompletedTask;
        }

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

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue()());
        }
    }
}
