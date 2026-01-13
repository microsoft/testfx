// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device.Android;

/// <summary>
/// Collects test artifacts from Android devices.
/// </summary>
public sealed class AndroidArtifactCollector : IArtifactCollector
{
    private readonly AdbClient _adbClient;

    public AndroidArtifactCollector()
    {
        _adbClient = new AdbClient();
    }

    /// <inheritdoc/>
    public async Task<ArtifactCollection> CollectArtifactsAsync(
        DeviceInfo device,
        string appId,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var trxFiles = new List<string>();
        var coverageFiles = new List<string>();
        var logFiles = new List<string>();
        var otherFiles = new List<string>();

        // Android apps store files in /data/data/{package}/files/
        string appFilesPath = $"/data/data/{appId}/files/TestResults/";

        // List files in the test results directory
        string listArgs = $"-s {device.Id} shell ls -1 {appFilesPath}";
        AdbResult listResult = await _adbClient.ExecuteAsync(listArgs, cancellationToken);

        if (!listResult.Success)
        {
            return new ArtifactCollection(trxFiles, coverageFiles, logFiles, otherFiles);
        }

        string[] remoteFiles = listResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string remoteFile in remoteFiles)
        {
            string fileName = remoteFile.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            string remotePath = $"{appFilesPath}{fileName}";
            string localPath = Path.Combine(outputDirectory, fileName);

            // Pull file from device
            string pullArgs = $"-s {device.Id} pull {remotePath} \"{localPath}\"";
            AdbResult pullResult = await _adbClient.ExecuteAsync(pullArgs, cancellationToken);

            if (pullResult.Success)
            {
                // Categorize file by extension
                if (fileName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
                {
                    trxFiles.Add(localPath);
                }
                else if (fileName.EndsWith(".coverage", StringComparison.OrdinalIgnoreCase) ||
                         fileName.EndsWith(".cobertura.xml", StringComparison.OrdinalIgnoreCase))
                {
                    coverageFiles.Add(localPath);
                }
                else if (fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    logFiles.Add(localPath);
                }
                else
                {
                    otherFiles.Add(localPath);
                }
            }
        }

        return new ArtifactCollection(trxFiles, coverageFiles, logFiles, otherFiles);
    }
}
