// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

[TestClass]
public sealed partial class FSharpAnalyzersTests(TestContext testContext)
{
    private const string PackageId = "MSTest.Analyzers.FSharp";
    private const string AnalyzerFile = "MSTest.Analyzers.FSharp.dll";
    private const string AnalyzerEntry = "analyzers/dotnet/fs/" + AnalyzerFile;

    private static int FindingsExitCode => OperatingSystem.IsWindows() ? -2 : 254;

    private static string PackagePath => Directory.GetFiles(Constants.ArtifactsPackagesShipping, PackageId + ".*.nupkg").Single();

    private static string PackageVersion
    {
        get
        {
            using ZipArchive package = ZipFile.OpenRead(PackagePath);
            using Stream manifest = package.GetEntry(PackageId + ".nuspec")!.Open();
            return XDocument.Load(manifest).Descendants().Single(element => element.Name.LocalName == "version").Value;
        }
    }

    [TestMethod]
    public void PackedAnalyzer_ContainsPluginAndDependenciesWithoutRuntimeAssets()
    {
        using ZipArchive package = ZipFile.OpenRead(PackagePath);
        string[] entries = [.. package.Entries.Select(entry => entry.FullName)];
        Assert.Contains(AnalyzerEntry, entries);
        Assert.Contains("analyzers/dotnet/fs/FSharp.Analyzers.SDK.dll", entries);
        Assert.Contains("analyzers/dotnet/fs/FSharp.Compiler.Service.dll", entries);
        Assert.Contains("analyzers/dotnet/fs/FSharp.Core.dll", entries);
        Assert.Contains("PACKAGE.md", entries);
        Assert.IsEmpty(entries.Where(entry => entry.StartsWith("lib/", StringComparison.Ordinal)));
        using Stream manifest = package.GetEntry(PackageId + ".nuspec")!.Open();
        Assert.IsEmpty(XDocument.Load(manifest).Descendants().Where(element => element.Name.LocalName == "dependency"));
    }

    [TestMethod]
    public async Task InvalidSignatures_ReportMSTEST0003()
    {
        const string source = """
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass; AbstractClass>]
type InvalidTests() =
    [<TestMethod>]
    static member StaticTest() = ()
    [<TestMethod>]
    member private _.PrivateTest() = ()
    [<TestMethod>]
    member _.IntegerTest() = 42
    [<TestMethod>]
    member _.AsyncTest() = async { return () }
    [<TestMethod>]
    member _.GenericTaskTest() = task { return () }
    [<TestMethod>]
    member _.GenericValueTaskTest() = ValueTask<int>(42)
    [<TestMethod>]
    member _.UninferableTest<'T>() = ()
    [<TestMethod>]
    abstract AbstractTest: unit -> unit

module ModuleTests =
    [<TestMethod>]
    let ModuleTest() = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, expectedExitCode: FindingsExitCode);
        string[] names = ["StaticTest", "PrivateTest", "IntegerTest", "AsyncTest", "GenericTaskTest", "GenericValueTaskTest", "UninferableTest", "AbstractTest", "ModuleTest"];
        Assert.HasCount(names.Length, diagnostics);
        foreach (string name in names)
        {
            JsonElement diagnostic = Assert.ContainsSingle(diagnostics.Where(diagnostic => diagnostic.GetProperty("message").GetProperty("text").GetString() == $"Test method '{name}' signature is invalid"));
            Assert.AreEqual("MSTEST0003", diagnostic.GetProperty("ruleId").GetString());
            AssertLocation(source, name, diagnostic);
        }
    }

    [TestMethod]
    public async Task ValidSignaturesAndAssertions_DoNotReportDiagnostics()
    {
        const string source = """
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass; Ignore("Tracked maintenance")>]
type ValidTests() =
    [<TestMethod>]
    member _.UnitTest() = ()
    [<TestMethod>]
    member _.TaskTest() : Task = task { return () } :> Task
    [<TestMethod>]
    member _.ValueTaskTest() = ValueTask()
    [<TestMethod>]
    abstract VirtualTest: unit -> unit
    default _.VirtualTest() = ()
    [<TestMethod>]
    abstract GenericVirtualTest<'T>: value: 'T -> unit
    default _.GenericVirtualTest<'T>(value: 'T) = ()
    [<TestMethod; DataRow(42)>]
    member _.GenericTest<'T>(value: 'T) = ()
    [<TestMethod; Ignore(IgnoreMessage = "Tracked maintenance")>]
    member _.Assertions() =
        let actual = System.DateTime.UtcNow.Second
        let expected = actual + 1
        Assert.AreEqual(42, actual)
        Assert.AreEqual(expected, actual)
        Assert.AreEqual(actual = actual, expected = 42)
        Assert.AreEqual(42, 42)
        Assert.AreNotEqual(0, actual)
        Assert.AreSame("expected", "actual")
        Assert.AreNotSame("expected", "actual")
