// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsClient
{
    private static Uri BuildRunsUri(string collectionUri, string project)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs?api-version={ApiVersion}");

    private static Uri BuildRunUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}?api-version={ApiVersion}");

    private static Uri BuildResultsUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results?api-version={ApiVersion}");

    private static Uri BuildResultAttachmentsUri(string collectionUri, string project, int runId, int testCaseResultId, int? testSubResultId)
    {
        string query = testSubResultId is null
            ? $"api-version={ApiVersion}"
            : $"testSubResultId={testSubResultId.Value.ToString(CultureInfo.InvariantCulture)}&api-version={ApiVersion}";
        return new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results/{testCaseResultId}/attachments?{query}");
    }

    private static Uri BuildRunAttachmentsUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/attachments?api-version={ApiVersion}");
}
