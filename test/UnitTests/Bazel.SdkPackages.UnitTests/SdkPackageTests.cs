// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bazel.SdkPackages.UnitTests;

[TestClass]
public sealed class SdkPackageTests(TestContext context)
{
    [TestMethod]
    public async Task Generate_SdkPreservesTemplatesAndComponentVersions()
    {
        JsonObject input = CreateInput(sdk: true);

        (int exitCode, string output) = await RunAsync(input);

        Assert.AreEqual(0, exitCode, output);
        Assert.AreEqual("""
            <Project>
              <PropertyGroup Condition="'$(Override)' == ''">
                <MSTest>7.8.9-preview.bazel</MSTest>
                <Windows>7.8.9-preview.bazel</Windows>
                <Platform>2.3.4-preview.bazel</Platform>
                <Coverage>18.13.0-preview.42</Coverage>
                <TestSdk>18.10.1</TestSdk>
                <Fakes>18.1.1</Fakes>
                <Playwright>1.64.1</Playwright>
                <Aspire>13.5.3</Aspire>
              </PropertyGroup>
            </Project>
            """.ReplaceLineEndings(), File.ReadAllText(PathFor("generated.props")).ReplaceLineEndings());
        Assert.AreEqual("""
            <Project><Import Project="$(MSBuildThisFileDirectory)NativeAOT.targets" Condition="'$(PublishAot)' == 'true'" /></Project>
            """, File.ReadAllText(PathFor("generated.targets")));
        Assert.AreEqual("7.8.9-preview.bazel", File.ReadAllText(PathFor("version.txt")));
        using JsonDocument request = JsonDocument.Parse(File.ReadAllText(PathFor("pack.json")));
        JsonElement metadata = request.RootElement.GetProperty("metadata");
        Assert.AreEqual("MSTest.Sdk", metadata.GetProperty("id").GetString());
        Assert.AreEqual("""
            Purpose first.

            Shared product.
            """.ReplaceLineEndings(), metadata.GetProperty("description").GetString()!.ReplaceLineEndings());
        Assert.AreEqual("PACKAGE.md", metadata.GetProperty("readme").GetString());
        Assert.AreEqual("MIT", metadata.GetProperty("license").GetProperty("value").GetString());
        Assert.IsTrue(metadata.GetProperty("requireLicenseAcceptance").GetBoolean());
        Assert.AreSequenceEqual(new[] { "Microsoft", "testing", "SDK" }, metadata.GetProperty("tags").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.StartsWith("Notes for 7.8.9.", metadata.GetProperty("releaseNotes").GetString());
        Assert.HasCount(0, request.RootElement.GetProperty("dependencyGroups").EnumerateArray().ToArray());
        Assert.AreSequenceEqual(
            new[] { "PACKAGE.md", "Sdk/Sdk.props", "Sdk/Runner/Runner.targets" },
            request.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetProperty("target").GetString()).ToArray(),
            SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public async Task Generate_MetaPackagePreservesDependencyAndAssetFlow()
    {
        JsonObject input = CreateInput(sdk: false);

        (int exitCode, string output) = await RunAsync(input);

        Assert.AreEqual(0, exitCode, output);
        Assert.AreEqual("7.8.9-preview.bazel", File.ReadAllText(PathFor("version.txt")));
        using JsonDocument request = JsonDocument.Parse(File.ReadAllText(PathFor("pack.json")));
        Assert.AreEqual("MSTest", request.RootElement.GetProperty("metadata").GetProperty("id").GetString());
        JsonElement[] groups = request.RootElement.GetProperty("dependencyGroups").EnumerateArray().ToArray();
        Assert.AreSequenceEqual(new[] { "net8.0", "net9.0", "net10.0", "net11.0" }, groups.Select(group => group.GetProperty("targetFramework").GetString()).ToArray());
        foreach (JsonElement group in groups)
        {
            Dictionary<string, JsonElement> dependencies = group.GetProperty("dependencies").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetString()!);
            Assert.AreSequenceEqual(
                new[] { "MSTest.TestFramework", "MSTest.TestAdapter", "Microsoft.Testing.Extensions.TrxReport", "Microsoft.NET.Test.Sdk", "Microsoft.Testing.Extensions.CodeCoverage" },
                dependencies.Keys.ToArray(),
                SequenceOrder.InAnyOrder);
            Assert.AreEqual("7.8.9-preview.bazel", dependencies["MSTest.TestFramework"].GetProperty("version").GetString());
            Assert.AreEqual("7.8.9-preview.bazel", dependencies["MSTest.TestAdapter"].GetProperty("version").GetString());
            Assert.AreEqual("2.3.4-preview.bazel", dependencies["Microsoft.Testing.Extensions.TrxReport"].GetProperty("version").GetString());
            Assert.AreEqual("18.10.1", dependencies["Microsoft.NET.Test.Sdk"].GetProperty("version").GetString());
            Assert.AreEqual("18.13.0-preview.42", dependencies["Microsoft.Testing.Extensions.CodeCoverage"].GetProperty("version").GetString());
            foreach ((string id, JsonElement dependency) in dependencies)
            {
                string[] expected = id == "Microsoft.NET.Test.Sdk" ? [] : ["Build", "Analyzers"];
                Assert.AreSequenceEqual(expected, dependency.GetProperty("exclude").EnumerateArray().Select(item => item.GetString()).ToArray(), id);
            }
        }

        Assert.AreSequenceEqual(
            new[] { "PACKAGE.md", "lib/net8.0/_._", "lib/net9.0/_._", "lib/net10.0/_._", "lib/net11.0/_._" },
            request.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetProperty("target").GetString()).ToArray(),
            SequenceOrder.InAnyOrder);
        Assert.IsFalse(File.Exists(PathFor("generated.props")));
    }

