#:package NuGet.Protocol@7.0.1

using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

if (args.Length != 3)
{
    Console.WriteLine("Usage: PackageDownloader <mstest-version> <mstest-engine-version> <mtp-version>");
    return 1;
}

string mstestVersion = args[0];
string mstestEngineVersion = args[1];
string mtpVersion = args[2];
string feedUrl = "https://pkgs.dev.azure.com/dnceng/public/_packaging/test-tools/nuget/v3/index.json";

var packages = new (string, string)[]
{
    // MSTest packages
    ("MSTest", mstestVersion),
    ("MSTest.Analyzers", mstestVersion),
    ("MSTest.Sdk", mstestVersion),
    ("MSTest.TestFramework", mstestVersion),
    ("MSTest.TestAdapter", mstestVersion),

    // MSTest engine packages
    ("MSTest.Engine", mstestEngineVersion),
    ("MSTest.SourceGeneration", mstestEngineVersion),

    // MTP packages
    ("Microsoft.Testing.Extensions.AzureDevOpsReport", mtpVersion),
    ("Microsoft.Testing.Extensions.CrashDump", mtpVersion),
    ("Microsoft.Testing.Extensions.HangDump", mtpVersion),
    ("Microsoft.Testing.Extensions.HotReload", mtpVersion),
    ("Microsoft.Testing.Extensions.Retry", mtpVersion),
    ("Microsoft.Testing.Extensions.Telemetry", mtpVersion),
    ("Microsoft.Testing.Extensions.TrxReport", mtpVersion),
    ("Microsoft.Testing.Extensions.TrxReport.Abstractions", mtpVersion),
    ("Microsoft.Testing.Extensions.VSTestBridge", mtpVersion),
    ("Microsoft.Testing.Platform", mtpVersion),
    ("Microsoft.Testing.Platform.MSBuild", mtpVersion),
};

try
{
    var logger = NullLogger.Instance;
    var cache = new SourceCacheContext();
    var repo = Repository.Factory.GetCoreV3(feedUrl);
    var resource = await repo.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
    Directory.CreateDirectory("PackagesToPublish");
    foreach (var (packageName, version) in packages)
    {
        var nugetVersion = NuGetVersion.Parse(version);
        string outFile = Path.Combine("PackagesToPublish", $"{packageName}.{version}.nupkg");
        using var stream = File.Open(outFile, FileMode.Create);

        bool success = await resource.CopyNupkgToStreamAsync(
            packageName,
            nugetVersion,
            stream,
            cache,
            logger,
            CancellationToken.None).ConfigureAwait(false);
        if (!success)
        {
            Console.WriteLine("Package not found or failed to download.");
            return 2;
        }

        Console.WriteLine($"Downloaded to {outFile}");
    }

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 3;
}
