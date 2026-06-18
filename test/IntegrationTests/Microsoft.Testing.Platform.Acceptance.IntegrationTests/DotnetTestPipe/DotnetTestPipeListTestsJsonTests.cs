// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Validates the agreed design for <c>--list-tests json</c> under <c>--server dotnettestcli</c>
/// (the mode the .NET SDK always uses for <c>dotnet test</c>): the test app does <b>not</b> render
/// the JSON document itself. Instead it streams the discovered tests to the SDK over the dotnet-test
/// pipe, and the SDK is responsible for producing the JSON (combining the tests from every test app
/// into a single document).
/// <para>
/// Built on the black-box <see cref="FakeDotnetTestSdk"/> harness introduced in
/// <a href="https://github.com/microsoft/testfx/pull/9153">microsoft/testfx#9153</a>, so the
/// behavior is exercised end-to-end against the real wire protocol.
/// </para>
/// </summary>
[TestClass]
public class DotnetTestPipeListTestsJsonTests : AcceptanceTestBase<DotnetTestPipeListTestsJsonTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeListTestsJson";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_ListTestsJson_StreamsDiscoveredTestsOverPipeAndKeepsStdoutClean()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // The SDK requests the full discovery object by advertising IsIDE in the handshake (the same
        // signal it sets when running `dotnet test --list-tests json`), so the host streams the
        // complete discovery details (file location, method identifier, traits) we assert below.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost, extraArguments: "--list-tests json", isIde: true, cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);

        // Under --server the test app must stay silent on stdout/stderr: the SDK owns rendering,
        // including building the --list-tests json document. In particular the app must NOT print a
        // JSON document itself, otherwise the SDK would receive duplicated/raw output.
        Assert.AreEqual(
            string.Empty,
            result.TestHostResult.StandardOutput.Trim(),
            $"Expected no stdout under --server dotnettestcli (the SDK renders the output).{Environment.NewLine}" +
            $"Captured stdout:{Environment.NewLine}{result.TestHostResult.StandardOutput}");

        Assert.AreEqual(
            string.Empty,
            result.TestHostResult.StandardError.Trim(),
            $"Expected no stderr noise for a successful discovery.{Environment.NewLine}" +
            $"Captured stderr:{Environment.NewLine}{result.TestHostResult.StandardError}");

        // The discovered tests must instead be streamed to the SDK as DiscoveredTestMessages frames,
        // carrying the full discovery object (file location, method identifier, traits) the SDK needs
        // to build the --list-tests json document.
        var discoveredTests = new Dictionary<string, DiscoveredTest>(StringComparer.Ordinal);
        bool sawDiscoveredFrame = false;
        foreach (RawMessage frame in result.ReceivedMessages)
        {
            if (frame.SerializerId != DotnetTestPipeProtocol.SerializerIds.DiscoveredTestMessages)
            {
                continue;
            }

            sawDiscoveredFrame = true;
            foreach (DiscoveredTest test in DotnetTestPipeProtocol.DecodeDiscoveredTests(frame.Body))
            {
                Assert.IsNotNull(test.DisplayName, "Every discovered test must carry a display name.");
                discoveredTests[test.DisplayName] = test;
            }
        }

        Assert.IsTrue(sawDiscoveredFrame, "Expected at least one DiscoveredTestMessages frame over the dotnet-test pipe.");
        Assert.Contains("Test1", discoveredTests.Keys);
        Assert.Contains("Test2", discoveredTests.Keys);

        // Regression guard: the full discovery details must keep flowing over the pipe so the SDK can
        // build the --list-tests json document. If any of these fields stop being streamed the test
        // fails, even though the display names alone would still arrive.
        DiscoveredTest test1 = discoveredTests["Test1"];
        Assert.AreEqual("MyTests.cs", test1.FilePath);
        Assert.AreEqual("MyNamespace", test1.Namespace);
        Assert.AreEqual("MyTestClass", test1.TypeName);
        Assert.AreEqual("Test1", test1.MethodName);
        Assert.AreEqual("Smoke", test1.Traits["Category"]);

        DiscoveredTest test2 = discoveredTests["Test2"];
        Assert.AreEqual("MyTests.cs", test2.FilePath);
        Assert.AreEqual("Test2", test2.MethodName);
        Assert.AreEqual("Integration", test2.Traits["Category"]);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetCode = """
#file DotnetTestPipeListTestsJson.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
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

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DiscoveringTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DiscoveringTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DiscoveringTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DiscoveringTestFramework);
    public string Description => nameof(DiscoveringTestFramework);
    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "0",
                DisplayName = "Test1",
                Properties = new(
                    DiscoveredTestNodeStateProperty.CachedInstance,
                    new TestFileLocationProperty("MyTests.cs", new LinePositionSpan(new LinePosition(10, 1), new LinePosition(10, 5))),
                    new TestMethodIdentifierProperty("MyTestAssembly", "MyNamespace", "MyTestClass", "Test1", 0, [], "System.Void"),
                    new TestMetadataProperty("Category", "Smoke")),
            }));
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "1",
                DisplayName = "Test2",
                Properties = new(
                    DiscoveredTestNodeStateProperty.CachedInstance,
                    new TestFileLocationProperty("MyTests.cs", new LinePositionSpan(new LinePosition(20, 1), new LinePosition(20, 5))),
                    new TestMethodIdentifierProperty("MyTestAssembly", "MyNamespace", "MyTestClass", "Test2", 0, [], "System.Void"),
                    new TestMetadataProperty("Category", "Integration")),
            }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
