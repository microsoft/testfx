// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class MSBuildMultithreadingTests : AcceptanceTestBase<NopAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, DisplayName = "Generation tasks in a multiprocess build")]
    [DataRow(true, DisplayName = "Generation tasks in a multithreaded build")]
    public async Task GenerationTasks_PreserveProjectIsolationAndRunInProcess(bool multithreaded)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            nameof(GenerationTasks_PreserveProjectIsolationAndRunInProcess),
            SourceCode.PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{Path.Combine(asset.TargetAssetPath, "Generation.slnx")}\" -c Release -m:2 -mt:{multithreaded} -nr:false -v:normal \"-warnAsError:MSB4286;MSB4287;MSB4288\"",
            environmentVariables: new()
            {
                ["MSBUILDDISABLEFEATURESFROMVERSION"] = null,
                ["TESTINGPLATFORM_MSBUILD_LAUNCH_ATTACH_DEBUGGER"] = null,
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            },
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        SL.Build binlog = BinlogReader.Read(build.BinlogPath!);
        SL.Task[] generationTasks = binlog.FindChildrenRecursive<SL.Task>()
            .Where(task => task.Name is "TestingPlatformSelfRegisteredExtensions" or "ConfigurationFileTask")
            .ToArray();
        Assert.HasCount(4, generationTasks);

        if (multithreaded)
        {
            // MSBuild strict mode logs its sentinel once; any other occurrence reveals a process-relative path leak.
            // https://github.com/dotnet/msbuild/blob/main/documentation/specs/multithreading/thread-safe-tasks.md#validate-a-task-migration
            Assert.HasCount(
                1,
                binlog.FindChildrenRecursive<SL.Message>().Where(message => message.Text.Contains("MT-sentinel-CWD", StringComparison.Ordinal)));
        }

        using ZipArchive package = ZipFile.OpenRead(
            Path.Combine(Constants.ArtifactsPackagesShipping, $"Microsoft.Testing.Platform.MSBuild.{MicrosoftTestingPlatformVersion}.nupkg"));
        using Stream packedTask = package.GetEntry("_MSBuildTasks/netstandard2.0/Microsoft.Testing.Platform.MSBuild.dll")!.Open();
        byte[] expectedTaskHash = SHA256.HashData(packedTask);
        Assert.IsEmpty(generationTasks.Where(task => task.FindChildrenRecursive<SL.Message>()
            .Any(message => message.Text.Contains($"Launching task \"{task.Name}\"", StringComparison.Ordinal)))
            .Select(task => task.Name));
        foreach (SL.Task task in generationTasks)
        {
            using Stream taskAssembly = File.OpenRead(task.FromAssembly);
            Assert.AreSequenceEqual(expectedTaskHash, SHA256.HashData(taskAssembly));
        }

        foreach (string projectName in new[] { "Copy", "Merge" })
        {
            string projectDirectory = Path.Combine(asset.TargetAssetPath, projectName);
            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            string packagePath = assets.RootElement.GetProperty("libraries")
                .GetProperty($"Microsoft.Testing.Platform.MSBuild/{MicrosoftTestingPlatformVersion}").GetProperty("path").GetString()!;
            string[] resolvedTaskPaths = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
                .Select(folder => Path.GetFullPath(Path.Combine(
                    folder.Name, packagePath, "_MSBuildTasks", "netstandard2.0", "Microsoft.Testing.Platform.MSBuild.dll")))
                .ToArray();
            SL.Project project = binlog.FindChildrenRecursive<SL.Project>()
                .Single(candidate => candidate.ProjectFile == Path.Combine(projectDirectory, $"{projectName}.csproj")
                    && candidate.FindChildrenRecursive<SL.Task>().Any(task => task.Name == "ConfigurationFileTask"));
            SL.Task[] projectTasks = project.FindChildrenRecursive<SL.Task>()
                .Where(task => task.Name is "TestingPlatformSelfRegisteredExtensions" or "ConfigurationFileTask").ToArray();
            Assert.HasCount(2, projectTasks);
            foreach (SL.Task task in projectTasks)
            {
                Assert.Contains(
                    path => string.Equals(
                        path,
                        Path.GetFullPath(task.FromAssembly),
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
                    resolvedTaskPaths);
            }

            string source = File.ReadAllText(Path.Combine(projectDirectory, "obj", "Release", "net10.0", "SelfRegisteredExtensions.cs"));
            Assert.Contains($"global::Hooks.{projectName}Hook.AddExtensions(builder, args);", source);
            Assert.DoesNotContain($"global::Hooks.{(projectName == "Copy" ? "Merge" : "Copy")}Hook.AddExtensions", source);

            var host = TestInfrastructure.TestHost.LocateFrom(projectDirectory, projectName, "net10.0");
            TestHostResult execution = await host.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
            execution.AssertExitCodeIs(0);
            Assert.AreEqual($"hook={projectName}", execution.StandardOutput.Trim());

            using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(host.DirectoryName, $"{projectName}.testconfig.json")));
            Assert.AreEqual(projectName, configuration.RootElement.GetProperty("project").GetString());
            if (projectName == "Merge")
            {
                Assert.AreEqual(
                    "Merge-test",
                    configuration.RootElement.GetProperty("commandLineOptionDefaults").GetProperty("filter-uid").GetString());
            }
            else
            {
                Assert.IsFalse(configuration.RootElement.TryGetProperty("commandLineOptionDefaults", out _));
            }
        }
    }

    private const string SourceCode = """
        #file Generation.slnx
        <Solution>
          <Project Path="Copy/Copy.csproj" />
          <Project Path="Merge/Merge.csproj" />
        </Solution>

        #file Directory.Build.props
        <Project>
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <OutputType>Exe</OutputType>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>Generated</RootNamespace>
            <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
            <Compile Include="$(MSBuildThisFileDirectory)Program.cs" />
            <TestingPlatformBuilderHook Include="$(MSBuildProjectName)" DisplayName="$(MSBuildProjectName)" TypeFullName="Hooks.$(MSBuildProjectName)Hook" />
          </ItemGroup>
        </Project>

        #file Copy/Copy.csproj
        <Project Sdk="Microsoft.NET.Sdk" />

        #file Copy/testconfig.json
        {"project":"Copy"}

        #file Merge/Merge.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <TestingPlatformCommandLineOptionDefault Include="filter-uid" Value="Merge-test" />
          </ItemGroup>
        </Project>

        #file Merge/testconfig.json
        {"project":"Merge"}

        #file Program.cs
        using Generated;
        using Microsoft.Testing.Platform.Builder;

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args).ConfigureAwait(false);
        builder.AddSelfRegisteredExtensions(args);

        namespace Hooks
        {
            public static class CopyHook
            {
                public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
                    => Console.WriteLine("hook=Copy");
            }

            public static class MergeHook
            {
                public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
                    => Console.WriteLine("hook=Merge");
            }
        }
        """;
}
