// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsClientBehaviorTests
{
    [TestMethod]
    public async Task UpdateRun_UsesEscapedUriAuthenticationAndPatchPayload()
    {
        using var fixture = new CiReporterFixture();
        using var handler = new Handler(_ => ReplyAsync(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);
        AzureDevOpsPublishConfiguration configuration = fixture.PublishConfiguration() with { Project = "Team A/B", AccessToken = "literal-token" };

        await client.UpdateTestRunStateAsync(configuration, 73, "Completed", CancellationToken.None);
        var recovered = new AzureDevOpsTestCaseResult("Tests.Recovered", "tests.dll", "recovered row", "Passed", 12, null, null, null, null) { Id = 91 };
        await client.UpdateTestResultsAsync(configuration, 73, [recovered], CancellationToken.None);

        Assert.HasCount(2, handler.Requests);
        Request request = handler.Requests[0];
        Assert.AreEqual("PATCH", request.Method);
        Assert.AreEqual("https://ci.invalid/org/Team%20A%2FB/_apis/test/runs/73?api-version=7.1", request.Uri);
        Assert.AreEqual("Basic", request.AuthenticationScheme);
        Assert.AreEqual(":literal-token", Encoding.UTF8.GetString(Convert.FromBase64String(request.Authentication!)));
        Assert.AreEqual("{\"state\":\"Completed\"}", request.Body);
        Assert.AreEqual("application/json; api-version=7.1", request.Accept);
        Assert.IsTrue(request.Token.CanBeCanceled);
        Assert.AreEqual("PATCH", handler.Requests[1].Method);
        Assert.AreEqual("https://ci.invalid/org/Team%20A%2FB/_apis/test/runs/73/results?api-version=7.1", handler.Requests[1].Uri);
        using var resultPayload = JsonDocument.Parse(handler.Requests[1].Body);
        JsonElement row = resultPayload.RootElement[0];
        Assert.AreEqual(91, row.GetProperty("id").GetInt32());
        Assert.AreEqual("Passed", row.GetProperty("outcome").GetString());
        Assert.AreEqual(JsonValueKind.Null, row.GetProperty("errorMessage").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, row.GetProperty("stackTrace").ValueKind);
        Assert.IsEmpty(fixture.Delays);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Attachment_Run")]
    [DataRow(1, DisplayName = "Attachment_Result")]
    [DataRow(2, DisplayName = "Attachment_SubResult")]
    public async Task Attachment_UsesCorrectEndpointAndLiteralUtf8Payload(int scope)
    {
        using var fixture = new CiReporterFixture();
        using var handler = new Handler(_ => ReplyAsync(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);
        var attachment = AzureDevOpsTestResultAttachment.FromString("alpha\nβ😀", "console.log", "ConsoleLog", "owned comment");

        if (scope == 0)
        {
            await client.UploadTestRunAttachmentAsync(fixture.PublishConfiguration(), 73, attachment, CancellationToken.None);
        }
        else
        {
            await client.UploadTestResultAttachmentAsync(fixture.PublishConfiguration(), 73, 91, scope == 2 ? 104 : null, attachment, CancellationToken.None);
        }

        Request request = Assert.ContainsSingle(handler.Requests);
        Assert.AreEqual("POST", request.Method);
        Assert.AreEqual(
            scope switch
            {
                0 => "https://ci.invalid/org/project/_apis/test/runs/73/attachments?api-version=7.1",
                1 => "https://ci.invalid/org/project/_apis/test/runs/73/results/91/attachments?api-version=7.1",
                _ => "https://ci.invalid/org/project/_apis/test/runs/73/results/91/attachments?testSubResultId=104&api-version=7.1",
            }, request.Uri);
        using var payload = JsonDocument.Parse(request.Body);
        Assert.AreEqual("console.log", payload.RootElement.GetProperty("fileName").GetString());
        Assert.AreEqual("ConsoleLog", payload.RootElement.GetProperty("attachmentType").GetString());
        Assert.AreEqual("owned comment", payload.RootElement.GetProperty("comment").GetString());
        Assert.AreSequenceEqual(Encoding.UTF8.GetBytes("alpha\nβ😀"), Convert.FromBase64String(payload.RootElement.GetProperty("stream").GetString()!));
    }

    [TestMethod]
    [DataRow(401, DisplayName = "Request_Unauthorized")]
    [DataRow(302, DisplayName = "Request_RedirectedAuthentication")]
    [DataRow(0, DisplayName = "Request_OpaqueRedirect")]
    [DataRow(400, DisplayName = "Request_PermanentHttpFailure")]
    [DataRow(200, DisplayName = "Request_UnexpectedHtml")]
    public async Task Request_PermanentFailuresAreNotRetried(int status)
    {
        using var fixture = new CiReporterFixture();
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent("failure details", Encoding.UTF8, status == 200 ? "text/html" : "application/json"),
            };
            if (status == 0)
            {
                response.ReasonPhrase = "opaqueredirect";
            }

            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.CreateTestRunAsync(fixture.PublishConfiguration(), CancellationToken.None));

        string expected = status switch
        {
            0 => string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAuthenticationFailure, "opaqueredirect"),
            401 or 302 => string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAuthenticationFailure, status.ToString(CultureInfo.InvariantCulture)),
            200 => string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingUnexpectedContentType, 200, "text/html; charset=utf-8"),
            _ => string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingHttpError, status, "failure details"),
        };
        Assert.AreEqual(expected, error.Message);
        Assert.HasCount(1, handler.Requests);
        Assert.IsEmpty(fixture.Delays);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Request_TransientHttpException")]
    [DataRow(1, DisplayName = "Request_TransientIoException")]
    [DataRow(2, DisplayName = "Request_TransientSocketException")]
    [DataRow(3, DisplayName = "Request_InternalCancellation")]
    public async Task Request_TransientFailuresUseBoundedBackoffAndStablePayload(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.Delay = (_, _) => Task.CompletedTask;
        Exception original = failure switch
        {
            0 => new HttpRequestException("transport unavailable"),
            1 => new IOException("transport unavailable"),
            2 => new SocketException((int)SocketError.ConnectionReset),
            _ => new TaskCanceledException("internal attempt timeout"),
        };
        using var handler = new Handler(_ => Task.FromException<HttpResponseMessage>(original));
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);

        Exception error = await Assert.ThrowsAsync<Exception>(() => client.CreateTestRunAsync(fixture.PublishConfiguration(), CancellationToken.None));

        if (failure == 3)
        {
            Assert.IsInstanceOfType<InvalidOperationException>(error);
            Assert.AreEqual(AzureDevOpsResources.AzureDevOpsLivePublishingRequestFailed, error.Message);
            Assert.IsInstanceOfType<OperationCanceledException>(error.InnerException);
        }
        else
        {
            Assert.AreSame(original, error);
        }

        Assert.HasCount(3, handler.Requests);
        Assert.AreSequenceEqual([TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)], fixture.Delays);
        Assert.HasCount(1, handler.Requests.Select(request => request.Body).Distinct(StringComparer.Ordinal));
        using var payload = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.AreEqual("Owned run", payload.RootElement.GetProperty("name").GetString());
        Assert.AreEqual(123, payload.RootElement.GetProperty("build").GetProperty("id").GetInt32());
        Assert.AreEqual(CiReporterFixture.Epoch, payload.RootElement.GetProperty("startedDate").GetDateTimeOffset());
    }

    [TestMethod]
    [DataRow(0, 4000, DisplayName = "RetryAfter_PositiveDelta")]
    [DataRow(1, 3000, DisplayName = "RetryAfter_FutureDate")]
    [DataRow(2, 500, DisplayName = "RetryAfter_ExpiredDate")]
    [DataRow(3, 500, DisplayName = "RetryAfter_ZeroDelta")]
    [DataRow(4, 500, DisplayName = "RetryAfter_AbsentHeader")]
    public async Task Request_RetryAfterUsesClockOrExponentialFallback(int header, int expectedMilliseconds)
    {
        using var fixture = new CiReporterFixture();
        fixture.Delay = (_, _) => Task.CompletedTask;
        int calls = 0;
        using var handler = new Handler(_ =>
        {
            if (++calls > 1)
            {
                return ReplyAsync(HttpStatusCode.OK, "{\"id\":93}");
            }

            var response = new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("{}") };
            response.Headers.RetryAfter = header switch
            {
                0 => new RetryConditionHeaderValue(TimeSpan.FromSeconds(4)),
                1 => new RetryConditionHeaderValue(CiReporterFixture.Epoch.AddSeconds(3)),
                2 => new RetryConditionHeaderValue(CiReporterFixture.Epoch.AddSeconds(-1)),
                3 => new RetryConditionHeaderValue(TimeSpan.Zero),
                _ => null,
            };
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);

        int id = await client.CreateTestRunAsync(fixture.PublishConfiguration(), CancellationToken.None);

        Assert.AreEqual(93, id);
        Assert.HasCount(2, handler.Requests);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), Assert.ContainsSingle(fixture.Delays));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AcceptedHtml_NoLogger")]
    [DataRow(true, DisplayName = "AcceptedHtml_ThrowingLogger")]
    public async Task Publish_AcceptedHtmlAndFailingDiagnosticsNeverRetry(bool throwingLogger)
    {
        using var fixture = new CiReporterFixture();
        fixture.LogError = new IOException("diagnostic failed");
        using var handler = new Handler(_ => ReplyAsync(HttpStatusCode.OK, "<html>accepted</html>", "text/html"));
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object, throwingLogger ? fixture.Logger.Object : null);
        var result = new AzureDevOpsTestCaseResult("Tests.Accepted", "tests.dll", "accepted row", "Passed", 17, null, null, null, null);

        IReadOnlyList<AzureDevOpsPublishedTestResult>? response = await client.PublishTestResultsWithSubResultsAsync(fixture.PublishConfiguration(), 73, [result], CancellationToken.None);

        Assert.IsNull(response);
        Assert.HasCount(1, handler.Requests);
        Assert.IsEmpty(fixture.Delays);
        Assert.HasCount(throwingLogger ? 1 : 0, fixture.Logs);
        if (throwingLogger)
        {
            Assert.Contains("text/html", fixture.LogText);
        }
    }

    [TestMethod]
    public async Task Request_UserCancellationDoesNotBecomeInternalTimeout()
    {
        using var fixture = new CiReporterFixture();
        using var cancellation = new CancellationTokenSource();
        using var handler = new Handler(async token =>
        {
            cancellation.Cancel();
            return await Task.FromException<HttpResponseMessage>(new OperationCanceledException(token));
        });
        using var http = new HttpClient(handler);
        var client = new AzureDevOpsTestResultsClient(http, fixture.TaskService.Object, fixture.Clock.Object);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => client.CreateTestRunAsync(fixture.PublishConfiguration(), cancellation.Token));

        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsTrue(Assert.ContainsSingle(handler.Requests).Token.IsCancellationRequested);
        Assert.IsEmpty(fixture.Delays);
    }

    private static Task<HttpResponseMessage> ReplyAsync(HttpStatusCode status, string body, string mediaType = "application/json")
        => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });

    private sealed record Request(string Method, string Uri, string Body, string? AuthenticationScheme, string? Authentication, string Accept, CancellationToken Token);

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
#if NETCOREAPP
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
#else
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
#endif
            Requests.Add(new(request.Method.Method, request.RequestUri!.AbsoluteUri, body, request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter, request.Headers.Accept.ToString(), cancellationToken));
            return await send(cancellationToken);
        }
    }
}
