// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class TestHostControllerDataConsumerTests : AcceptanceTestBase<TestHostControllerDataConsumerTests.TestAssetFixture>
{
    private const string AssetName = "TestHostControllerDataConsumer";

    [TestMethod]
    [DataRow(false, DisplayName = "Consumer registered after producer")]
    [DataRow(true, DisplayName = "Consumer registered before producer")]
    public async Task ControllerDataConsumerObserver_ConsumesCoverageReportInEitherRegistrationOrder(bool consumerFirst)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string id = Guid.NewGuid().ToString("N");
        string reportPath = Path.Combine(testHost.DirectoryName, $"{id}.coverage.xml");
        string htmlPath = Path.Combine(testHost.DirectoryName, $"{id}.html");
        const string CoverageContent = "<coverage lines-covered=\"1\" lines-valid=\"1\" />";

        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["ENABLE_CONTROLLER_DATA_CONSUMER"] = "1",
                ["REGISTER_CONSUMER_FIRST"] = consumerFirst.ToString(),
                ["COVERAGE_REPORT_PATH"] = reportPath,
                ["COVERAGE_REPORT_CONTENT"] = CoverageContent,
                ["GENERATED_REPORT_PATH"] = htmlPath,
                ["SKIP_FIXED_LIFECYCLE_FILES"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual(CoverageContent, File.ReadAllText(reportPath));
        Assert.IsTrue(File.Exists(htmlPath), "The controller data consumer did not produce the report.");
        Assert.AreEqual($"<html><body>{CoverageContent}</body></html>", File.ReadAllText(htmlPath));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file TestHostControllerDataConsumer.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>preview</LangVersion>
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new TestFrameworkCapabilities(),
    (_, _) => new DummyTestFramework());

bool registerConsumer = Environment.GetEnvironmentVariable("ENABLE_CONTROLLER_DATA_CONSUMER") == "1";
bool consumerFirst = Environment.GetEnvironmentVariable("REGISTER_CONSUMER_FIRST") == "True";
if (registerConsumer && consumerFirst)
{
    builder.TestHostControllers.AddDataConsumerWhenControllerStarts(_ => new CoverageReportConsumer());
}

builder.TestHostControllers.AddProcessLifetimeHandler(
    serviceProvider => new CoverageReportLifetimeHandler(serviceProvider.GetMessageBus()));

if (registerConsumer && !consumerFirst)
{
    builder.TestHostControllers.AddDataConsumerWhenControllerStarts(_ => new CoverageReportConsumer());
}

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

public sealed class CoverageReportLifetimeHandler(IMessageBus messageBus) : ITestHostProcessLifetimeHandler, IDataProducer
{
    public string Uid => "acceptance.coverage.producer";
    public string Version => "1.0";
    public string DisplayName => Uid;
    public string Description => Uid;
    public Type[] DataTypesProduced => [typeof(TestCoverageReportMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        string? reportPath = Environment.GetEnvironmentVariable("COVERAGE_REPORT_PATH");
        if (string.IsNullOrEmpty(reportPath))
        {
            return;
        }

        File.WriteAllText(reportPath, Environment.GetEnvironmentVariable("COVERAGE_REPORT_CONTENT")!);
        await messageBus.PublishAsync(
            this,
            new TestCoverageReportMessage(new SessionUid("acceptance-session"), reportPath, CoverageReportFormat.Cobertura, "acceptance.coverage.collector"));
    }
}

public sealed class CoverageReportConsumer : IDataConsumer
{
    public string Uid => "acceptance.coverage.consumer";
    public string Version => "1.0";
    public string DisplayName => Uid;
    public string Description => Uid;
    public Type[] DataTypesConsumed => [typeof(TestCoverageReportMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is TestCoverageReportMessage report)
        {
            if (report.Format != CoverageReportFormat.Cobertura || !File.Exists(report.ReportPath))
            {
                throw new InvalidOperationException("The coverage report was not available to the controller consumer.");
            }

            string content = File.ReadAllText(report.ReportPath);
            string htmlPath = Environment.GetEnvironmentVariable("GENERATED_REPORT_PATH")!;
            File.WriteAllText(htmlPath, $"<html><body>{content}</body></html>");
        }

        return Task.CompletedTask;
    }
}

public sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => "acceptance.testframework";
    public string Version => "1.0";
    public string DisplayName => Uid;
    public string Description => Uid;
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
            new TestNode { Uid = "test", DisplayName = "test", Properties = new(new PassedTestNodeStateProperty()) }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; } = null!;
}
