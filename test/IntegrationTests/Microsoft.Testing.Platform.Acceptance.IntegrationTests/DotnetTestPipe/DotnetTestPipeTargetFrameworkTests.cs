// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

[TestClass]
public sealed class DotnetTestPipeTargetFrameworkTests : AcceptanceTestBase<NopAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, "net10.0", DisplayName = "DeclaredModernTarget")]
    [DataRow(true, null, DisplayName = "MissingTargetIsNotRuntime")]
    public Task DotnetTestPipe_TargetFrameworkReflectsApplicationMetadata(bool suppressTargetAttribute, string? expectedTarget)
        => VerifyTargetFrameworkAsync("net10.0", suppressTargetAttribute, expectedTarget);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public Task DotnetTestPipe_FrameworkTargetIsNotInstalledRuntimeOrPackageTarget()
        => VerifyTargetFrameworkAsync("net472", suppressTargetAttribute: false, expectedTarget: "net472", verifyStandalone: true);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public Task DotnetTestPipe_TargetFrameworkIncludesDeclaredPlatform()
        => VerifyTargetFrameworkAsync("net10.0-windows10.0.19041.0", suppressTargetAttribute: false, expectedTarget: "net10.0-windows10.0.19041.0");

    private async Task VerifyTargetFrameworkAsync(string framework, bool suppressTargetAttribute, string? expectedTarget, bool verifyStandalone = false)
    {
        const string assetName = "AssemblyTargetProbe";
        string code = AssetCode
            .PatchCodeWithReplace("$TargetFramework$", framework)
            .PatchCodeWithReplace("$GenerateTargetFrameworkAttribute$", suppressTargetAttribute ? "false" : "true")
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);
        using TestAsset asset = await TestAsset.GenerateAssetAsync($"{assetName}-{Guid.NewGuid():N}", code);
        await DotnetCli.RunAsync(
            $"build {asset.TargetAssetPath} -c Release",
            cancellationToken: TestContext.CancellationToken);
        var testHost = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, assetName, framework);
        VerifyPackageProvenance(asset, testHost);

        // A 1.0 peer ignores new optional properties and still receives the unchanged runtime field.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(testHost, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.TestHostResult.ExitCode, result.TestHostResult.ToString());
        Assert.AreEqual("1.0.0", result.NegotiatedProtocolVersion);
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.AreEqual(
            expectedTarget is not null,
            result.ReceivedHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.TargetFramework, out string? target));
        Assert.AreEqual(expectedTarget, target);
        string runtime = result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.Framework];
        Assert.StartsWith(framework == "net472" ? ".NET Framework " : ".NET 10.", runtime);
        Assert.HasCount(1, result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestResultMessages).ToArray());
        TestContext.WriteLine($"Consumer: {asset.TargetAssetPath}; package: Microsoft.Testing.Platform/{MicrosoftTestingPlatformVersion}; target: {target ?? "<absent>"}; runtime: {runtime}");

        if (verifyStandalone)
        {
            TestHostResult standalone = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
            Assert.AreEqual(0, standalone.ExitCode, standalone.ToString());
            string architecture = result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.Architecture].ToLowerInvariant();
            Assert.MatchesRegex($@"\[{architecture} - net\d+\]", standalone.StandardOutput);
            Assert.Contains($"{assetName}.exe (net472|{architecture})", standalone.StandardOutput);
            Assert.DoesNotContain("runtime:", standalone.StandardOutput);
        }
    }

    private void VerifyPackageProvenance(TestAsset asset, TestInfrastructure.TestHost testHost)
    {
        string assetsPath = Path.Combine(asset.TargetAssetPath, "obj", "project.assets.json");
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        string packageIdentity = $"Microsoft.Testing.Platform/{MicrosoftTestingPlatformVersion}";
        JsonElement library = assets.RootElement.GetProperty("libraries").GetProperty(packageIdentity);
        string packagePath = library.GetProperty("path").GetString()!;
        string runtimeAsset = assets.RootElement.GetProperty("targets").EnumerateObject().First().Value
            .GetProperty(packageIdentity).GetProperty("runtime").EnumerateObject().Single().Name;
        string cachedAssembly = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, packagePath.Replace('/', Path.DirectorySeparatorChar), runtimeAsset.Replace('/', Path.DirectorySeparatorChar)))
            .First(File.Exists);
        string deployedAssembly = Path.Combine(testHost.DirectoryName, "Microsoft.Testing.Platform.dll");
        string package = Path.Combine(Constants.ArtifactsPackagesShipping, $"Microsoft.Testing.Platform.{MicrosoftTestingPlatformVersion}.nupkg");
        using ZipArchive archive = ZipFile.OpenRead(package);
        ZipArchiveEntry? payload = archive.GetEntry(runtimeAsset);
        Assert.IsNotNull(payload);
        using Stream stream = payload.Open();
        string expectedHash = Convert.ToHexString(SHA256.HashData(stream));

        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cachedAssembly))));
        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(deployedAssembly))));
        TestContext.WriteLine($"Assets: {assetsPath}; cached: {cachedAssembly}; deployed: {deployedAssembly}; payload SHA256: {expectedHash}");
    }

    private const string AssetCode = """
        #file AssemblyTargetProbe.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>$TargetFramework$</TargetFramework>
            <GenerateTargetFrameworkAttribute>$GenerateTargetFrameworkAttribute$</GenerateTargetFrameworkAttribute>
            <OutputType>Exe</OutputType>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <LangVersion>preview</LangVersion>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
          </ItemGroup>
        </Project>

        #file Program.cs
        using Microsoft.Testing.Platform.Builder;
        using Microsoft.Testing.Platform.Capabilities.TestFramework;
        using Microsoft.Testing.Platform.Extensions.Messages;
        using Microsoft.Testing.Platform.Extensions.TestFramework;

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new ProbeFramework());
        using ITestApplication application = await builder.BuildAsync();
        return await application.RunAsync();

        public sealed class ProbeFramework : ITestFramework, IDataProducer
        {
            public string Uid => "target.probe";
            public string Version => "1.0.0";
            public string DisplayName => "Target probe";
            public string Description => "Publishes one passing test.";
            public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
            public Task<bool> IsEnabledAsync() => Task.FromResult(true);
            public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
                => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
            public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
                => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
            public async Task ExecuteRequestAsync(ExecuteRequestContext context)
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                    context.Request.Session.SessionUid,
                    new TestNode
                    {
                        Uid = "target-test",
                        DisplayName = "Target test",
                        Properties = new(PassedTestNodeStateProperty.CachedInstance),
                    }));
                context.Complete();
            }
        }
        """;
}