""";
        Assert.IsEmpty(await AnalyzeAsync(source, expectedExitCode: 0));
    }

    [TestMethod]
    public async Task OverloadedVirtualSlots_OnlyUnimplementedSignatureIsInvalid()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; AbstractClass>]
type Tests() =
    [<TestMethod>]
    abstract OverloadedTest: value: int -> unit
    default _.OverloadedTest(value: int) = ()
    [<TestMethod>]
    abstract OverloadedTest: value: string -> unit
""";
        JsonElement diagnostic = Assert.ContainsSingle(await AnalyzeAsync(source, expectedExitCode: FindingsExitCode));
        Assert.AreEqual("MSTEST0003", diagnostic.GetProperty("ruleId").GetString());
        Assert.AreEqual("Test method 'OverloadedTest' signature is invalid", diagnostic.GetProperty("message").GetProperty("text").GetString());
        Assert.AreEqual(9, diagnostic.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region").GetProperty("startLine").GetInt32());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InternalVisibility_HonorsDiscoverInternals(bool discoverInternals)
    {
        string source = $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
{{(discoverInternals ? "[<assembly: DiscoverInternals>]\ndo ()" : string.Empty)}}
[<TestClass>]
type internal InternalTests() =
    [<TestMethod>]
    member _.InternalTypeTest() = ()
[<TestClass>]
type PublicTests() =
    [<TestMethod>]
    member internal _.InternalMemberTest() = ()
    [<TestMethod>]
    member private _.PrivateMemberTest() = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, expectedExitCode: FindingsExitCode);
        Assert.HasCount(discoverInternals ? 1 : 3, diagnostics);
        Assert.AreEqual(
            discoverInternals ? "Test method 'PrivateMemberTest' signature is invalid" : "Test method 'InternalTypeTest' signature is invalid",
            diagnostics[0].GetProperty("message").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task SwappedAssertions_ReportMSTEST0017()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Assertions() =
        let actual = System.DateTime.UtcNow.Second
        let expected = actual + 1
        let actualObject = obj()
        let observedType = System.DateTime.UtcNow.GetType()
        Assert.AreEqual(actual, 42)
        Assert.AreNotEqual(actual, 42)
        Assert.AreSame(actualObject, null)
        Assert.AreNotSame(actualObject, null)
        Assert.AreEqual(actual, expected)
        Assert.AreEqual(expected = actual, actual = 42)
        Assert.AreSame(observedType, typeof<System.DateTime>)
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, expectedExitCode: FindingsExitCode);
        Assert.HasCount(7, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0017", diagnostic.GetProperty("ruleId").GetString());
            Assert.AreEqual(
                "Assertion arguments should be passed in the correct order. 'actual' and 'expected'/'notExpected' arguments have been swapped.",
                diagnostic.GetProperty("message").GetProperty("text").GetString());
            Assert.IsGreaterThan(0, diagnostic.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region").GetProperty("startLine").GetInt32());
        }
    }

    [TestMethod]
    public async Task MissingIgnoreJustification_ReportMSTEST0066()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Ignore>]
type IgnoredClass() =
    [<TestMethod; Ignore>]
    member _.IgnoredTest() = ()
    [<TestMethod; Ignore("   ")>]
    member _.WhitespaceTest() = ()
    [<TestMethod; Ignore(null)>]
    member _.NullTest() = ()
    [<TestMethod; Ignore(IgnoreMessage = "")>]
    member _.EmptyNamedTest() = ()

