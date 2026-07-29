// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    public async Task AddArtifactsAsync(FileInfo trxFile, Dictionary<IExtension, List<SessionFileArtifact>> artifacts)
    {
        var document = XDocument.Load(trxFile.FullName);
        XElement testRun = document.Element(NamespaceUri + "TestRun")
            ?? throw new InvalidOperationException("TestRun element not found");
        XElement deployment = testRun.Element(NamespaceUri + "TestSettings")?.Element(NamespaceUri + "Deployment")
            ?? throw new InvalidOperationException("Deployment element not found");
        string runDeploymentRoot = deployment.Attribute("runDeploymentRoot")?.Value
            ?? throw new InvalidOperationException("Unexpected null 'runDeploymentRoot'");
        XElement resultSummary = testRun.Element(NamespaceUri + "ResultSummary")
            ?? throw new InvalidOperationException("ResultSummary element not found");
        XElement? collectorDataEntries = resultSummary.Element(NamespaceUri + "CollectorDataEntries");
        if (collectorDataEntries is null)
        {
            collectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");
            resultSummary.Add(collectorDataEntries);
        }

        var attachmentWarnings = new List<string>();
        AddArtifactsToCollection(artifacts, collectorDataEntries, runDeploymentRoot, attachmentWarnings);
        XElement? runInfos = resultSummary.Element(NamespaceUri + "RunInfos");
        foreach (string attachmentWarning in attachmentWarnings)
        {
            AddRunInfo(resultSummary, ref runInfos, "Warning", attachmentWarning);
        }

        AttachmentWarnings = attachmentWarnings;

        // FileMode.Create rather than the File.OpenWrite equivalent (OpenOrCreate): this is a full
        // rewrite of the document, and OpenOrCreate would leave trailing bytes from the previous
        // contents behind if the new document were ever shorter.
        // Note that we need to dispose the IFileStream, not the inner stream.
        using IFileStream fs = _fileSystem.NewFileStream(trxFile.FullName, FileMode.Create, FileAccess.Write);
#if NETCOREAPP
        await document.SaveAsync(fs.Stream, SaveOptions.None, _cancellationToken).ConfigureAwait(false);
#else
        document.Save(fs.Stream, SaveOptions.None);
#endif
    }

    private void AddArtifactsToCollection(Dictionary<IExtension, List<SessionFileArtifact>> artifacts, XElement collectorDataEntries, string runDeploymentRoot, List<string> attachmentWarnings)
    {
        foreach (KeyValuePair<IExtension, List<SessionFileArtifact>> extensionArtifacts in artifacts)
        {
            var collector = new XElement(
                NamespaceUri + "Collector",
                new XAttribute("agentName", _environment.MachineName),
                new XAttribute("uri", $"datacollector://{extensionArtifacts.Key.Uid}/{extensionArtifacts.Key.Version}"),
                new XAttribute("collectorDisplayName", extensionArtifacts.Key.DisplayName));
            collectorDataEntries.Add(collector);

            var uriAttachments = new XElement(NamespaceUri + "UriAttachments");
            collector.Add(uriAttachments);

            foreach (SessionFileArtifact artifact in extensionArtifacts.Value)
            {
                if (!TryCopyArtifactAndGetHref(artifact.FileInfo, runDeploymentRoot, null, attachmentWarnings, out string? href))
                {
                    continue;
                }

                uriAttachments.Add(new XElement(NamespaceUri + "UriAttachment", new XElement(NamespaceUri + "A", new XAttribute("href", href!))));
            }
        }
    }

    private bool TryCopyArtifactAndGetHref(FileInfo artifact, string runDeploymentRoot, string? relativeResultsDirectory, List<string> attachmentWarnings, out string? href)
    {
        try
        {
            href = CopyArtifactIntoTrxDirectoryAndReturnHrefValue(artifact, runDeploymentRoot, relativeResultsDirectory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddAttachmentWarning(artifact, ex, attachmentWarnings);
        }

        href = null;
        return false;
    }

    private static void AddAttachmentWarning(FileInfo artifact, Exception exception, List<string> attachmentWarnings)
        => attachmentWarnings.Add(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxAttachmentCopyFailed, artifact.FullName, exception.GetType().Name));

    private string CopyArtifactIntoTrxDirectoryAndReturnHrefValue(FileInfo artifact, string runDeploymentRoot, string? relativeResultsDirectory = null)
    {
        string artifactDirectory = CreateOrGetTrxArtifactDirectory(runDeploymentRoot, relativeResultsDirectory);
        string fileName = artifact.Name;

        string destination = Path.Combine(artifactDirectory, fileName);
        int nameCounter = 0;

        // If the file already exists, append a number to the end of the file name
        while (_fileSystem.ExistFile(TrxLongPathHelper.GetPathForFileSystemAccess(destination)))
        {
            nameCounter++;
            destination = Path.Combine(artifactDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{nameCounter}{Path.GetExtension(fileName)}");
        }

        _fileSystem.CopyFile(
            TrxLongPathHelper.GetPathForFileSystemAccess(artifact.FullName),
            TrxLongPathHelper.GetPathForFileSystemAccess(destination));

        return Path.Combine(_environment.MachineName, Path.GetFileName(destination));
    }

    private string CreateOrGetTrxArtifactDirectory(string runDeploymentRoot, string? relativeResultsDirectory = null)
    {
        string directoryName = relativeResultsDirectory is null
            ? Path.Combine(_configuration.GetTestResultDirectory(), runDeploymentRoot, "In", _environment.MachineName)
            : Path.Combine(_configuration.GetTestResultDirectory(), runDeploymentRoot, "In", relativeResultsDirectory, _environment.MachineName);

        // Only the file-system calls get the extended-length form; the returned value stays in its
        // display form because it is the base of the path recorded in the TRX.
        string directoryNameForFileSystemAccess = TrxLongPathHelper.GetPathForFileSystemAccess(directoryName);
        if (!_fileSystem.ExistDirectory(directoryNameForFileSystemAccess))
        {
            _fileSystem.CreateDirectory(directoryNameForFileSystemAccess);
        }

        return directoryName;
    }
}
