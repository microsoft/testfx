// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TrxFailingTestTests : AcceptanceTestBase<TrxFailingTestTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTestFails_ContainsExceptionInfoInOutput(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxFile = trxFiles[0];
        string trxContent = File.ReadAllText(trxFile);

        // Verify that the TRX contains the UnitTestResult with outcome="Failed"
        Assert.Contains(@"<UnitTestResult", trxContent, trxContent);
        Assert.Contains(@"outcome=""Failed""", trxContent, trxContent);

        // Verify that the TRX contains the Output element with error info
        Assert.Contains(@"<Output>", trxContent, trxContent);
        Assert.Contains(@"<ErrorInfo>", trxContent, trxContent);

        // Verify that exception message is present
        Assert.Contains(@"<Message>", trxContent, trxContent);
        Assert.Contains("Expected 1 but got 2", trxContent, trxContent);

        // Verify that stack trace is present
        Assert.Contains(@"<StackTrace>", trxContent, trxContent);
        Assert.Contains("at DummyTestFramework.ExecuteRequestAsync", trxContent, trxContent);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "TrxTest";
        private const string WithFailingTest = nameof(WithFailingTest);

        private const string FailingTestCode = """
#file TrxTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_,__) => new DummyTestFramework());
        builder.AddTrxReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var testMethodIdentifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test", 0, Array.Empty<string>(), string.Empty);

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "0",
                DisplayName = "FailingTest",
                Properties = new PropertyBag(
                    new FailedTestNodeStateProperty("Expected 1 but got 2"),
                    testMethodIdentifier,
                    new TrxExceptionProperty("Expected 1 but got 2", "   at DummyTestFramework.ExecuteRequestAsync() in Program.cs:line 50"))
            }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(WithFailingTest);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (WithFailingTest, AssetName,
                FailingTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