type Helper() =
    [<Ignore>]
    member _.NotATest() = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, expectedExitCode: FindingsExitCode);
        string[] names = ["IgnoredClass", "IgnoredTest", "WhitespaceTest", "NullTest", "EmptyNamedTest"];
        Assert.HasCount(names.Length, diagnostics);
        foreach (string name in names)
        {
            JsonElement diagnostic = Assert.ContainsSingle(diagnostics.Where(diagnostic => diagnostic.GetProperty("message").GetProperty("text").GetString() == $"'[Ignore]' should specify a message explaining why '{name}' is ignored"));
            Assert.AreEqual("MSTEST0066", diagnostic.GetProperty("ruleId").GetString());
            AssertLocation(source, name, diagnostic);
        }
    }

    [TestMethod]
    public async Task UnrelatedTypes_DoNotReportDiagnostics()
    {
        const string source = """
namespace Other
open System
type TestClassAttribute() = inherit Attribute()
type TestMethodAttribute() = inherit Attribute()
type IgnoreAttribute() = inherit Attribute()
type Assert =
    static member AreEqual(expected: int, actual: int) = ()

[<TestClass; Ignore>]
type Tests() =
    [<TestMethod; Ignore>]
    static member Test() =
        let actual = DateTime.UtcNow.Second
        Assert.AreEqual(actual, 42)
""";
        Assert.IsEmpty(await AnalyzeAsync(source, expectedExitCode: 0));
    }

    [TestMethod]
    [DataRow("ignore-file")]
    [DataRow("ignore-line")]
    [DataRow("ignore-line-next")]
    [DataRow("ignore-region-start")]
    public async Task SuppressionComments_AreHonoredByHost(string suppression)
    {
        string source = $$"""
{{(suppression == "ignore-file" ? "// fsharpanalyzer: ignore-file MSTEST0017" : string.Empty)}}
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Assertions() =
        let actual = System.DateTime.UtcNow.Second
        {{(suppression is "ignore-line-next" or "ignore-region-start" ? $"// fsharpanalyzer: {suppression} MSTEST0017" : string.Empty)}}
        Assert.AreEqual(actual, 42) {{(suppression == "ignore-line" ? "// fsharpanalyzer: ignore-line MSTEST0017" : string.Empty)}}
        {{(suppression == "ignore-region-start" ? "// fsharpanalyzer: ignore-region-end" : string.Empty)}}
""";
        Assert.IsEmpty(await AnalyzeAsync(source, expectedExitCode: 0));
    }

    [TestMethod]
    public async Task EditorEntryPoint_HandlesCheckedPartialAndMissingTypedTrees()
    {
        const string program = """"
namespace EditorHost
open System
open System.IO
open System.Reflection
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text
open Microsoft.VisualStudio.TestTools.UnitTesting
open MSTest.Analyzers.FSharp

module Program =
    [<EntryPoint>]
    let main _ =
        async {
            let sample = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Ignore>]
    static member Invalid() =
        let actual = System.DateTime.UtcNow.Second
        Assert.AreEqual(actual, 42)
"""
            let samplePath = Path.Combine(Directory.GetCurrentDirectory(), "Sample.fs")
            File.WriteAllText(samplePath, sample)
            let checker = FSharpChecker.Create(keepAssemblyContents = true)
            let references =
                (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
                |> Array.map (fun path -> "-r:" + path)
            let options =
                checker.GetProjectOptionsFromCommandLineArgs(
                    samplePath + ".fsproj",
                    [| "--target:library"; "--targetprofile:netcore"; "--noframework"; yield! references; samplePath |])
            let! project = checker.ParseAndCheckProject(options)
            let errors = project.Diagnostics |> Array.filter (fun diagnostic -> diagnostic.Severity = FSharpDiagnosticSeverity.Error)
            if not (Array.isEmpty errors) then
                failwithf "Sample failed to type-check: %A" errors
            let text = SourceText.ofString sample
            let! parsed, answer = checker.ParseAndCheckFileInProject(samplePath, 0, text, options)
            let checkedFile =
                match answer with
                | FSharpCheckFileAnswer.Succeeded results -> results
                | FSharpCheckFileAnswer.Aborted -> failwith "Sample type-check was aborted."
            let context: EditorContext = {
                FileName = samplePath
                SourceText = text
                ParseFileResults = parsed
                CheckFileResults = Some checkedFile
                TypedTree = checkedFile.ImplementationFile
                CheckProjectResults = Some project
                ProjectOptions = AnalyzerProjectOptions.BackgroundCompilerOptions options
                AnalyzerIgnoreRanges = Map.empty
            }
            let! full = Analyzers.analyzeEditor context
            let codes = full |> List.map (fun message -> message.Code) |> List.sort
            if codes <> [ "MSTEST0003"; "MSTEST0017"; "MSTEST0066" ] then
                failwithf "Unexpected editor diagnostics: %A" full
            let! partial = Analyzers.analyzeEditor { context with CheckProjectResults = None }
            if partial <> full then
                failwithf "Partial editor snapshot changed diagnostics: %A" partial
            let! missing = Analyzers.analyzeEditor { context with TypedTree = None; CheckFileResults = None; CheckProjectResults = None }
            if not (List.isEmpty missing) then
                failwithf "Missing typed tree produced diagnostics: %A" missing
            let assembly = Assembly.Load("MSTest.Analyzers.FSharp")
            let entryPoints = assembly.GetType("MSTest.Analyzers.FSharp.Analyzers", true)
            if entryPoints.GetMethod("analyzeEditor").GetCustomAttributes(typeof<EditorAnalyzerAttribute>, false).Length <> 1
               || entryPoints.GetMethod("analyzeCli").GetCustomAttributes(typeof<CliAnalyzerAttribute>, false).Length <> 1 then
                failwith "The analyzer entry points are not registered for their respective hosts."
            printfn "EDITOR_VALIDATED:MSTEST0003,MSTEST0017,MSTEST0066;PARTIAL=3;MISSING_TYPED_TREE=0"
            return 0
        }
        |> Async.RunSynchronously
"""";
        using TestAsset asset = await GenerateConsumerAsync(program, editorHost: true);
        await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\"", cancellationToken: testContext.CancellationToken);
        await AssertPackedProvenanceAsync(asset.TargetAssetPath);
        using DotnetMuxer host = new();
        int exitCode = await host.ExecuteAsync(
            $"\"{Path.Combine(asset.TargetAssetPath, "bin", "Debug", "net10.0", "Consumer.dll")}\"",
            asset.TargetAssetPath,
            testContext.CancellationToken);
        Assert.AreEqual(0, exitCode, host.StandardOutput + host.StandardError);
        Assert.AreEqual(
            "EDITOR_VALIDATED:MSTEST0003,MSTEST0017,MSTEST0066;PARTIAL=3;MISSING_TYPED_TREE=0",
            host.StandardOutput.Trim());
    }

    private static async Task<TestAsset> GenerateConsumerAsync(string source, bool editorHost = false)
    {
        string frameworkPackage = Directory.GetFiles(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.*.nupkg")
            .Concat(Directory.GetFiles(Constants.ArtifactsTmpPackages, "MSTest.TestFramework.*.nupkg")).Single();
        using ZipArchive frameworkArchive = ZipFile.OpenRead(frameworkPackage);
        using Stream frameworkManifest = frameworkArchive.GetEntry("MSTest.TestFramework.nuspec")!.Open();
        string frameworkVersion = XDocument.Load(frameworkManifest).Descendants().Single(element => element.Name.LocalName == "version").Value;
        string coreVersion = typeof(FSharpAnalyzersTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "FSharpAnalyzersCoreVersion").Value!;
        string assetCode = $$"""
#file Consumer.fsproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateProgramFile>false</GenerateProgramFile>
    <NoWarn>$(NoWarn);57</NoWarn>
    {{(editorHost ? "<OutputType>Exe</OutputType><DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>" : string.Empty)}}
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Tests.fs" />
    <PackageReference Include="MSTest.TestFramework" Version="{{frameworkVersion}}" />
    <PackageReference Include="{{PackageId}}" Version="{{PackageVersion}}" PrivateAssets="all" IncludeAssets="analyzers" GeneratePathProperty="true" />
    {{(source.Contains("#file Legacy", StringComparison.Ordinal) ? "<ProjectReference Include=\"Legacy/Legacy.fsproj\" />" : string.Empty)}}
    {{(source.Contains("3.11.1", StringComparison.Ordinal) ? "<Reference Include=\"Microsoft.VisualStudio.TestPlatform.TestFramework\" HintPath=\"$(NuGetPackageRoot)\\mstest.testframework\\3.11.1\\lib\\net8.0\\Microsoft.VisualStudio.TestPlatform.TestFramework.dll\" />" : string.Empty)}}
    {{(editorHost ? $"""
    <PackageReference Include="FSharp.Core" Version="{coreVersion}" />
    <Reference Include="MSTest.Analyzers.FSharp" HintPath="$(PkgMSTest_Analyzers_FSharp)/{AnalyzerEntry}" />
    <Reference Include="FSharp.Analyzers.SDK" HintPath="$(PkgMSTest_Analyzers_FSharp)/analyzers/dotnet/fs/FSharp.Analyzers.SDK.dll" />
    <Reference Include="FSharp.Compiler.Service" HintPath="$(PkgMSTest_Analyzers_FSharp)/analyzers/dotnet/fs/FSharp.Compiler.Service.dll" />
    """ : string.Empty)}}
  </ItemGroup>
</Project>
#file Tests.fs
{{source}}
""";
        TestAsset asset = await TestAsset.GenerateAssetAsync("Consumer", assetCode, addPublicFeeds: true);
        await WriteRuleConfigurationAsync(asset.TargetAssetPath, ["MSTEST0003", "MSTEST0017", "MSTEST0066"]);
        return asset;
    }

    private async Task<JsonElement[]> AnalyzeAsync(string source, int expectedExitCode, string? rule = null, IReadOnlyDictionary<string, string>? settings = null, bool useDefaults = false)
    {
        using TestAsset asset = await GenerateConsumerAsync(source);
        if (rule is not null)
        {
            await WriteRuleConfigurationAsync(asset.TargetAssetPath, [rule], settings);
        }

        if (useDefaults)
        {
            File.Delete(Path.Combine(asset.TargetAssetPath, "mstest-analyzers.json"));
        }

        await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\"", cancellationToken: testContext.CancellationToken);

        string analyzerPath = await AssertPackedProvenanceAsync(asset.TargetAssetPath);
        string reportPath = Path.Combine(asset.TargetAssetPath, "diagnostics.sarif");
        (int exitCode, string standardOutput, string standardError) = await RunAnalyzerCliAsync(asset.TargetAssetPath, analyzerPath, reportPath);
        testContext.WriteLine(standardOutput);
        testContext.WriteLine(standardError);
        Assert.AreEqual(expectedExitCode, exitCode, standardOutput + standardError);
        Assert.DoesNotContain("Failed to", standardOutput + standardError);
        Assert.Contains("Registered 1 analyzers from 1 dlls", standardOutput + standardError);

        string reportDirectory = Path.Combine(Constants.Root, "artifacts", "TestResults", Constants.BuildConfiguration, "FSharpAnalyzers");
        Directory.CreateDirectory(reportDirectory);
        string reportArtifact = Path.Combine(reportDirectory, Guid.NewGuid().ToString("N") + ".sarif");
        File.Copy(reportPath, reportArtifact);
        testContext.AddResultFile(reportArtifact);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, testContext.CancellationToken));
        return [.. report.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().Select(result => result.Clone())];
    }

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAnalyzerCliAsync(string assetPath, string analyzerPath, string reportPath)
    {
        string cliPath = Path.Combine(AppContext.BaseDirectory, "FSharpAnalyzerCli", "FSharp.Analyzers.Cli.dll");
        Assert.IsTrue(File.Exists(cliPath), $"The pinned CLI was not staged at {cliPath}.");
        // The CLI loads MSBuild from the preview repository SDK, which targets a newer runtime than the tool.
        using DotnetMuxer cli = new(new Dictionary<string, string?>
        {
            ["DOTNET_ROLL_FORWARD"] = "LatestMajor",
            ["DOTNET_ROLL_FORWARD_TO_PRERELEASE"] = "1",
            ["TEMP"] = assetPath,
            ["TMP"] = assetPath,
            ["TMPDIR"] = assetPath,
            ["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES"),
            ["MSBUILDUSESERVER"] = "0",
        });
        int exitCode = await cli.ExecuteAsync(
            $"\"{cliPath}\" --project \"{Path.Combine(assetPath, "Consumer.fsproj")}\" --analyzers-path \"{analyzerPath}\" --report \"{reportPath}\" --treat-as-error \"MSTEST*\" --verbosity d",
            assetPath,
            testContext.CancellationToken);
        return (exitCode, cli.StandardOutput, cli.StandardError);
    }

    private static async Task WriteRuleConfigurationAsync(string directory, string[] enabledRules, IReadOnlyDictionary<string, string>? overrides = null)
    {
        var settings = Enumerable.Range(1, 89).ToDictionary(index => $"MSTEST{index:D4}", _ => (object)"none");
        foreach (string rule in enabledRules)
        {
            settings[rule] = "warning";
        }

        settings["PublishAot"] = enabledRules.Contains("MSTEST0072");
        settings["RunAOTCompilation"] = enabledRules.Contains("MSTEST0072");
        settings["MSTestHostTestClassInjectionEnabled"] = enabledRules.Contains("MSTEST0088");
        settings["IsMSTestTestAdapterReferenced"] = true;
        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string> setting in overrides)
            {
                settings[setting.Key] = setting.Value;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "mstest-analyzers.json"), JsonSerializer.Serialize(settings));
    }

    private async Task<string> AssertPackedProvenanceAsync(string assetPath)
    {
        string assetsPath = Path.Combine(assetPath, "obj", "project.assets.json");
        using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(assetsPath, testContext.CancellationToken));
        string packageKey = PackageId + "/" + PackageVersion;
        string relativePath = assets.RootElement.GetProperty("libraries").GetProperty(packageKey).GetProperty("path").GetString()!;
        string packageRoot = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, relativePath))
            .Single(path => File.Exists(Path.Combine(path, AnalyzerEntry)));
        string restoredAnalyzer = Path.Combine(packageRoot, AnalyzerEntry);
        using ZipArchive package = ZipFile.OpenRead(PackagePath);
        using Stream packedAnalyzer = package.GetEntry(AnalyzerEntry)!.Open();
        using FileStream restoredFile = File.OpenRead(restoredAnalyzer);
        string packedHash = Convert.ToHexString(await SHA256.HashDataAsync(packedAnalyzer, testContext.CancellationToken));
        string restoredHash = Convert.ToHexString(await SHA256.HashDataAsync(restoredFile, testContext.CancellationToken));
        Assert.AreEqual(packedHash, restoredHash);

        foreach (JsonProperty target in assets.RootElement.GetProperty("targets").EnumerateObject())
        {
            JsonElement analyzer = target.Value.GetProperty(packageKey);
            Assert.IsFalse(analyzer.TryGetProperty("compile", out _), "The plugin must not become a consumer compile reference.");
            Assert.IsFalse(analyzer.TryGetProperty("runtime", out _), "The plugin must not become a consumer runtime dependency.");
        }

        testContext.WriteLine($"Packed analyzer: {PackagePath}; restored analyzer: {restoredAnalyzer}; DLL SHA256: {restoredHash}");
        return Path.GetDirectoryName(restoredAnalyzer)!;
    }

    private static void AssertLocation(string source, string name, JsonElement diagnostic)
    {
        Match identifier = Regex.Match(source, $@"\b{Regex.Escape(name)}\b");
        Assert.IsTrue(identifier.Success, $"Expected '{name}' in the test source.");
        int offset = identifier.Index;
        int expectedLine = source[..offset].Count(character => character == '\n') + 1;
        JsonElement location = diagnostic.GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.EndsWith("Tests.fs", location.GetProperty("artifactLocation").GetProperty("uri").GetString()!);
        Assert.AreEqual(expectedLine, location.GetProperty("region").GetProperty("startLine").GetInt32());
        Assert.AreEqual(name.Length, location.GetProperty("region").GetProperty("endColumn").GetInt32() - location.GetProperty("region").GetProperty("startColumn").GetInt32());
    }
}
