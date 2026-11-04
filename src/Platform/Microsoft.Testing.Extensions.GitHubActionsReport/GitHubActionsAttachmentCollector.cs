// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed class GitHubActionsAttachmentCollector(
    string rootDirectory,
    IFileSystem fileSystem,
    Func<string, Task> warn)
{
    internal const long MaxFileBytes = 25L * 1024 * 1024;
    internal const long MaxTotalBytes = 250L * 1024 * 1024;
    internal const int MaxFilesPerTest = 10;
    internal const int MaxFilesPerModule = 1000;

    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);
    private readonly string _moduleDirectory = Guid.NewGuid().ToString("N");
    private readonly List<(string Uid, string Name, string DisplayName, List<CiRunSummaryAttachment> Attachments)> _tests = [];
    private long _totalBytes;
    private int _fileCount;
    private int _testOrdinal;
    private bool _limitReported;

    internal async Task<CiRunSummaryAttachment[]?> CaptureAsync(TestNode testNode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileArtifactProperty[] files = [.. testNode.Properties.OfType<FileArtifactProperty>().Take(MaxFilesPerTest + 1)];
        if (files.Length == 0)
        {
            return null;
        }

        if (_fileCount >= MaxFilesPerModule)
        {
            if (!_limitReported)
            {
                _limitReported = true;
                await warn(GitHubActionsResources.AttachmentCountLimitWarning).ConfigureAwait(false);
            }

            return null;
        }

        string testDirectory = (++_testOrdinal).ToString(CultureInfo.InvariantCulture);
        var attachments = new List<CiRunSummaryAttachment>();
        // Keep associations even if warning delivery fails after an earlier file was copied.
        _tests.Add((testNode.Uid, TestNodeIdentity.GetTestName(testNode), testNode.DisplayName, attachments));
        foreach (FileArtifactProperty file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_fileCount >= MaxFilesPerModule)
            {
                _limitReported = true;
                await warn(GitHubActionsResources.AttachmentCountLimitWarning).ConfigureAwait(false);
                break;
            }

            _fileCount++;
            var attachment = new CiRunSummaryAttachment
            {
                FileName = SanitizeFileName(file.FileInfo.Name),
                Status = "unavailable",
            };
            attachments.Add(attachment);
            if (attachments.Count > MaxFilesPerTest)
            {
                attachment.Status = "count-limit";
                await WarnAsync(attachment).ConfigureAwait(false);
                break;
            }

            string relativePath = $"{_moduleDirectory}/{testDirectory}/{attachments.Count.ToString(CultureInfo.InvariantCulture)}-{attachment.FileName}";
            string destination = Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            bool destinationCreated = false;
            try
            {
                if (!fileSystem.IsRegularFile(file.FileInfo.FullName))
                {
                    attachment.Status = "non-regular";
                    await WarnAsync(attachment).ConfigureAwait(false);
                    continue;
                }

                using IFileStream? source = fileSystem.OpenReadRegularFile(file.FileInfo.FullName);
                if (source is null)
                {
                    attachment.Status = "non-regular";
                    await WarnAsync(attachment).ConfigureAwait(false);
                    continue;
                }

                long length = source.Stream.Length;
                if (length > MaxFileBytes || length > MaxTotalBytes - _totalBytes)
                {
                    attachment.Status = "size-limit";
                    await WarnAsync(attachment).ConfigureAwait(false);
                    continue;
                }

                fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
                long copied = 0;
                using (IFileStream target = fileSystem.NewFileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    destinationCreated = true;
                    byte[] buffer = new byte[81920];
                    int count;
                    while ((count = await source.Stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        copied += count;
                        if (copied > MaxFileBytes || copied > MaxTotalBytes - _totalBytes)
                        {
                            attachment.Status = "size-limit";
                            throw new IOException(GitHubActionsResources.AttachmentSizeChanged);
                        }

                        await target.Stream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                _totalBytes += copied;
                attachment.SizeBytes = copied;
                attachment.RelativePath = relativePath;
                attachment.Status = "staged";
                destinationCreated = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
            {
                await WarnAsync(attachment).ConfigureAwait(false);
            }
            finally
            {
                if (destinationCreated)
                {
                    try
                    {
                        fileSystem.DeleteFile(destination);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
                    {
                        await warn(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AttachmentCleanupWarning, attachment.FileName, ex.Message)).ConfigureAwait(false);
                    }
                }
            }
        }

        return [.. attachments];
    }

    internal async Task WriteManifestAsync(CiRunSummaryModule module, CancellationToken cancellationToken)
    {
        if (_tests.Count == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.Combine(_rootDirectory, _moduleDirectory);
        string manifestPath = Path.Combine(directory, "attachments.json");
        string temporaryPath = manifestPath + ".tmp";
        try
        {
            fileSystem.CreateDirectory(directory);
            using (IFileStream file = fileSystem.NewFileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new Utf8JsonWriter(file.Stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", 1);
                writer.WriteString("assemblyName", module.AssemblyName);
                writer.WriteString("targetFramework", module.TargetFramework);
                writer.WriteString("architecture", module.Architecture);
                writer.WriteString("sessionUid", module.SessionUid);
                writer.WriteNumber("attemptNumber", module.AttemptNumber);
                writer.WriteBoolean("truncated", _limitReported);
                writer.WriteStartArray("tests");
                foreach ((string uid, string name, string displayName, List<CiRunSummaryAttachment> attachments) in _tests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteStartObject();
                    writer.WriteString("uid", uid);
                    writer.WriteString("fullyQualifiedName", name);
                    writer.WriteString("displayName", displayName);
                    writer.WriteStartArray("attachments");
                    foreach (CiRunSummaryAttachment attachment in attachments)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("fileName", attachment.FileName);
                        writer.WriteString("relativePath", attachment.RelativePath);
                        writer.WriteNumber("sizeBytes", attachment.SizeBytes);
                        writer.WriteString("status", attachment.Status);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            fileSystem.MoveFile(temporaryPath, manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await warn(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AttachmentManifestWarning, ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            if (fileSystem.ExistFile(temporaryPath))
            {
                try
                {
                    fileSystem.DeleteFile(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await warn(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AttachmentManifestWarning, ex.Message)).ConfigureAwait(false);
                }
            }
        }
    }

    private Task WarnAsync(CiRunSummaryAttachment attachment)
        => warn(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.AttachmentSkippedWarning, attachment.FileName, attachment.Status));

    private static string SanitizeFileName(string name)
    {
        string shortened = name.Length > 80 ? name.Substring(name.Length - 80) : name;
        char[] characters = shortened.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            char character = characters[i];
            if (character is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_'))
            {
                characters[i] = '_';
            }
        }

        string sanitized = new string(characters).TrimEnd('.');
        return sanitized.Length == 0 ? "attachment" : sanitized;
    }
}
