// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class AzureDevOpsLivePublishingTests : AcceptanceTestBase<AzureDevOpsLivePublishingTests.TestAssetFixture>
{
    private const string AssetName = "AzureDevOpsLivePublishing";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeSdk_ParallelModulesPublishAllResultsAndPreserveFailureExitStatus(bool failingTests)
    {
        using TempDirectory directory = new();
        await using AzureDevOpsService service = new();
        service.AllowFirstCompletion.SetResult(true);
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        foreach (string module in new[] { "First", "Second", "Third" })
        {
            await directory.CopyDirectoryAsync(host.DirectoryName, Path.Combine(directory.Path, module), retainAttributes: !OperatingSystem.IsWindows());
        }

        // Optional overrides let the same packaged test exercise a servicing SDK without changing the
        // repository's toolchain or rebuilding its packages with that SDK.
        string muxer = Environment.GetEnvironmentVariable("TESTFX_AZDO_VALIDATION_DOTNET")
            ?? Path.Combine(RootFinder.Find(), ".dotnet", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        string? sdkVersion = Environment.GetEnvironmentVariable("TESTFX_AZDO_VALIDATION_SDK");
        string globalJson = sdkVersion is null
            ? """{"test":{"runner":"Microsoft.Testing.Platform"}}"""
            : JsonSerializer.Serialize(new { sdk = new { version = sdkVersion, rollForward = "disable" }, test = new { runner = "Microsoft.Testing.Platform" } });
        File.WriteAllText(Path.Combine(directory.Path, "global.json"), globalJson);
        Dictionary<string, string?> environment = new()
        {
            ["TF_BUILD"] = "true",
            ["SYSTEM_COLLECTIONURI"] = service.CollectionUri,
            ["SYSTEM_TEAMPROJECT"] = "project",
            ["SYSTEM_ACCESSTOKEN"] = "local-test-token",
            ["BUILD_BUILDID"] = "123",
            ["TESTINGPLATFORM_AZUREDEVOPS_TESTRUNID"] = null,
            ["TESTINGPLATFORM_AZUREDEVOPS_RESULTMAP"] = null,
            ["AZDO_MODULE"] = "C",
            ["AZDO_RENDEZVOUS"] = directory.Path,
            ["AZDO_FAIL"] = failingTests ? "true" : "false",
            ["DOTNET_ROOT"] = Path.GetDirectoryName(muxer),
        };
        TestInfrastructure.CommandLine command = new();
        int exitCode = await command.RunAsyncAndReturnExitCodeAsync(
            $"\"{muxer}\" test --test-modules \"{Path.Combine("*", AssetName + ".dll")}\" --max-parallel-test-modules 3 --publish-azdo-test-results --results-directory \"{directory.Path}\" --no-ansi --progress off",
            environmentVariables: environment,
            workingDirectory: directory.Path,
            cancellationToken: TestContext.CancellationToken);

        if (failingTests)
        {
            Assert.IsGreaterThan(0, exitCode, command.StandardOutput + command.ErrorOutput);
        }
        else
        {
            Assert.AreEqual((int)ExitCode.Success, exitCode, command.StandardOutput + command.ErrorOutput);
        }

        Assert.AreSequenceEqual(new[] { "C", "C", "C" }, service.TestNames);
        Assert.IsGreaterThanOrEqualTo(1, service.CompletedRunCount);
        Assert.AreEqual(service.CreatedRunCount, service.CompletedRunCount);
        Assert.AreEqual(0, service.RejectedResultCount);
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("1.0.0", false)]
    [DataRow("1.3.0", true)]
    public async Task ParallelModulesAndLateSuccessor_PublishEveryResultAndReportOversizedCoverage(string? protocolVersion, bool failingSuccessor)
    {
        using TempDirectory directory = new();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMinutes(2));
        CancellationToken token = cancellation.Token;
        await using AzureDevOpsService service = new();
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        async Task RunModuleAsync(string module)
        {
            Dictionary<string, string?> environment = new()
            {
                ["TF_BUILD"] = "true",
                ["SYSTEM_COLLECTIONURI"] = service.CollectionUri,
                ["SYSTEM_TEAMPROJECT"] = "project",
                ["SYSTEM_ACCESSTOKEN"] = "local-test-token",
                ["BUILD_BUILDID"] = "123",
                ["TESTINGPLATFORM_AZUREDEVOPS_TESTRUNID"] = null,
                ["TESTINGPLATFORM_AZUREDEVOPS_RESULTMAP"] = null,
                ["AZDO_MODULE"] = module,
                ["AZDO_RENDEZVOUS"] = directory.Path,
                ["AZDO_FAIL"] = module == "C" && failingSuccessor ? "true" : "false",
            };
            string arguments = $"--publish-azdo-test-results --results-directory \"{directory.Path}\"";
            ExitCode expectedExitCode = module == "C" && failingSuccessor ? ExitCode.AtLeastOneTestFailed : ExitCode.Success;
            if (protocolVersion is null)
            {
                TestHostResult result = await host.ExecuteAsync(arguments, environment, cancellationToken: token);
                result.AssertExitCodeIs(expectedExitCode);
                if (module == "B")
                {
                    result.AssertOutputContains("B.coverage");
                    result.AssertOutputContains("exceeds the limit");
                }
            }
            else
            {
                FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(host, arguments, environment, protocolVersion, cancellationToken: token);
                result.TestHostResult.AssertExitCodeIs(expectedExitCode);
                Assert.AreEqual(protocolVersion, result.NegotiatedProtocolVersion);
                string?[] warnings = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage)
                    .Select(message => DotnetTestPipeProtocol.DecodeDisplayMessageBody(message.Body))
                    .Where(message => message.Level == DotnetTestPipeProtocol.DisplayMessageLevels.Warning)
                    .Select(message => message.Text)];
                if (protocolVersion == "1.0.0")
                {
                    Assert.IsEmpty(warnings, "Legacy SDKs must not receive unsupported display messages.");
                }
                else if (module == "B")
                {
                    Assert.Contains(warning => warning is not null && warning.Contains("B.coverage") && warning.Contains("exceeds the limit"), warnings);
                }
            }
        }

        List<Task> modules = [];
        // Rendezvous needs overlapping hosts even on single-core agents. Serialize this class while
        // temporarily raising the shared infrastructure limit, and restore it after every child exits.
        int previousProcessLimit = TestInfrastructure.TestHost.MaxOutstandingExecutions;
        TestInfrastructure.TestHost.MaxOutstandingExecutions = Math.Max(3, previousProcessLimit);
        try
        {
            modules.Add(RunModuleAsync("A"));
            await WaitForFileAsync(Path.Combine(directory.Path, "A.ready"), token);
            modules.Add(RunModuleAsync("B"));
            await WaitForFileAsync(Path.Combine(directory.Path, "B.ready"), token);
            File.WriteAllText(Path.Combine(directory.Path, "A.release"), string.Empty);
            await service.FirstResultPublished.Task.WaitAsync(token);
            File.WriteAllText(Path.Combine(directory.Path, "B.release"), string.Empty);
            await service.FirstCompletionStarted.Task.WaitAsync(token);

            // Launch a real shipping test host while completion is held at the HTTP boundary.
            modules.Add(RunModuleAsync("C"));
            await WaitForFileAsync(Path.Combine(directory.Path, "C.started"), token);
            service.AllowFirstCompletion.TrySetResult(true);
            await Task.WhenAll(modules);

            Assert.AreSequenceEqual(new[] { "A", "B", "C" }, service.TestNames.OrderBy(name => name, StringComparer.Ordinal));
            Assert.AreEqual(2, service.CreatedRunCount);
            Assert.AreEqual(2, service.CompletedRunCount);
            Assert.AreEqual(1, service.AttachmentCount);
            Assert.AreEqual(0, service.RejectedResultCount);
            Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "azdo-runid.123.owner")));
            Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "azdo-runid.123.json")));
        }
        finally
        {
            service.AllowFirstCompletion.TrySetResult(true);
            await cancellation.CancelAsync();
            try
            {
                await Task.WhenAll(modules);
            }
            catch (OperationCanceledException)
            {
                // Release every child process before removing its rendezvous directory.
            }
            finally
            {
                TestInfrastructure.TestHost.MaxOutstandingExecutions = previousProcessLimit;
            }
        }
    }

    private static async Task WaitForFileAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(50, cancellationToken);
        }
    }

    private sealed class AzureDevOpsService : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _requests = [];
        private readonly ConcurrentDictionary<int, string> _states = new();
        private readonly ConcurrentBag<string> _testNames = [];
        private readonly Task _acceptLoop;
        private int _nextRunId;
        private int _attachmentCount;
        private int _rejectedResultCount;

        public AzureDevOpsService()
        {
            _listener.Start();
            CollectionUri = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
            _acceptLoop = AcceptAsync();
        }

        public string CollectionUri { get; }

        public TaskCompletionSource<bool> FirstResultPublished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> FirstCompletionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowFirstCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IEnumerable<string> TestNames => _testNames;

        public int CompletedRunCount => _states.Count(state => state.Value == "Completed");

        public int CreatedRunCount => _states.Count;

        public int AttachmentCount => _attachmentCount;

        public int RejectedResultCount => _rejectedResultCount;

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
                // Stopping an idle accept is expected.
            }

            try
            {
                await Task.WhenAll(_requests);
            }
            catch (OperationCanceledException)
            {
                // A deliberately held completion is canceled during cleanup.
            }
            finally
            {
                _listener.Stop();
                _cancellation.Dispose();
            }
        }

        private async Task AcceptAsync()
        {
            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                _requests.Add(HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                List<byte> headerBytes = [];
                byte[] next = new byte[1];
                while (true)
                {
                    await stream.ReadExactlyAsync(next, _cancellation.Token);
                    headerBytes.Add(next[0]);
                    if (headerBytes.Count >= 4 && headerBytes[^4] == 13 && headerBytes[^3] == 10 && headerBytes[^2] == 13 && headerBytes[^1] == 10)
                    {
                        break;
                    }

                    if (headerBytes.Count > 16 * 1024)
                    {
                        throw new InvalidOperationException("Unexpectedly large HTTP headers.");
                    }
                }

                string[] headers = Encoding.ASCII.GetString([.. headerBytes]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                string[] request = headers[0].Split(' ');
                string lengthHeader = headers.Single(header => header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                int length = int.Parse(lengthHeader["Content-Length:".Length..], CultureInfo.InvariantCulture);
                if (length is < 0 or > 1024 * 1024)
                {
                    throw new InvalidOperationException("Unexpected HTTP body length.");
                }

                byte[] body = new byte[length];
                await stream.ReadExactlyAsync(body, _cancellation.Token);
                using var json = JsonDocument.Parse(body);
                (int status, string payload) = await RespondAsync(request[0], request[1].Split('?')[0], json.RootElement);
                byte[] responseBody = Encoding.UTF8.GetBytes(payload);
                byte[] responseHeaders = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(responseHeaders, _cancellation.Token);
                await stream.WriteAsync(responseBody, _cancellation.Token);
            }
        }

        private async Task<(int Status, string Payload)> RespondAsync(string method, string path, JsonElement body)
        {
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 4 && method == "POST")
            {
                int id = Interlocked.Increment(ref _nextRunId);
                _states[id] = "InProgress";
                return (200, JsonSerializer.Serialize(new { id }));
            }

            int runId = int.Parse(segments[4], CultureInfo.InvariantCulture);
            if (segments.Length == 5 && method == "PATCH")
            {
                if (runId == 1)
                {
                    FirstCompletionStarted.TrySetResult(true);
                    await AllowFirstCompletion.Task.WaitAsync(_cancellation.Token);
                }

                _states[runId] = body.GetProperty("state").GetString()!;
                return (200, "{}");
            }

            if (segments.Length == 6 && segments[5] == "results" && method == "POST")
            {
                if (_states[runId] != "InProgress")
                {
                    Interlocked.Add(ref _rejectedResultCount, body.GetArrayLength());
                    return (400, """{"message":"The run is already completed."}""");
                }

                var results = body.EnumerateArray().Select((result, index) =>
                {
                    _testNames.Add(result.GetProperty("testCaseTitle").GetString()!);
                    return new { id = index + 1, automatedTestName = result.GetProperty("automatedTestName").GetString() };
                }).ToArray();
                FirstResultPublished.TrySetResult(true);
                return (200, JsonSerializer.Serialize(new { count = results.Length, value = results }));
            }

            if (segments.Length == 6 && segments[5] == "attachments" && method == "POST")
            {
                Interlocked.Increment(ref _attachmentCount);
                return (200, "{}");
            }

            throw new InvalidOperationException($"Unexpected Azure DevOps request: {method} {path}");
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file AzureDevOpsLivePublishing.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

string module = Environment.GetEnvironmentVariable("AZDO_MODULE")!;
string rendezvous = Environment.GetEnvironmentVariable("AZDO_RENDEZVOUS")!;
File.WriteAllText(Path.Combine(rendezvous, $"{module}.started"), string.Empty);
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new Framework(module, rendezvous));
builder.AddAzureDevOpsProvider();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

sealed class Framework(string module, string rendezvous) : ITestFramework, IDataProducer
{
    public string Uid => nameof(Framework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(Framework);
    public string Description => nameof(Framework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        File.WriteAllText(Path.Combine(rendezvous, $"{module}.ready"), string.Empty);
        if (module != "C")
        {
            while (!File.Exists(Path.Combine(rendezvous, $"{module}.release")))
            {
                await Task.Delay(50, context.CancellationToken);
            }
        }

        SessionUid session = new(module);
        IProperty state = Environment.GetEnvironmentVariable("AZDO_FAIL") == "true"
            ? new FailedTestNodeStateProperty(new InvalidOperationException("Intentional test failure"))
            : PassedTestNodeStateProperty.CachedInstance;
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(session, new TestNode
        {
            Uid = new TestNodeUid(module),
            DisplayName = module,
            Properties = new PropertyBag(state),
        }));
        if (module != "C")
        {
            string path = Path.Combine(rendezvous, $"{module}.coverage");
            using (FileStream file = File.OpenWrite(path))
            {
                file.SetLength(module == "B" ? 16L * 1024 * 1024 + 1 : 8);
            }
            await context.MessageBus.PublishAsync(this, new SessionFileArtifact(session, new FileInfo(path), "coverage"));
        }
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources.PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
