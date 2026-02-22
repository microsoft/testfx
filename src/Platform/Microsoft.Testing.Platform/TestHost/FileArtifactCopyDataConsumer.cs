// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Copies per-test file artifacts to the test results directory so that the
/// results directory is self-contained.
/// </summary>
internal sealed class FileArtifactCopyDataConsumer : IDataConsumer
{
    private readonly IConfiguration _configuration;

    public FileArtifactCopyDataConsumer(IConfiguration configuration)
        => _configuration = configuration;

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(FileArtifactCopyDataConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => PlatformResources.FileArtifactCopyDataConsumerDisplayName;

    public string Description => PlatformResources.FileArtifactCopyDataConsumerDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is not TestNodeUpdateMessage message)
        {
            return Task.CompletedTask;
        }

        FileArtifactProperty[] artifacts = message.TestNode.Properties.OfType<FileArtifactProperty>();
        if (artifacts.Length == 0)
        {
            return Task.CompletedTask;
        }

        string resultsDirectory = _configuration.GetTestResultDirectory();

        foreach (FileArtifactProperty artifact in artifacts)
        {
            CopyFileToResultsDirectory(artifact.FileInfo, resultsDirectory);
        }

        return Task.CompletedTask;
    }

    private static void CopyFileToResultsDirectory(FileInfo file, string resultsDirectory)
    {
        // If the file is already under the results directory, skip.
        string fullResultsDirectory = Path.GetFullPath(resultsDirectory);
        if (file.FullName.StartsWith(fullResultsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(resultsDirectory))
        {
            Directory.CreateDirectory(resultsDirectory);
        }

        string destination = Path.Combine(resultsDirectory, file.Name);
        for (int nameCounter = 1; File.Exists(destination) && nameCounter <= 10; nameCounter++)
        {
            destination = Path.Combine(resultsDirectory, $"{Path.GetFileNameWithoutExtension(file.Name)}_{nameCounter}{Path.GetExtension(file.Name)}");
        }

        File.Copy(file.FullName, destination, overwrite: false);
    }
}
