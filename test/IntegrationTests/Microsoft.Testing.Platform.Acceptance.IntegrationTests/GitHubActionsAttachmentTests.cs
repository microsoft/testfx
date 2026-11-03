// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

// The packed asset is read-only after class initialization. Every host invocation gets independent
// sources, results and summary files, so the assembly's method-level parallelism remains safe.
[TestClass]
public sealed class GitHubActionsAttachmentTests : AcceptanceTestBase<GitHubActionsAttachmentTests.TestAssetFixture>
{
    private const string AssetName = "GitHubActionsAttachmentBehavior";
    private const string AttachmentTargetFramework = "net8.0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AttachmentsOptIn_StagesFailedFilesAndPreservesExistingOutput()
    {
        using Invocation invocation = CreateInvocation();
        Directory.CreateDirectory(invocation.AttachmentsDirectory);
        string existing = Path.Combine(invocation.AttachmentsDirectory, "existing.txt");
        File.WriteAllText(existing, "keep existing artifact");
        File.WriteAllText(invocation.SummaryPath, "# Existing primary summary\n\n");

        (TestHostResult result, string summary) = await RunAsync(invocation);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        Assert.StartsWith("# Existing primary summary\n\n", summary.ReplaceLineEndings("\n"));
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("| 4 | 1 | 2 | 1 | 0 | 0ms |", summary);
        Assert.AreEqual("keep existing artifact", File.ReadAllText(existing));
        using JsonDocument manifest = ReadSingleManifest(invocation.AttachmentsDirectory);
        Assert.AreEqual(AssetName, manifest.RootElement.GetProperty("assemblyName").GetString());
        Assert.AreEqual(AttachmentTargetFramework, manifest.RootElement.GetProperty("targetFramework").GetString());
        Assert.IsNotEmpty(manifest.RootElement.GetProperty("sessionUid").GetString()!);
        JsonElement[] tests = manifest.RootElement.GetProperty("tests").EnumerateArray().ToArray();
        Assert.AreSequenceEqual(
            new[] { "failed-1", "failed-2" },
            tests.Select(test => test.GetProperty("uid").GetString()).OrderBy(uid => uid, StringComparer.Ordinal));
        JsonElement firstTest = tests.Single(test => test.GetProperty("uid").GetString() == "failed-1");
        JsonElement secondTest = tests.Single(test => test.GetProperty("uid").GetString() == "failed-2");
        AssertStagedPair(invocation.AttachmentsDirectory, firstTest.GetProperty("attachments"), summary);
        AssertStagedAttachment(
            invocation.AttachmentsDirectory,
            Assert.ContainsSingle(secondTest.GetProperty("attachments").EnumerateArray()), [0, 65, 255], summary);
        Assert.HasCount(3, Directory.GetFiles(invocation.AttachmentsDirectory, "*.bin", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(new byte[] { 0, 65, 255 }, File.ReadAllBytes(invocation.FirstSource));
        Assert.AreSequenceEqual(new byte[] { 66, 0, 67, 68 }, File.ReadAllBytes(invocation.SecondSource));
    }

    [TestMethod]
    [DataRow(false, true, "fail", DisplayName = "Attachment opt-in absent")]
    [DataRow(true, false, "fail", DisplayName = "Outside GitHub Actions")]
    [DataRow(true, true, "pass", DisplayName = "Passing tests with attachments")]
    public async Task AttachmentsAbsentOrOutsideGitHubActions_DoNotCreateStagingOutput(bool optIn, bool githubActions, string mode)
    {
        using Invocation invocation = CreateInvocation();

        (TestHostResult result, string summary) = await RunAsync(invocation, mode, optIn, githubActions);

        result.AssertExitCodeIs(mode == "pass" ? ExitCode.Success : ExitCode.AtLeastOneTestFailed);
        Assert.IsFalse(Directory.Exists(invocation.AttachmentsDirectory));
        Assert.DoesNotContain("Attachments (", summary);
        if (githubActions)
        {
            Assert.Contains(mode == "pass" ? "✅ Test Run Summary" : "❌ Test Run Summary", summary);
        }
        else
        {
            Assert.AreEqual(string.Empty, summary);
        }
    }

    [TestMethod]
    [DataRow("--report-gh-step-summary off", false, DisplayName = "Summary off still stages")]
    [DataRow("--report-gh-failure-details off", true, DisplayName = "Failure details off still stages")]
    public async Task AttachmentsWithSummaryOrFailureDetailsDisabled_StillStage(string extraArgs, bool summaryExpected)
    {
        using Invocation invocation = CreateInvocation();

        (TestHostResult result, string summary) = await RunAsync(invocation, extraArgs: extraArgs);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        using JsonDocument manifest = ReadSingleManifest(invocation.AttachmentsDirectory);
        JsonElement tests = manifest.RootElement.GetProperty("tests");
        Assert.AreEqual(2, tests.GetArrayLength());
        JsonElement firstTest = tests.EnumerateArray().Single(test => test.GetProperty("uid").GetString() == "failed-1");
        JsonElement secondTest = tests.EnumerateArray().Single(test => test.GetProperty("uid").GetString() == "failed-2");
        AssertStagedPair(invocation.AttachmentsDirectory, firstTest.GetProperty("attachments"));
        AssertStagedAttachment(
            invocation.AttachmentsDirectory,
            Assert.ContainsSingle(secondTest.GetProperty("attachments").EnumerateArray()), [0, 65, 255]);
        Assert.AreEqual(summaryExpected, File.Exists(invocation.SummaryPath));
        Assert.DoesNotContain("Attachments (", summary);
        if (summaryExpected)
        {
            Assert.Contains("AttachmentTests.FailedOne", summary);
            Assert.Contains("### ❌ Failures (2)", summary);
            Assert.DoesNotContain("<details>", summary);
        }
        else
        {
            Assert.AreEqual(string.Empty, summary);
            Assert.IsFalse(Directory.Exists(Path.Combine(invocation.ResultsDirectory, ".ci-summary-fragments")));
        }
    }

    [TestMethod]
    public async Task AttachmentsUnavailable_WarnWithoutChangingFailureExitCode()
    {
        using Invocation invocation = CreateInvocation();

        (TestHostResult result, string summary) = await RunAsync(invocation, mode: "unavailable");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContains("unavailable");
        using JsonDocument manifest = ReadSingleManifest(invocation.AttachmentsDirectory);
        JsonElement test = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("failed-1", test.GetProperty("uid").GetString());
        JsonElement attachments = test.GetProperty("attachments");
        Assert.AreEqual(2, attachments.GetArrayLength());
        AssertStagedAttachment(
            invocation.AttachmentsDirectory,
            attachments.EnumerateArray().Single(attachment => attachment.GetProperty("status").GetString() == "staged"), [0, 65, 255], summary);
        JsonElement missing = attachments.EnumerateArray().Single(attachment => attachment.GetProperty("fileName").GetString() == "missing.bin");
        Assert.AreEqual("missing.bin", missing.GetProperty("fileName").GetString());
        Assert.AreEqual("unavailable", missing.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, missing.GetProperty("relativePath").ValueKind);
        Assert.AreEqual(0L, missing.GetProperty("sizeBytes").GetInt64());
        Assert.Contains("- `missing.bin` — not staged; see manifest.", summary);
        Assert.DoesNotContain(invocation.Root, summary);
        Assert.ContainsSingle(Directory.GetFiles(invocation.AttachmentsDirectory, "*.bin", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task ConcurrentModulesSharingRoot_KeepDistinctManifestsAndBytes()
    {
        using Invocation first = CreateInvocation();
        using Invocation second = CreateInvocation();

        (TestHostResult Result, string Summary)[] runs = await Task.WhenAll(
            RunAsync(first),
            RunAsync(second, stagingDirectory: first.AttachmentsDirectory));

        foreach ((TestHostResult result, string summary) in runs)
        {
            result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            Assert.Contains("| 4 | 1 | 2 | 1 | 0 | 0ms |", summary);
        }

        string[] manifests = Directory.GetFiles(first.AttachmentsDirectory, "attachments.json", SearchOption.AllDirectories);
        Assert.HasCount(2, manifests);
        Assert.HasCount(2, manifests.Select(Path.GetDirectoryName).Distinct(StringComparer.Ordinal));
        var sessions = new List<string>();
        foreach (string path in manifests)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            sessions.Add(manifest.RootElement.GetProperty("sessionUid").GetString()!);
            JsonElement tests = manifest.RootElement.GetProperty("tests");
            Assert.AreEqual(2, tests.GetArrayLength());
            JsonElement firstTest = tests.EnumerateArray().Single(test => test.GetProperty("uid").GetString() == "failed-1");
            JsonElement secondTest = tests.EnumerateArray().Single(test => test.GetProperty("uid").GetString() == "failed-2");
            AssertStagedPair(first.AttachmentsDirectory, firstTest.GetProperty("attachments"));
            AssertStagedAttachment(
                first.AttachmentsDirectory,
                Assert.ContainsSingle(secondTest.GetProperty("attachments").EnumerateArray()), [0, 65, 255]);
        }

        Assert.HasCount(2, sessions.Distinct(StringComparer.Ordinal));
        Assert.HasCount(6, Directory.GetFiles(first.AttachmentsDirectory, "*.bin", SearchOption.AllDirectories));
    }

    private Invocation CreateInvocation() => new(TestContext.TestRunDirectory!);

    private async Task<(TestHostResult Result, string Summary)> RunAsync(
        Invocation invocation,
        string mode = "fail",
        bool optIn = true,
        bool githubActions = true,
        string extraArgs = "",
        string? stagingDirectory = null)
    {
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, AttachmentTargetFramework);
        string attachments = optIn ? $"--report-gh-attachments \"{stagingDirectory ?? invocation.AttachmentsDirectory}\"" : string.Empty;
        TestHostResult result = await host.ExecuteAsync(
            $"--report-gh {attachments} --results-directory \"{invocation.ResultsDirectory}\" {extraArgs}",
            environmentVariables: new Dictionary<string, string?>
            {
                ["GITHUB_ACTIONS"] = githubActions ? "true" : "false",
                ["TF_BUILD"] = "false",
                ["GITHUB_STEP_SUMMARY"] = invocation.SummaryPath,
                ["GH_ATTACHMENT_MODE"] = mode,
                ["GH_ATTACHMENT_FIRST"] = invocation.FirstSource,
                ["GH_ATTACHMENT_SECOND"] = invocation.SecondSource,
                ["GH_ATTACHMENT_MISSING"] = Path.Combine(invocation.Root, "missing.bin"),
            },
            cancellationToken: TestContext.CancellationToken);
        return (result, File.Exists(invocation.SummaryPath) ? File.ReadAllText(invocation.SummaryPath) : string.Empty);
    }

    private static JsonDocument ReadSingleManifest(string directory)
        => JsonDocument.Parse(File.ReadAllText(Assert.ContainsSingle(
            Directory.GetFiles(directory, "attachments.json", SearchOption.AllDirectories))));

    private static void AssertStagedPair(string root, JsonElement attachments, string? summary = null)
    {
        JsonElement[] ordered = attachments.EnumerateArray().OrderBy(attachment => attachment.GetProperty("sizeBytes").GetInt64()).ToArray();
        Assert.HasCount(2, ordered);
        AssertStagedAttachment(root, ordered[0], [0, 65, 255], summary);
        AssertStagedAttachment(root, ordered[1], [66, 0, 67, 68], summary);
    }

    private static void AssertStagedAttachment(string root, JsonElement attachment, byte[] expected, string? summary = null)
    {
        Assert.AreEqual("staged", attachment.GetProperty("status").GetString());
        Assert.AreEqual("report_log.bin", attachment.GetProperty("fileName").GetString());
        Assert.AreEqual(expected.LongLength, attachment.GetProperty("sizeBytes").GetInt64());
        string relativePath = attachment.GetProperty("relativePath").GetString()!;
        Assert.IsFalse(Path.IsPathRooted(relativePath));
        Assert.DoesNotContain("..", relativePath.Split('/'));
        Assert.DoesNotContain('\\', relativePath);
        Assert.MatchesRegex("^[a-zA-Z0-9._/-]+$", relativePath);
        Assert.AreSequenceEqual(expected, File.ReadAllBytes(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        if (summary is not null)
        {
            Assert.Contains($"- `{relativePath}` ({expected.LongLength} bytes)", summary);
        }
    }

    private sealed class Invocation : IDisposable
    {
        internal Invocation(string testRunDirectory)
        {
            Root = Path.Combine(testRunDirectory, "github-attachments-" + Guid.NewGuid().ToString("N"));
            string first = Path.Combine(Root, "sources", "first");
            string second = Path.Combine(Root, "sources", "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            FirstSource = Path.Combine(first, "report log.bin");
            SecondSource = Path.Combine(second, "report log.bin");
            File.WriteAllBytes(FirstSource, [0, 65, 255]);
            File.WriteAllBytes(SecondSource, [66, 0, 67, 68]);
        }

        internal string Root { get; }

        internal string FirstSource { get; }

        internal string SecondSource { get; }

        internal string AttachmentsDirectory => Path.Combine(Root, "attachments");

        internal string SummaryPath => Path.Combine(Root, "summary.md");

        internal string ResultsDirectory => Path.Combine(Root, "results");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file GitHubActionsAttachmentBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.GitHubActionsReport" Version="$MicrosoftTestingExtensionsGitHubActionsReportVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new AttachmentTestFramework());
        builder.AddGitHubActionsProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class AttachmentTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(AttachmentTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(AttachmentTestFramework);
    public string Description => nameof(AttachmentTestFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        string first = Environment.GetEnvironmentVariable("GH_ATTACHMENT_FIRST")!;
        string second = Environment.GetEnvironmentVariable("GH_ATTACHMENT_SECOND")!;
        string mode = Environment.GetEnvironmentVariable("GH_ATTACHMENT_MODE")!;
        if (mode == "pass")
        {
            await PublishAsync(context, "passed", "AttachmentTests.Passed", PassedTestNodeStateProperty.CachedInstance, first);
        }
        else if (mode == "unavailable")
        {
            await PublishAsync(context, "failed-1", "AttachmentTests.FailedOne", new FailedTestNodeStateProperty("expected failure"),
                first, Environment.GetEnvironmentVariable("GH_ATTACHMENT_MISSING")!);
        }
        else
        {
            await PublishAsync(context, "failed-1", "AttachmentTests.FailedOne", new FailedTestNodeStateProperty("first expected failure"), first, second);
            await PublishAsync(context, "failed-2", "AttachmentTests.FailedTwo", new FailedTestNodeStateProperty("second expected failure"), first);
            await PublishAsync(context, "passed", "AttachmentTests.Passed", PassedTestNodeStateProperty.CachedInstance, first);
            await PublishAsync(context, "skipped", "AttachmentTests.Skipped", new SkippedTestNodeStateProperty("expected skip"), second);
        }

        context.Complete();
    }

    private Task PublishAsync(ExecuteRequestContext context, string uid, string name, TestNodeStateProperty state, params string[] paths)
    {
        var properties = new PropertyBag(state);
        for (int i = 0; i < paths.Length; i++)
        {
            properties.Add(new FileArtifactProperty(new FileInfo(paths[i]), "Diagnostics " + i));
        }

        return context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid, new TestNode { Uid = uid, DisplayName = name, Properties = properties }));
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources.PatchTargetFrameworks(AttachmentTargetFramework)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion));
    }
}
