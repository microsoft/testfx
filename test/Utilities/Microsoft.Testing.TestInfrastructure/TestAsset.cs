// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public class TestAsset : IDisposable
{
    private const string FileTag = "#file";

    private readonly TempDirectory _tempDirectory;
    private readonly string _assetCode;
    private bool _isDisposed;

    public TestAsset(string targetPath, string assetCode, bool cleanup = true)
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

    public static async Task<TestAsset> GenerateAssetAsync(string assetName, string code, bool addDefaultNugetConfigFile = true)
    {
        string defaultNuGetConfig = $"""

#file NuGet.config

<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear/>
        <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
        <add key="dotnet-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
        <add key="dotnet-eng" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json" />
        <add key="vs-buildservices" value="https://pkgs.dev.azure.com/azure-public/vside/_packaging/vs-buildservices/nuget/v3/index.json" />
        <add key="dotnet7" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7/nuget/v3/index.json" />
        <add key="dotnet8" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json" />
        <add key="dotnet9" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json" />
        <add key="local-nonshipping" value="{Constants.ArtifactsPackagesNonShipping}" />
        <add key="local-shipping" value="{Constants.ArtifactsPackagesShipping}" />
        <add key="local-test-packages" value="{Constants.ArtifactsTmpPackages}" />
    </packageSources>
    <config>
        <add key="globalPackagesFolder" value=".packages" />
    </config>
</configuration>

""";
        var testAsset = new TestAsset(assetName, addDefaultNugetConfigFile ? string.Concat(code, defaultNuGetConfig) : code);
        string[] splitFiles = testAsset._assetCode.Split(new string[] { FileTag }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string fileContent in splitFiles)
        {
            (string, string) fileInfo = ParseFile(fileContent);
            await TempDirectory.WriteFileAsync(testAsset._tempDirectory.DirectoryPath, fileInfo.Item1, fileInfo.Item2);
        }

        return testAsset;
    }
}
