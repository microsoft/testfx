// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http.Headers;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsClient
{
    /// <summary>
    /// Reads the response body and parses it into published results, swallowing parse/transport failures
    /// that must not cause the caller to retry (the server has already accepted the write).
    /// </summary>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    private static async Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> TryReadAndParsePublishedResultsAsync(
        HttpResponseMessage response,
        CancellationToken readTimeoutToken,
        CancellationToken userCancellationToken,
        IReadOnlyList<AzureDevOpsTestCaseResult> results,
        bool validateAutomatedTestName)
    {
        try
        {
            string payload = await ReadAsStringAsync(response.Content, readTimeoutToken).ConfigureAwait(false);
            return ParsePublishedResults(payload, results, validateAutomatedTestName);
        }
        catch (OperationCanceledException) when (!userCancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    private static IReadOnlyList<AzureDevOpsPublishedTestResult>? ParsePublishedResults(
        string payload,
        IReadOnlyList<AzureDevOpsTestCaseResult> submittedResults,
        bool validateAutomatedTestName)
    {
        PublishTestResultsResponse? parsed = JsonSerializer.Deserialize<PublishTestResultsResponse>(payload, JsonSerializerOptions);
        if (parsed?.Value is null || parsed.Value.Length != submittedResults.Count)
        {
            return null;
        }

        var publishedResults = new AzureDevOpsPublishedTestResult[submittedResults.Count];
        for (int i = 0; i < submittedResults.Count; i++)
        {
            PublishedTestResult published = parsed.Value[i];
            AzureDevOpsTestCaseResult submitted = submittedResults[i];
            if (published.Id <= 0
                || (validateAutomatedTestName
                    ? !string.Equals(published.AutomatedTestName, submitted.AutomatedTestName, StringComparison.Ordinal)
                    : submitted.Id != published.Id))
            {
                return null;
            }

            IReadOnlyList<AzureDevOpsTestSubResult>? submittedSubResults = submitted.SubResults;
            Dictionary<int, int> subResultIdsBySequenceId = [];
            if (submittedSubResults is { Count: > 0 }
                && published.SubResults is { } publishedSubResults)
            {
                var submittedSequenceIds = new HashSet<int>();
                for (int j = 0; j < submittedSubResults.Count; j++)
                {
                    if (!submittedSequenceIds.Add(submittedSubResults[j].SequenceId))
                    {
                        break;
                    }
                }

                if (submittedSequenceIds.Count == submittedSubResults.Count)
                {
                    bool sequenceIdsOmitted = publishedSubResults.Length == submittedSubResults.Count;
                    for (int j = 0; sequenceIdsOmitted && j < publishedSubResults.Length; j++)
                    {
                        sequenceIdsOmitted = publishedSubResults[j].SequenceId == 0;
                    }

                    if (sequenceIdsOmitted)
                    {
                        // Azure DevOps' PATCH response returns only the appended sub-results, in request
                        // order, but omits sequenceId. Correlate by position only for that exact-count shape.
                        for (int j = 0; j < publishedSubResults.Length; j++)
                        {
                            if (publishedSubResults[j].Id <= 0)
                            {
                                subResultIdsBySequenceId.Clear();
                                break;
                            }

                            subResultIdsBySequenceId.Add(submittedSubResults[j].SequenceId, publishedSubResults[j].Id);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < publishedSubResults.Length; j++)
                        {
                            PublishedTestSubResult publishedSubResult = publishedSubResults[j];
                            if (!submittedSequenceIds.Contains(publishedSubResult.SequenceId))
                            {
                                continue;
                            }

                            if (publishedSubResult.Id <= 0
                                || subResultIdsBySequenceId.ContainsKey(publishedSubResult.SequenceId))
                            {
                                subResultIdsBySequenceId.Clear();
                                break;
                            }

                            subResultIdsBySequenceId.Add(publishedSubResult.SequenceId, publishedSubResult.Id);
                        }
                    }

                    if (subResultIdsBySequenceId.Count != submittedSubResults.Count)
                    {
                        subResultIdsBySequenceId.Clear();
                    }
                }
            }

            publishedResults[i] = new AzureDevOpsPublishedTestResult(published.Id, subResultIdsBySequenceId);
        }

        return publishedResults;
    }

    private static AttachmentRequest? TryBuildAttachmentRequest(AzureDevOpsTestResultAttachment attachment)
    {
        byte[]? bytes;
        if (attachment.FilePath is { Length: > 0 } filePath)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return null;
                }

                if (fileInfo.Length > AzureDevOpsLivePublishingConstants.MaxAttachmentSizeBytes)
                {
                    return null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
            {
                return null;
            }

            try
            {
                bytes = File.ReadAllBytes(filePath);
                if (bytes.Length > AzureDevOpsLivePublishingConstants.MaxAttachmentSizeBytes)
                {
                    return null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
            {
                return null;
            }
        }
        else if (attachment.InlineContent is { } inline)
        {
            // Inline content (stdout/stderr) is already truncated by the publisher to MaxInlineAttachmentBytes.
            bytes = Encoding.UTF8.GetBytes(inline);
        }
        else
        {
            return null;
        }

        return new AttachmentRequest(
            Convert.ToBase64String(bytes),
            attachment.FileName,
            attachment.Comment,
            attachment.AttachmentType);
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Payload types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Payload types are internal, fixed, and controlled by this extension.")]
    private static HttpRequestMessage CreateRequest<TPayload>(
        HttpMethod method,
        Uri uri,
        string accessToken,
        TPayload payload,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        HttpRequestMessage request = CreateRequest(method, uri, accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, jsonSerializerOptions ?? JsonSerializerOptions),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string accessToken)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}")));
        request.Headers.Accept.ParseAdd($"application/json; api-version={ApiVersion}");
        return request;
    }

    private sealed record CreateTestRunRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("automated")] bool Automated,
        [property: JsonPropertyName("build")] BuildReference Build,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("startedDate")] DateTimeOffset StartedDate,
        [property: JsonPropertyName("pipelineReference")] PipelineReference? PipelineReference);

    private sealed record BuildReference([property: JsonPropertyName("id")] int Id);

    private sealed record PipelineReference(
        [property: JsonPropertyName("pipelineId")] int PipelineId,
        [property: JsonPropertyName("stageReference")] StageReference? StageReference,
        [property: JsonPropertyName("phaseReference")] PhaseReference? PhaseReference,
        [property: JsonPropertyName("jobReference")] JobReference? JobReference);

    private sealed record StageReference(
        [property: JsonPropertyName("stageName")] string StageName,
        [property: JsonPropertyName("attempt")] int? Attempt);

    private sealed record PhaseReference(
        [property: JsonPropertyName("phaseName")] string PhaseName,
        [property: JsonPropertyName("attempt")] int? Attempt);

    private sealed record JobReference(
        [property: JsonPropertyName("jobName")] string JobName,
        [property: JsonPropertyName("attempt")] int? Attempt);

    private sealed record CreateTestRunResponse([property: JsonPropertyName("id")] int Id);

    private sealed record UpdateTestRunStateRequest([property: JsonPropertyName("state")] string State);

    private sealed record PublishTestResultsResponse(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("value")] PublishedTestResult[]? Value);

    private sealed record PublishedTestResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("automatedTestName")] string? AutomatedTestName,
        [property: JsonPropertyName("subResults")] PublishedTestSubResult[]? SubResults);

    private sealed record PublishedTestSubResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("sequenceId")] int SequenceId);

    private sealed record AttachmentRequest(
        [property: JsonPropertyName("stream")] string Stream,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("comment")] string? Comment,
        [property: JsonPropertyName("attachmentType")] string AttachmentType);
}
