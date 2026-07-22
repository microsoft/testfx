// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// The compile oracle for the source-only <c>Microsoft.Testing.Platform.ServerClient.Source</c> package.
/// The anti-drift <see cref="MtpServerClientSourcePackageTests"/> inspects the <c>.nupkg</c> textually; this
/// test does the one thing text inspection cannot: it restores the packed package into a real, deliberately
/// hostile consumer and compiles it. The consumer mirrors the strictest real adopter (vstest):
/// <list type="bullet">
///   <item><c>ImplicitUsings=disable</c> — the injected source must carry its own using directives;</item>
///   <item>multi-targets <c>net462</c> (Windows) + <c>netstandard2.0</c> + <c>net8.0</c> — the down-level TFMs
///   need the shipped polyfills (init/required members, Index/Range, Ensure, ...) and the Jsonite JSON path,
///   while net8.0 exercises the in-box System.Text.Json path;</item>
///   <item><c>Nullable=enable</c> and <c>TreatWarningsAsErrors</c> — any warning in the injected source breaks
///   the build, exactly as it would in vstest.</item>
/// </list>
/// The consumer's single type references the whole client API surface, so a missing using, a missing polyfill,
/// or a name collision (for example the shipped <c>...Json.JsonSerializer</c> vs <c>System.Text.Json</c>)
/// becomes a compile error here rather than a surprise in a downstream repo. The restore is isolated to a
/// throwaway packages folder so a stale same-version (<c>2.4.0-dev</c>) cache can never produce a false pass.
/// The package must have been produced first (build with <c>-pack</c>); otherwise the test fails with a hint.
/// </summary>
[TestClass]
public sealed class MtpServerClientSourcePackageConsumerTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MtpServerClientSourceConsumer";
    private const string PackageId = "Microsoft.Testing.Platform.ServerClient.Source";

    private const string Sources = """
        #file HostileConsumer/HostileConsumer.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
            <OutputType>Library</OutputType>
            <!-- Hostile consumer: no implicit usings, nullable on, warnings are errors. -->
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <!-- The injected source uses modern C# (records, file-scoped namespaces). The down-level TFMs
                 would otherwise default to an old LangVersion; a real adopter (vstest) already builds latest. -->
            <LangVersion>preview</LangVersion>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform.ServerClient.Source" Version="$ServerClientSourceVersion$" />
          </ItemGroup>
        </Project>

        #file HostileConsumer/Consumer.cs
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Testing.Platform.ServerMode.Client;

        namespace HostileConsumer;

        // Never executed. It exists only to force the compiler to bind against every entry point of the
        // injected source-only client, so a missing using directive, a missing down-level polyfill, or a
        // leaked/colliding type in the packed source turns into a compile error here.
        internal static class Consumer
        {
            internal static async Task DriveAsync(string source, CancellationToken cancellationToken)
            {
                var logger = new DelegateMtpClientLogger(
                    (MtpClientLogLevel level, string message) => Console.WriteLine(level + ": " + message));

                var options = new MtpServerClientOptions
                {
                    ClientName = "HostileConsumer",
                    ClientVersion = "1.2.3",
                    DebuggerProvider = true,
                    IsStateful = true,
                    ConnectionTimeout = TimeSpan.FromSeconds(30),
                    Logger = logger,
                };
                options.EnvironmentVariables["EXAMPLE"] = "1";

                using IMtpServerClient client = MtpServerClient.Launch(source, options);

                client.TestNodesUpdated += OnTestNodesUpdated;
                client.LogReceived += (object? sender, MtpLogEventArgs e) => Console.WriteLine(e.Level + ": " + e.Message);
                client.TelemetryReceived += (object? sender, MtpTelemetryEventArgs e) => Console.WriteLine(e.EventName + ": " + e.Metrics.Count);
                client.AttachmentsReceived += (object? sender, MtpAttachmentsEventArgs e) => Console.WriteLine(e.Attachments.Count);
                client.ServerRequestHandler =
                    (string method, IDictionary<string, object?>? parameters, CancellationToken ct) => Task.FromResult<IDictionary<string, object?>?>(null);

                MtpServerCapabilities capabilities = await client.InitializeAsync(cancellationToken);
                Console.WriteLine(capabilities.ServerName ?? "unknown");
                Console.WriteLine(client.Capabilities?.MultiRequestSupport ?? false);
                Console.WriteLine(client.ProcessId);

                await client.DiscoverTestsAsync(cancellationToken);
                await client.DiscoverTestsAsync(new[] { "uid" }, cancellationToken);
                await client.DiscoverTestsWithFilterAsync("/*/*/*/*", cancellationToken);

                MtpRunResult runResult = await client.RunTestsAsync(cancellationToken);
                foreach (MtpAttachment attachment in runResult.Artifacts)
                {
                    Console.WriteLine(attachment.Uri + " " + attachment.DisplayName + " " + attachment.Producer + " " + attachment.Type + " " + attachment.Description);
                }

                await client.RunTestsAsync(new[] { "uid" }, cancellationToken);
                await client.RunTestsWithFilterAsync("/*/*/*/*", cancellationToken);
                await client.ExitAsync(cancellationToken);
            }

            private static void OnTestNodesUpdated(object? sender, MtpTestNodeUpdateEventArgs e)
            {
                Console.WriteLine(e.RunId);
                foreach (MtpTestNodeUpdate change in e.Changes)
                {
                    Console.WriteLine(change.Uid + " " + change.DisplayName + " " + change.NodeType + " " + change.ExecutionState);
                    Console.WriteLine(change.ErrorMessage + " " + change.ErrorStackTrace);
                    Console.WriteLine(change.DurationInMilliseconds);
                    Console.WriteLine(change.StandardOutput + " " + change.StandardError + " " + change.FilePath);
                    Console.WriteLine(change.LineStart);
                    Console.WriteLine(change.LineEnd);
                    Console.WriteLine(change.ParentUid);
                    Console.WriteLine(change.Node.Count);
                }
            }
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task HostileConsumer_CompilesAgainstPackedSource()
    {
        // net462 only builds on Windows; the down-level packed source is identical to what a netstandard2.0
        // consumer compiles, so netstandard2.0 covers the Jsonite/polyfill path on non-Windows too.
        string targetFrameworks = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "net462;netstandard2.0;net8.0"
            : "netstandard2.0;net8.0";

        string patchedSources = Sources
            .PatchCodeWithReplace("$TargetFrameworks$", targetFrameworks)
            .PatchCodeWithReplace("$ServerClientSourceVersion$", ResolveServerClientSourceVersion());

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(AssetName, patchedSources);

        // Restore into a throwaway packages folder so the freshly packed nupkg is always used — a stale
        // cached copy of the same 2.4.0-dev version would otherwise make this oracle pass vacuously.
        string isolatedPackages = Path.Combine(testAsset.TargetAssetPath, ".nuget-packages");
        var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = isolatedPackages,
        };

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}/HostileConsumer -c {Constants.BuildConfiguration}",
            environmentVariables: environmentVariables,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, $"The hostile consumer failed to compile the injected source. Build output:\n{result.StandardOutput}\n{result.StandardError}");
    }

    private static string ResolveServerClientSourceVersion()
    {
        const string prefix = PackageId + ".";
        const string extension = ".nupkg";

        // Require exactly one match: FirstOrDefault() over several lingering builds could compile against
        // an arbitrary stale version and defeat this compile oracle even with an isolated NuGet cache.
        string[] matches = Directory
            .GetFiles(Constants.ArtifactsPackagesShipping, prefix + "*" + extension, SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, name: Path.GetFileName(path)))
            // The prefix is unique to this package, but keep the digit check to be robust to future siblings.
            .Where(tuple => tuple.name.Length > prefix.Length && char.IsDigit(tuple.name[prefix.Length]))
            .Select(tuple => tuple.path)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one packed '{PackageId}' package in '{Constants.ArtifactsPackagesShipping}', " +
                $"but found {matches.Length}. Build with -pack first and clear any stale builds from the shipping folder.");
        }

        string fileName = Path.GetFileName(matches[0]);
        return fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
    }
}
