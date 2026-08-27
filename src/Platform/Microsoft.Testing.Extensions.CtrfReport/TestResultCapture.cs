// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.CtrfReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    private const string DefaultAttachmentContentType = "application/octet-stream";

    public static CapturedTestResult? TryCapture(TestNode node)
    {
        CapturedTestResultCoreData? coreData = TestResultCaptureHelper.TryCaptureCore(node, includeLocation: true);
        if (!coreData.HasValue)
        {
            return null;
        }

        CapturedTestResultCoreData core = coreData.GetValueOrDefault();
        (string status, string? rawStatus) = ClassifyStatus(core.State);
        (string? ns, string? className, _) = GetClassAndMethodName(core.Properties.Identifier);
        RetryAttemptProperty? retryAttempt = node.Properties.SingleOrDefault<RetryAttemptProperty>();

        var result = new CapturedTestResult
        {
            Status = status,
            RawStatus = rawStatus,
            Namespace = TestResultCaptureHelper.Truncate(ns, TestResultCaptureHelper.MaxIdentityFieldLength),
            FilePath = TestResultCaptureHelper.Truncate(core.Properties.Location?.FilePath, TestResultCaptureHelper.MaxIdentityFieldLength),
            Line = core.Properties.Location?.LineSpan.Start.Line,
            Attachments = CaptureAttachments(node.Properties),
            RetryAttemptNumber = retryAttempt?.AttemptNumber,
            IsSupersededRetryAttempt = retryAttempt?.IsSuperseded == true,
        };
        result.PopulateBaseFields(node, core);

        // CTRF decomposes the identifier into a separate Namespace plus a type-name-only
        // ClassName, rather than the namespace-prefixed ClassName the shared base-field
        // population produces, so override ClassName here (MethodName already matches the
        // shared value).
        result.ClassName = TestResultCaptureHelper.Truncate(className, TestResultCaptureHelper.MaxIdentityFieldLength);
        return result;
    }

    // CTRF status enum: passed, failed, skipped, pending, other.
    // Returns the CTRF status plus an optional `rawStatus` preserving the original
    // MTP outcome when it doesn't map 1:1 (timedOut, errored, cancelled).
    private static (string Status, string? RawStatus) ClassifyStatus(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => ("passed", null),
            SkippedTestNodeStateProperty => ("skipped", null),
            TimeoutTestNodeStateProperty => ("failed", "timedOut"),
            ErrorTestNodeStateProperty => ("failed", "errored"),
            FailedTestNodeStateProperty => ("failed", null),
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            CancelledTestNodeStateProperty => ("other", "cancelled"),
#pragma warning restore CS0618, MTP0001
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    private static (string? Namespace, string? ClassName, string? MethodName) GetClassAndMethodName(TestMethodIdentifierProperty? identifier)
    {
        if (identifier is null)
        {
            return (null, null, null);
        }

        string? ns = RoslynString.IsNullOrEmpty(identifier.Namespace) ? null : identifier.Namespace;
        return (ns, identifier.TypeName, identifier.MethodName);
    }

    private static IReadOnlyList<CapturedAttachment>? CaptureAttachments(
        PropertyBag properties,
        Func<FileInfo, string>? resolveFullPath = null)
    {
        resolveFullPath ??= static fileInfo => fileInfo.FullName;
        List<CapturedAttachment>? attachments = null;
        PropertyBag.PropertyBagEnumerator enumerator = properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is not FileArtifactProperty artifact)
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = resolveFullPath(artifact.FileInfo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
            {
                continue;
            }

            string name = RoslynString.IsNullOrEmpty(artifact.DisplayName)
                ? Path.GetFileName(fullPath)
                : artifact.DisplayName;
            attachments ??= [];
            attachments.Add(new CapturedAttachment
            {
                Name = TestResultCaptureHelper.Truncate(name, TestResultCaptureHelper.MaxIdentityFieldLength)!,
                ContentType = GetAttachmentContentType(fullPath),
                Path = TestResultCaptureHelper.Truncate(fullPath, TestResultCaptureHelper.MaxIdentityFieldLength)!,
                Description = TestResultCaptureHelper.Truncate(artifact.Description, TestResultCaptureHelper.MaxMessageLength),
            });
        }

        return attachments;
    }

    private static string GetAttachmentContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bmp" => "image/bmp",
            ".csv" => "text/csv",
            ".gif" => "image/gif",
            ".htm" or ".html" => "text/html",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".json" => "application/json",
            ".log" or ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".tif" or ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => DefaultAttachmentContentType,
        };
}
