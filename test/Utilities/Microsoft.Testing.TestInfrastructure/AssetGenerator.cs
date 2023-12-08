// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public class AssetGenerator : IDisposable
{
    private const string FileTag = "#file";

    private readonly TempDirectory _tempDirectory;
    private readonly string _assetCode;
    private bool _isDisposed;

    public AssetGenerator(string targetPath, string assetCode, bool cleanup = true)
    {
        _assetCode = assetCode;

        _tempDirectory = new(targetPath, cleanup: cleanup);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _tempDirectory.Dispose();
        }

        _isDisposed = true;
    }

    public string TargetAssetPath => _tempDirectory.DirectoryPath;

    private static (string Name, string Content) ParseFile(string fileContent)
    {
        int fileNameEndIndex = fileContent.IndexOf(Environment.NewLine, StringComparison.InvariantCulture);
        if (fileNameEndIndex < 0)
        {
            return (string.Empty, string.Empty);
        }

        string name = fileContent[..fileNameEndIndex].Trim();
        string content = fileContent.Remove(0, fileNameEndIndex).TrimStart('\r', '\n');
        return (name, content);
    }

    public async Task CreateAssetAsync()
    {
        string[] splitFiles = _assetCode.Split(new string[] { FileTag }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string fileContent in splitFiles)
        {
            (string, string) fileInfo = ParseFile(fileContent);
            await TempDirectory.WriteFileAsync(_tempDirectory.DirectoryPath, fileInfo.Item1, fileInfo.Item2);
        }
    }
}