    [TestMethod]
    [DataRow("unknown-template", "Unknown SDK template property")]
    [DataRow("malformed-template", "Unresolved SDK template token")]
    [DataRow("duplicate-version", "Expected exactly one unconditional VersionPrefix")]
    [DataRow("missing-version", "Expected exactly one unconditional VersionPrefix")]
    [DataRow("central-drift", "differs from the central Aspire.Hosting.Testing")]
    [DataRow("missing-asset", "Native package files do not match")]
    [DataRow("sdk-dependencies", "suppress NuGet dependencies")]
    [DataRow("framework-drift", "differs from SupportedNetFrameworks")]
    [DataRow("meta-private-assets", "test SDK build-asset flow changed")]
    [DataRow("meta-reference", "Unsupported meta-package project reference")]
    public async Task Generate_RejectsContractDriftBeforeWritingOutputs(string scenario, string expectedError)
    {
        JsonObject input = CreateInput(sdk: !scenario.StartsWith("meta-", StringComparison.Ordinal));
        switch (scenario)
        {
            case "unknown-template":
                ReplaceInFile(PathFor("sdk.props.template"), "${MSTestVersion}", "${Unknown}");
                break;
            case "malformed-template":
                ReplaceInFile(PathFor("sdk.props.template"), "${MSTestVersion}", "${MSTestVersion");
                break;
            case "duplicate-version":
                ReplaceInFile(PathFor("Versions.props"), "<VersionPrefix>7.8.9</VersionPrefix>", "<VersionPrefix>7.8.9</VersionPrefix><VersionPrefix>1.0.0</VersionPrefix>");
                break;
            case "missing-version":
                ReplaceInFile(PathFor("Versions.props"), "<VersionPrefix>7.8.9</VersionPrefix>", "");
                break;
            case "central-drift":
                ReplaceInFile(PathFor("Directory.Packages.props"), "Include=\"Aspire.Hosting.Testing\" Version=\"13.5.3\"", "Include=\"Aspire.Hosting.Testing\" Version=\"13.5.4\"");
                break;
            case "missing-asset":
                input["files"]!.AsArray().RemoveAt(0);
                break;
            case "sdk-dependencies":
                ReplaceInFile(PathFor("Package.csproj"), "<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>", "<SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>");
                break;
            case "framework-drift":
                ReplaceInFile(PathFor("Directory.Build.props"), "net11.0", "net12.0");
                break;
            case "meta-private-assets":
                ReplaceInFile(PathFor("Package.csproj"), "PrivateAssets=\"none\"", "PrivateAssets=\"all\"");
                break;
            case "meta-reference":
                ReplaceInFile(PathFor("Package.csproj"), "MSTest.TestAdapter.csproj", "Unknown.csproj");
                break;
            default:
                Assert.Fail($"Unknown scenario: {scenario}");
                break;
        }

        (int exitCode, string output) = await RunAsync(input);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains(expectedError, output);
        Assert.IsFalse(File.Exists(PathFor("version.txt")));
        Assert.IsFalse(File.Exists(PathFor("pack.json")));
        Assert.IsFalse(File.Exists(PathFor("generated.props")));
    }

