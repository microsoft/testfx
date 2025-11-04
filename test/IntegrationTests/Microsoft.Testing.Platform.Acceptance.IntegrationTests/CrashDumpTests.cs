// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class CrashDumpTests : AcceptanceTestBase<CrashDumpTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_DefaultSetting_CreateDump(string tfm)
    {
        string globalProperties = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Workaround: createdump doesn't work correctly on the apphost on macOS.
            // But it works correctly on the dotnet process.
            // So, disable apphost on macOS for now.
            // Related: https://github.com/dotnet/runtime/issues/119945
            globalProperties = "-p:UseAppHost=false";
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {AssetFixture.TargetAssetPath} -f {tfm} {globalProperties} --crashdump --results-directory {resultDirectory}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{tfm}'\n{result}'");
    }

    [TestMethod]
    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        string globalProperties = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Workaround: createdump doesn't work correctly on the apphost on macOS.
            // But it works correctly on the dotnet process.
            // So, disable apphost on macOS for now.
            // Related: https://github.com/dotnet/runtime/issues/119945
            globalProperties = "-p:UseAppHost=false";
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {AssetFixture.TargetAssetPath} -f {TargetFrameworks.NetCurrent} {globalProperties} --crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        Assert.IsNotNull(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories).SingleOrDefault(), "Dump file not found");
    }

    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    [TestMethod]
    public async Task CrashDump_Formats_CreateDump(string format)
    {
        string globalProperties = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Workaround: createdump doesn't work correctly on the apphost on macOS.
            // But it works correctly on the dotnet process.
            // So, disable apphost on macOS for now.
            // Related: https://github.com/dotnet/runtime/issues/119945
            globalProperties = "-p:UseAppHost=false";
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {AssetFixture.TargetAssetPath} -f {TargetFrameworks.NetCurrent} {globalProperties} --crashdump --crashdump-type {format} --results-directory {resultDirectory}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{format}'\n{result}'");
        File.Delete(dumpFile);
    }

    [TestMethod]
    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string globalProperties = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Workaround: createdump doesn't work correctly on the apphost on macOS.
            // But it works correctly on the dotnet process.
            // So, disable apphost on macOS for now.
            // Related: https://github.com/dotnet/runtime/issues/119945
            globalProperties = "-p:UseAppHost=false";
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {AssetFixture.TargetAssetPath} -f {TargetFrameworks.NetCurrent} {globalProperties} --crashdump --crashdump-type invalid --results-directory {resultDirectory}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        result.AssertOutputContains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "CrashDumpFixture";

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        public string TargetAssetPath => GetAssetPath(AssetName);

        private const string Sources = """
#file CrashDump.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Environment.FailFast("CrashDump");
        context.Complete();
        return Task.CompletedTask;
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
