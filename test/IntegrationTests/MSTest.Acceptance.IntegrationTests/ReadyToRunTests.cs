// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ReadyToRunTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestReadyToRun";
    private const uint ReadyToRunSignature = 0x00525452;

    private const string SourceCode = """
#file MSTestReadyToRun.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <PublishReadyToRun>true</PublishReadyToRun>
    <SelfContained>false</SelfContained>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
  </PropertyGroup>
</Project>

#file ReadyToRunTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReadyToRunTestAsset;

[TestClass]
public sealed class CalculatorTests
{
    [TestMethod]
    public void Add_ReturnsExpectedSum()
    {
        Assert.AreEqual(42, 19 + 23);
    }

    [TestMethod]
    [DataRow(2, 3, 6)]
    [DataRow(-4, 5, -20)]
    public void Multiply_ReturnsExpectedProduct(int left, int right, int expected)
    {
        Assert.AreEqual(expected, left * right);
    }
}
""";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task PublishReadyToRun_FrameworkDependentApp_IsReadyToRunAndExecutesTests()
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm),
            addPublicFeeds: true);

        DotnetMuxerResult publishResult = await DotnetCli.RunAsync(
            $"publish {testAsset.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm} -r {RID} --self-contained false",
            cancellationToken: TestContext.CancellationToken);
        publishResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(
            testAsset.TargetAssetPath,
            AssetName,
            tfm,
            RID,
            Verb.publish,
            BuildConfiguration.Release);

        AssertReadyToRun(Path.Combine(testHost.DirectoryName, $"{AssetName}.dll"));

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    private static void AssertReadyToRun(string assemblyPath)
    {
        using FileStream assembly = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(assembly);

        CorHeader? corHeader = peReader.PEHeaders.CorHeader;
        Assert.IsNotNull(corHeader, $"Published assembly '{assemblyPath}' should have a CLR header.");

        DirectoryEntry managedNativeHeader = corHeader.ManagedNativeHeaderDirectory;
        Assert.IsGreaterThan(0, managedNativeHeader.RelativeVirtualAddress, $"Published assembly '{assemblyPath}' should have a managed native header.");
        Assert.IsGreaterThanOrEqualTo(sizeof(uint), managedNativeHeader.Size, $"Published assembly '{assemblyPath}' should have a complete ReadyToRun header.");

        byte[] signatureBytes = peReader
            .GetSectionData(managedNativeHeader.RelativeVirtualAddress)
            .GetContent(0, sizeof(uint))
            .ToArray();
        uint signature = BinaryPrimitives.ReadUInt32LittleEndian(signatureBytes);

        Assert.AreEqual(ReadyToRunSignature, signature, $"Published assembly '{assemblyPath}' should contain the ReadyToRun signature.");
    }
}