    private JsonObject CreateInput(bool sdk)
    {
        string versions = Write("Versions.props", """
            <Project><PropertyGroup>
              <VersionPrefix>7.8.9</VersionPrefix>
              <TestingPlatformVersionPrefix>2.3.4</TestingPlatformVersionPrefix>
              <MicrosoftTestingExtensionsCodeCoverageVersion>18.13.0-preview.42</MicrosoftTestingExtensionsCodeCoverageVersion>
            </PropertyGroup></Project>
            """);
        string repositoryProperties = Write("Directory.Build.props", """
            <Project><PropertyGroup>
              <SupportedNetFrameworks>net8.0;net9.0;net10.0;net11.0</SupportedNetFrameworks>
              <PackageLicenseExpression>MIT</PackageLicenseExpression>
            </PropertyGroup></Project>
            """);
        string sourceProperties = Write("Source.props", """
            <Project><PropertyGroup>
              <CommonPackageTags>Microsoft testing</CommonPackageTags>
              <CommonProductDescription>Shared product.</CommonProductDescription>
              <PackageReleaseNotes>Notes for $(VersionPrefix).</PackageReleaseNotes>
            </PropertyGroup></Project>
            """);
        string centralPackages = Write("Directory.Packages.props", """
            <Project><PropertyGroup>
              <MicrosoftNETTestSdkVersion>18.10.1</MicrosoftNETTestSdkVersion>
              <MicrosoftTestingExtensionsFakesVersion>18.1.1</MicrosoftTestingExtensionsFakesVersion>
              <MicrosoftPlaywrightVersion>1.64.1</MicrosoftPlaywrightVersion>
              <AspireHostingTestingVersion>13.5.3</AspireHostingTestingVersion>
            </PropertyGroup><ItemGroup>
              <PackageVersion Include="Aspire.Hosting.Testing" Version="13.5.3" />
              <PackageVersion Include="Microsoft.Playwright.MSTest.v4" Version="1.64.1" />
            </ItemGroup></Project>
            """);
        string references = sdk ? """
            <None Pack="true" PackagePath="Sdk\Sdk.props" />
            <None Pack="true" PackagePath="Sdk\Runner\Runner.targets" />
            """ : """
            <ProjectReference Include="$(RepoRoot)src\Adapter\MSTest.TestAdapter\MSTest.TestAdapter.csproj" Condition="'$(TargetFramework)' != '$(UwpMinimum)'" />
            <ProjectReference Include="$(RepoRoot)src\TestFramework\MSTest.Uwp\MSTest.Uwp.csproj" Condition="'$(TargetFramework)' == '$(UwpMinimum)'" />
            <ProjectReference Include="$(RepoRoot)src\TestFramework\TestFramework\TestFramework.csproj" />
            <ProjectReference Include="$(RepoRoot)src\Platform\Microsoft.Testing.Extensions.TrxReport\Microsoft.Testing.Extensions.TrxReport.csproj" Condition="'$(TargetFramework)' != '$(UwpMinimum)'" />
            <PackageReference Include="Microsoft.NET.Test.Sdk" VersionOverride="$(MicrosoftNETTestSdkVersion)" Condition="'$(TargetFramework)' != '$(UwpMinimum)'" PrivateAssets="none" />
            <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
            """;
        string id = sdk ? "MSTest.Sdk" : "MSTest";
        string project = Write("Package.csproj", $"""
            <Project><PropertyGroup>
              <PackageId>{id}</PackageId>
              <IncludeBuildOutput>false</IncludeBuildOutput>
              <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
              <PackageTags>$(CommonPackageTags) SDK</PackageTags>
              <PackageDescription><![CDATA[Purpose first.

            $(CommonProductDescription)]]></PackageDescription>
            </PropertyGroup><ItemGroup>{references}</ItemGroup></Project>
            """);
        var files = new List<object> { new { source = Write("PACKAGE.md", "# Package"), target = "PACKAGE.md" } };
        var templates = new List<object>();
        if (sdk)
        {
            string template = Write("sdk.props.template", """
                <Project>
                  <PropertyGroup Condition="'$(Override)' == ''">
                    <MSTest>${MSTestVersion}</MSTest>
                    <Windows>${MSTestWindowsUIAutomationVersion}</Windows>
                    <Platform>${MicrosoftTestingPlatformVersion}</Platform>
                    <Coverage>${MicrosoftTestingExtensionsCodeCoverageVersion}</Coverage>
                    <TestSdk>${MicrosoftNETTestSdkVersion}</TestSdk>
                    <Fakes>${MicrosoftTestingExtensionsFakesVersion}</Fakes>
                    <Playwright>${MicrosoftPlaywrightVersion}</Playwright>
                    <Aspire>${AspireHostingTestingVersion}</Aspire>
                  </PropertyGroup>
                </Project>
                """);
            string runner = Write("runner.targets.template", """
                <Project><Import Project="$(MSBuildThisFileDirectory)NativeAOT.targets" Condition="'$(PublishAot)' == 'true'" /></Project>
                """);
            templates.Add(new { source = template, output = PathFor("generated.props") });
            templates.Add(new { source = runner, output = PathFor("generated.targets") });
            files.Add(new { source = PathFor("generated.props"), target = "Sdk/Sdk.props" });
            files.Add(new { source = PathFor("generated.targets"), target = "Sdk/Runner/Runner.targets" });
        }
        else
        {
            string placeholder = Write("_._", "");
            files.AddRange(new[] { "net8.0", "net9.0", "net10.0", "net11.0" }.Select(framework => new { source = placeholder, target = $"lib/{framework}/_._" }));
        }

        return JsonSerializer.SerializeToNode(new
        {
            packageId = id,
            project,
            versions,
            repositoryProperties,
            sourceProperties,
            centralPackages,
            frameworks = new[] { "net8.0", "net9.0", "net10.0", "net11.0" },
            templates,
            files,
            output = PathFor("package.nupkg"),
            nuspecOutput = PathFor("package.nuspec"),
            versionFile = PathFor("version.txt"),
            requestOutput = PathFor("pack.json"),
        })!.AsObject();
    }

    private static void ReplaceInFile(string path, string oldValue, string newValue)
        => File.WriteAllText(path, File.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal));

    private string PathFor(string name)
    {
        string? directory = context.TestTempDirectory;
        Assert.IsNotNull(directory);
        return Path.Combine(directory, name);
    }

    private string Write(string name, string content)
    {
        string path = PathFor(name);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<(int exitCode, string output)> RunAsync(JsonObject input)
    {
        string manifest = Write("inputs.json", input.ToJsonString());
        string location = Environment.GetEnvironmentVariable("TESTFX_BAZEL_SDK_PACKAGE_TOOL")
            ?? throw new InvalidOperationException("The SDK package tool location was not supplied.");
        string tool = global::Bazel.Runfiles.Create().Rlocation(location);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("exec");
        process.StartInfo.ArgumentList.Add(tool);
        process.StartInfo.ArgumentList.Add(manifest);
        Assert.IsTrue(process.Start());
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cleanup.Token);
            }
        }

        return (process.ExitCode, await output + await error);
    }
}
