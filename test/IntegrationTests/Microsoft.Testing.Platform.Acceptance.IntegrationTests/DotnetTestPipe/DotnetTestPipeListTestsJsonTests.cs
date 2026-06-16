// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Validates the fix for <a href="https://github.com/microsoft/testfx/issues/8661">microsoft/testfx#8661</a>:
/// <c>--list-tests json</c> must still emit its JSON document to stdout when the test app runs under
/// <c>--server dotnettestcli</c> (the mode the .NET SDK always uses for <c>dotnet test</c>).
/// <para>
/// Built on the black-box <see cref="FakeDotnetTestSdk"/> harness introduced in
/// <a href="https://github.com/microsoft/testfx/pull/9153">microsoft/testfx#9153</a>, so the
/// before/after of this behavior is exercised end-to-end against the real wire protocol.
/// </para>
/// </summary>
[TestClass]
public class DotnetTestPipeListTestsJsonTests : AcceptanceTestBase<DotnetTestPipeListTestsJsonTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeListTestsJson";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_ListTestsJson_EmitsJsonDocumentToStdoutUnderServerMode()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost, extraArguments: "--list-tests json", cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);

        // The JSON document must be the only thing on stdout (no banner, no progress, no summary).
        // Trim because the test infrastructure may append a trailing newline.
        string output = result.TestHostResult.StandardOutput.Trim();
        Assert.IsTrue(
            output.StartsWith('{') && output.EndsWith('}'),
            $"Expected stdout to be a single JSON object under --server dotnettestcli, but got:{Environment.NewLine}{output}");

        Assert.AreEqual(
            string.Empty,
            result.TestHostResult.StandardError.Trim(),
            $"Expected no stderr noise for a successful JSON discovery.{Environment.NewLine}" +
            $"Captured stderr:{Environment.NewLine}{result.TestHostResult.StandardError}");

        using var document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;

        Assert.AreEqual(DiscoveredTestsJsonSchemaVersion, root.GetProperty("schemaVersion").GetInt32());

        JsonElement tests = root.GetProperty("tests");
        var displayNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tests.GetArrayLength(); i++)
        {
            displayNames.Add(tests[i].GetProperty("displayName").GetString()!);
        }

        Assert.Contains("Test1", displayNames);
        Assert.Contains("Test2", displayNames);
    }

    /// <summary>
    /// Mirror of <c>DiscoveredTestsJsonSerializer.SchemaVersion</c>. The serializer type is
    /// <c>[Microsoft.CodeAnalysis.Embedded]</c> internal and cannot be referenced from this
    /// acceptance project, so the expected schema version is duplicated here intentionally — a
    /// bump on the product side should be a conscious update here too.
    /// </summary>
    private const int DiscoveredTestsJsonSchemaVersion = 1;

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
            new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "Test2", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));

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
