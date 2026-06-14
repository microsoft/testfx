// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    internal static AzureDevOpsTestCaseResultWithAttachments? CreateTestCaseResult(TestNode testNode, string automatedTestStorage)
    {
        TestNodeStateProperty? state = testNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        TimingProperty? timing = testNode.Properties.SingleOrDefault<TimingProperty>();
        string automatedTestName = testNode.Uid.Value;

        AzureDevOpsTestCaseResult? result = state switch
        {
            PassedTestNodeStateProperty passed => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.PassedTestOutcome, passed.Explanation, null, timing),
            FailedTestNodeStateProperty failed => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, failed.Exception?.Message ?? failed.Explanation, failed.Exception?.StackTrace, timing),
            ErrorTestNodeStateProperty error => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, error.Exception?.Message ?? error.Explanation, error.Exception?.StackTrace, timing),
            SkippedTestNodeStateProperty skipped => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.NotExecutedTestOutcome, skipped.Explanation, null, timing),
            TimeoutTestNodeStateProperty timeout => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, BuildTimeoutMessage(timeout), timeout.Exception?.StackTrace, timing),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            CancelledTestNodeStateProperty cancelled => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.AbortedTestOutcome, cancelled.Exception?.Message ?? cancelled.Explanation, cancelled.Exception?.StackTrace, timing),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            _ => null,
        };

        if (result is null)
        {
            return null;
        }

        // Only attach artifacts for non-passing outcomes to avoid uploading large dumps/logs for every
        // passing test. Users who want pass-time artifacts can use the pipeline-level
        // `--report-azdo-upload-artifacts files` option instead.
        bool isFailure = state is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            ;

        IReadOnlyList<AzureDevOpsTestResultAttachment> attachments = isFailure
            ? BuildAttachmentsFromTestNode(testNode)
            : [];

        return new AzureDevOpsTestCaseResultWithAttachments(result, attachments);
    }

    private static IReadOnlyList<AzureDevOpsTestResultAttachment> BuildAttachmentsFromTestNode(TestNode testNode)
    {
        List<AzureDevOpsTestResultAttachment>? attachments = null;
        StandardOutputProperty? stdout = null;
        StandardErrorProperty? stderr = null;

        // Single-pass collection: replaces 1 × OfType<FileArtifactProperty>() loop + 2 × SingleOrDefault<T>()
        // with one GetStructEnumerator() walk, saving 2 linked-list traversals + 1 LINQ allocation per failure.
        PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case FileArtifactProperty fileArtifact:
                    string? fullPath;
                    try
                    {
                        fullPath = fileArtifact.FileInfo.FullName;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
                    {
                        break;
                    }

                    attachments ??= [];
                    attachments.Add(AzureDevOpsTestResultAttachment.FromFile(
                        fullPath,
                        AzureDevOpsAttachmentTypes.GeneralAttachment,
                        comment: fileArtifact.Description ?? fileArtifact.DisplayName));
                    break;
                case StandardOutputProperty so:
                    stdout = so;
                    break;
                case StandardErrorProperty se:
                    stderr = se;
                    break;
            }
        }

        if (stdout is not null && !RoslynString.IsNullOrEmpty(stdout.StandardOutput))
        {
            attachments ??= [];
            attachments.Add(AzureDevOpsTestResultAttachment.FromString(
                TruncateInline(stdout.StandardOutput),
                "stdout.log",
                AzureDevOpsAttachmentTypes.ConsoleLog));
        }

        if (stderr is not null && !RoslynString.IsNullOrEmpty(stderr.StandardError))
        {
            attachments ??= [];
            attachments.Add(AzureDevOpsTestResultAttachment.FromString(
                TruncateInline(stderr.StandardError),
                "stderr.log",
                AzureDevOpsAttachmentTypes.GeneralAttachment));
        }

        return (IReadOnlyList<AzureDevOpsTestResultAttachment>?)attachments ?? [];
    }

    private static string TruncateInline(string content)
    {
        int maxBytes = AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes;
        if (Encoding.UTF8.GetByteCount(content) <= maxBytes)
        {
            return content;
        }

        // UTF-8 can use up to 4 bytes per char; reserve room for the truncation marker.
        const string Marker = "\n...[truncated]";
        int markerBytes = Encoding.UTF8.GetByteCount(Marker);
        int budget = maxBytes - markerBytes;
        if (budget <= 0)
        {
            return Marker;
        }

        int byteCount = 0;
        int charCount = 0;
        while (charCount < content.Length)
        {
            int charBytes = GetUtf8ByteCount(content, charCount, out int charsConsumed);
            if (byteCount + charBytes > budget)
            {
                break;
            }

            byteCount += charBytes;
            charCount += charsConsumed;
        }

        if (charCount > 0 && char.IsHighSurrogate(content[charCount - 1]))
        {
            charCount--;
        }

        return content[..charCount] + Marker;
    }

    private static int GetUtf8ByteCount(string content, int index, out int charsConsumed)
    {
        char ch = content[index];
        charsConsumed = 1;
        if (ch < 0x80)
        {
            return 1;
        }

        if (ch < 0x800)
        {
            return 2;
        }

        if (char.IsHighSurrogate(ch) && index + 1 < content.Length && char.IsLowSurrogate(content[index + 1]))
        {
            charsConsumed = 2;
            return 4;
        }

        return 3;
    }

    private static AzureDevOpsTestResultAttachment? TryCreateRunAttachment(SessionFileArtifact sessionFileArtifact)
    {
        FileInfo fileInfo = sessionFileArtifact.FileInfo;
        string name;
        string fullName;
        try
        {
            name = fileInfo.Name;
            fullName = fileInfo.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
        {
            return null;
        }

        if (RoslynString.IsNullOrEmpty(name))
        {
            return null;
        }

        bool isCoverage = name.EndsWith(".coverage", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".cobertura.xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".opencover.xml", StringComparison.OrdinalIgnoreCase);

        return !isCoverage
            ? null
            : AzureDevOpsTestResultAttachment.FromFile(
                fullName,
                AzureDevOpsAttachmentTypes.CodeCoverage,
                comment: sessionFileArtifact.Description ?? sessionFileArtifact.DisplayName);
    }

    private static AzureDevOpsTestCaseResult CreateResult(
        string displayName,
        string automatedTestName,
        string automatedTestStorage,
        string outcome,
        string? errorMessage,
        string? stackTrace,
        TimingProperty? timing)
        => new(
            automatedTestName,
            automatedTestStorage,
            displayName,
            outcome,
            timing is null ? null : (long)Math.Round(timing.GlobalTiming.Duration.TotalMilliseconds, MidpointRounding.AwayFromZero),
            errorMessage,
            stackTrace,
            timing?.GlobalTiming.StartTime,
            timing?.GlobalTiming.EndTime);

    private static string BuildTimeoutMessage(TimeoutTestNodeStateProperty timeout)
    {
        string? reason = timeout.Explanation ?? timeout.Exception?.Message;
        return RoslynString.IsNullOrWhiteSpace(reason)
            ? AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessage
            : string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessageWithReason, reason);
    }
}
