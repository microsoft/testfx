// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public abstract class TestBase
{
    protected TestBase(ITestExecutionContext testExecutionContext)
    {
        // TestsRunWatchDog.AddTestRun(testExecutionContext.TestInfo.StableUid);
    }

    public static TestArgumentsEntry<string>[] NET_Tfms { get; } = new TestArgumentsEntry<string>[] { new("net8.0", "net8.0"), new("net7.0", "net7.0"), new("net6.0", "net6.0") };

    public static TestArgumentsEntry<string> MainNET_Tfm { get; } = NET_Tfms[0];

    public static TestArgumentsEntry<string>[] NETFramework_Tfms { get; } = new TestArgumentsEntry<string>[] { new("net462", "net462") };

    public static TestArgumentsEntry<string> MainNETFramework_Tfm => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? NETFramework_Tfms[0] : throw new InvalidOperationException(".NET Framework supported only in Windows");

    public static TestArgumentsEntry<string>[] All_Tfms => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? NET_Tfms.Concat(NETFramework_Tfms).ToArray() : NET_Tfms;

    public static async Task<AssetGenerator> CreateAssetAsync(string assetName, string code, bool addDefaultNugetConfigFile = true)
    {
        string defaultNugetConfig = $"""

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
        <add key="local-nonshipping" value="{DotnetMuxer.ARTIFACTS_PACKAGES_NONSHIPPING}" />
        <add key="local-shipping" value="{DotnetMuxer.ARTIFACTS_PACKAGES_SHIPPING}" />
    </packageSources>
    <config>
        <add key="globalPackagesFolder" value=".packages" />
    </config>
</configuration>

""";
        var assetGenerator = new AssetGenerator(assetName, addDefaultNugetConfigFile ? string.Concat(code, defaultNugetConfig) : code);
        await assetGenerator.CreateAssetAsync();
        return assetGenerator;
    }
}
