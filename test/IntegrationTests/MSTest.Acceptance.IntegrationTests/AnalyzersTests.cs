// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AnalyzersTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CA1515_OnlyPublicTestClassesAreSuppressed(bool includeHelper)
    {
        string code = $$"""
            #file PublicTestClasses.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFrameworks$</TargetFramework>
                <OutputType>Exe</OutputType>
                <RunAnalyzers>true</RunAnalyzers>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.Analyzers" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
              </ItemGroup>
            </Project>

            #file .editorconfig
            root = true
            [*.cs]
            dotnet_diagnostic.CA1515.severity = warning

            #file Program.cs
            internal static class Program
            {
                private static void Main() { }
            }

            #file TestCases.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
            [TestClass]
            public sealed class TestCases
            {
                [TestMethod]
                public void Test() { }
            }

            #file Helper.cs
            {{(includeHelper ? "public class Helper { }" : string.Empty)}}
            """
            .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(CA1515_OnlyPublicTestClassesAreSuppressed), code);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\"",
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(includeHelper ? 1 : 0, result.ExitCode, result.StandardOutput);
        result.AssertOutputDoesNotContain("TestCases.cs(");
        if (includeHelper)
        {
            result.AssertOutputContains("Helper.cs(1,14): error CA1515");
        }
    }

    [TestMethod]
    public async Task PackagedAnalyzersAndCodeFixesRunOnMinimumRoslynHost()
    {
        string code = """"
            #file MinimumRoslynHost.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFrameworks$</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RunAnalyzers>false</RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.Analyzers" Version="$MSTestVersion$" GeneratePathProperty="true" ExcludeAssets="all" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" ExcludeAssets="analyzers" />
                <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.14.0" />
                <Reference Include="MSTest.Analyzers" HintPath="$(PkgMSTest_Analyzers)\analyzers\dotnet\cs\MSTest.Analyzers.dll" />
                <Reference Include="MSTest.Analyzers.CodeFixes" HintPath="$(PkgMSTest_Analyzers)\analyzers\dotnet\cs\MSTest.Analyzers.CodeFixes.dll" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using System.Collections.Immutable;
            using Microsoft.CodeAnalysis;
            using Microsoft.CodeAnalysis.CodeActions;
            using Microsoft.CodeAnalysis.CodeFixes;
            using Microsoft.CodeAnalysis.CSharp;
            using Microsoft.CodeAnalysis.Diagnostics;
            using Microsoft.CodeAnalysis.Formatting;
            using Microsoft.CodeAnalysis.Text;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using MSTest.Analyzers;

            Version minimumHostVersion = new(4, 14, 0, 0);
            Assert.AreEqual(minimumHostVersion, typeof(Compilation).Assembly.GetName().Version);
            Assert.AreEqual(minimumHostVersion, typeof(Workspace).Assembly.GetName().Version);
            Assert.AreEqual(minimumHostVersion, typeof(CSharpCompilation).Assembly.GetName().Version);
            Assert.AreEqual(minimumHostVersion, typeof(AvoidUsingAssertsInAsyncVoidContextAnalyzer).Assembly
                .GetReferencedAssemblies().Single(reference => reference.Name == "Microsoft.CodeAnalysis").Version);
            Assert.AreEqual(minimumHostVersion, typeof(AvoidUsingAssertsInAsyncVoidContextFixer).Assembly
                .GetReferencedAssemblies().Single(reference => reference.Name == "Microsoft.CodeAnalysis.Workspaces").Version);

            MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
            const string source = """
                global using System;
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public class Tests
                {
                    public async void Run()
                    {
                        Assert.Fail();
                    }
                }
                """;
            using var workspace = new AdhocWorkspace();
            Project project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "MinimumHost",
                "MinimumHost",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: references));
            Document document = workspace.AddDocument(project.Id, "Tests.cs", SourceText.From(source));
            Compilation compilation = (await document.Project.GetCompilationAsync())!;
            Assert.IsEmpty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            var analyzer = new AvoidUsingAssertsInAsyncVoidContextAnalyzer();
            ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
            Assert.HasCount(1, diagnostics);
            Assert.AreEqual("MSTEST0040", diagnostics[0].Id);
            Assert.AreEqual("Assert.Fail()", source.Substring(diagnostics[0].Location.SourceSpan.Start, diagnostics[0].Location.SourceSpan.Length));

            var actions = new List<CodeAction>();
            await new AvoidUsingAssertsInAsyncVoidContextFixer().RegisterCodeFixesAsync(new CodeFixContext(
                document, diagnostics[0], (action, _) => actions.Add(action), CancellationToken.None));
            Assert.HasCount(1, actions);
            ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(CancellationToken.None);
            ApplyChangesOperation change = operations.OfType<ApplyChangesOperation>().Single();
            Document fixedDocument = await Formatter.FormatAsync(change.ChangedSolution.GetDocument(document.Id)!);
            const string expected = """
                global using System;
                using System.Threading.Tasks;
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public class Tests
                {
                    public async Task Run()
                    {
                        Assert.Fail();
                    }
                }
                """;
            Assert.AreEqual(expected.ReplaceLineEndings(), (await fixedDocument.GetTextAsync()).ToString().ReplaceLineEndings());
            Compilation fixedCompilation = (await fixedDocument.Project.GetCompilationAsync())!;
            Assert.IsEmpty(fixedCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            Assert.IsEmpty(await fixedCompilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync());

            Console.WriteLine("Roslyn 4.14: packaged C# analyzer and code fix passed.");
            """"
            .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(PackagedAnalyzersAndCodeFixesRunOnMinimumRoslynHost), code);
        await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            cancellationToken: TestContext.CancellationToken);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {testAsset.TargetAssetPath} --no-build",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode);
        result.AssertOutputContains("Roslyn 4.14: packaged C# analyzer and code fix passed.");
    }

    [TestMethod]
    public async Task PackagedQueryableAssertionsUseNativeOverloads()
    {
        string code = $$"""
            #file QueryableAssertions.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFrameworks$</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RunAnalyzers>true</RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.Analyzers" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
              </ItemGroup>
            </Project>

            #file .editorconfig
            root = true
            [*.cs]
            dotnet_diagnostic.MSTEST0090.severity = warning

            #file Program.cs
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal static class Program
            {
                private static void Main()
                {
                    IQueryable<int> query = new[] { 1, 2 }.AsQueryable();
                    Assert.HasCount(2, query);
                    Assert.IsEmpty(query.Where(value => value < 0));
                    Assert.IsNotEmpty(query);

                    Assert.HasCount(2, query.AsEnumerable());
                    Assert.IsEmpty((IEnumerable<int>)query.Where(value => value < 0));
                    Assert.IsNotEmpty(query.AsEnumerable());
                    Assert.ContainsSingle(new[] { 1 }.AsQueryable());
                }
            }
            """
            .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(PackagedQueryableAssertionsUseNativeOverloads), code);
        string binlogPath = Path.Combine(testAsset.TargetAssetPath, "build.binlog");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -bl:\"{binlogPath}\"",
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        string output = result.StandardOutput + result.StandardError;
        Assert.AreEqual(0, result.ExitCode, output);
        string[] diagnostics = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("warning MSTEST0090", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(4, diagnostics, output);
        Assert.HasCount(1, diagnostics.Where(line => line.Contains("'Assert.HasCount'", StringComparison.Ordinal)).ToArray(), output);
        Assert.HasCount(1, diagnostics.Where(line => line.Contains("'Assert.IsEmpty'", StringComparison.Ordinal)).ToArray(), output);
        Assert.HasCount(1, diagnostics.Where(line => line.Contains("'Assert.IsNotEmpty'", StringComparison.Ordinal)).ToArray(), output);
        Assert.HasCount(1, diagnostics.Where(line => line.Contains("'Assert.ContainsSingle'", StringComparison.Ordinal)).ToArray(), output);

        DotnetMuxerResult runResult = await DotnetCli.RunAsync(
            $"run --project \"{testAsset.TargetAssetPath}\" --no-build",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, runResult.ExitCode, runResult.StandardOutput + runResult.StandardError);
    }

    [TestMethod]
    public async Task PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases()
    {
        string code = """
            #file AnalyzerOpportunities.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFrameworks$</TargetFramework>
                <RunAnalyzers>true</RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.Analyzers" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
              </ItemGroup>
            </Project>

            #file .editorconfig
            root = true
            [*.cs]
            dotnet_diagnostic.MSTEST0002.severity = warning
            dotnet_diagnostic.MSTEST0003.severity = warning
            dotnet_diagnostic.MSTEST0007.severity = warning
            dotnet_diagnostic.MSTEST0064.severity = warning
            dotnet_diagnostic.MSTEST0074.severity = warning
            dotnet_diagnostic.MSTEST0075.severity = warning
            dotnet_diagnostic.MSTEST0092.severity = warning
            dotnet_diagnostic.MSTEST0093.severity = warning

            #file Discovery.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
            public sealed class CustomTestClassAttribute : TestClassAttribute { }
            public sealed class CustomTestMethodAttribute : TestMethodAttribute { }
            [CustomTestClass]
            internal class Invisible
            {
                [TestMethod] public void HiddenCheck() { }
            }
            [TestClass]
            public class Generic<T>
            {
                [TestMethod] public void Check() { }
            }
            [TestClass]
            public class InvalidMethod
            {
                [CustomTestMethod] public static void Check() { }
            }

            #file Fixtures.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class FixtureTests
            {
                [TestInitialize, ResourceLock("db"), DoNotParallelize]
                public void Setup() { }
                [TestMethod] public void Check() { }
            }

            #file Locking.cs
            using System;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class LockingTests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.EnvironmentVariables, Mode = ResourceAccessMode.Read)]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Check()
                {
                    Environment.SetEnvironmentVariable("analyzer-compile-only", "value");
                    Directory.SetCurrentDirectory(".");
                }
            }

            #file AsyncAssertion.cs
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class AsyncAssertionTests
            {
                [TestMethod]
                public void Check() => Assert.Throws<Exception>(async () => await Task.Yield());
            }

            #file BadData.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
            [TestClass]
            public class InvalidDataTests
            {
                [TestMethod, CombinatorialData]
                public void Check(string value) { }
            }

            #file BadAttributes.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class InvalidAttributeTests
            {
                [TestMethod, Timeout(0)]
                public void Check() { }
            }

            #file ValidCases.cs
            using System;
            using System.IO;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
            [TestClass]
            public class ValidTests
            {
                [TestMethod, CombinatorialData, Timeout(1), Retry(1), ExpectedDuration(1)]
                public void Data(bool value, CancellationToken token) { }

                [TestMethod]
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Locked()
                {
                    Environment.SetEnvironmentVariable("analyzer-compile-only", "value");
                    Directory.SetCurrentDirectory(".");
                }

                [TestMethod]
                public async Task Async()
                {
                    await Assert.ThrowsAsync<Exception>(async () => await Task.Yield());
                }
            }
            """
            .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(PackagedAnalyzerOpportunities_ReportInvalidCasesAndAcceptValidCases), code);
        string isolatedPackages = Path.Combine(testAsset.TargetAssetPath, ".nuget-packages");
        var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = isolatedPackages,
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["PreferredUILang"] = "en-US",
            ["VSLang"] = "1033",
        };
        string binlogPath = Path.Combine(testAsset.TargetAssetPath, "build.binlog");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -bl:\"{binlogPath}\"",
            environmentVariables: environmentVariables,
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        string output = result.StandardOutput + result.StandardError;
        TestContext.WriteLine(output);
        Assert.AreEqual(0, result.ExitCode, output);
        Assert.DoesNotContain("AD0001", output);

        (string Rule, string File, string Message)[] expected =
        [
            ("MSTEST0002", "Discovery.cs", "Invisible"),
            ("MSTEST0002", "Discovery.cs", "open generic type"),
            ("MSTEST0003", "Discovery.cs", "'HiddenCheck' signature"),
            ("MSTEST0003", "Discovery.cs", "'Check' signature"),
            ("MSTEST0007", "Fixtures.cs", "[ResourceLock] has no effect"),
            ("MSTEST0007", "Fixtures.cs", "[DoNotParallelize] has no effect"),
            ("MSTEST0064", "AsyncAssertion.cs", "async void delegate"),
            ("MSTEST0074", "Locking.cs", "Environment.SetEnvironmentVariable"),
            ("MSTEST0075", "Locking.cs", "Directory.SetCurrentDirectory"),
            ("MSTEST0092", "BadData.cs", "Cannot infer combinatorial values"),
            ("MSTEST0093", "BadAttributes.cs", "requires a positive"),
        ];
        string[] diagnostics = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => expected.Any(item => line.Contains($"warning {item.Rule}:", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(expected.Length, diagnostics, output);
        foreach ((string rule, string file, string message) in expected)
        {
            Assert.HasCount(1, diagnostics.Where(line =>
                line.Contains($"warning {rule}:", StringComparison.Ordinal)
                && line.Contains(file, StringComparison.Ordinal)
                && line.Contains(message, StringComparison.Ordinal)).ToArray(), output);
        }

        Assert.DoesNotContain("ValidCases.cs", string.Join(Environment.NewLine, diagnostics));

        using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(testAsset.TargetAssetPath, "obj", "project.assets.json")));
        JsonElement libraries = assets.RootElement.GetProperty("libraries");
        Assert.IsTrue(assets.RootElement.GetProperty("packageFolders").EnumerateObject().Any(folder =>
            string.Equals(Path.TrimEndingDirectorySeparator(folder.Name), Path.TrimEndingDirectorySeparator(isolatedPackages), StringComparison.OrdinalIgnoreCase)));
        foreach (string packageId in new[] { "MSTest.Analyzers", "MSTest.TestFramework" })
        {
            Assert.IsTrue(libraries.EnumerateObject().Any(library =>
                string.Equals(library.Name, $"{packageId}/{MSTestVersion}", StringComparison.OrdinalIgnoreCase)));
            string lowerId = packageId.ToLowerInvariant();
            string shipping = Path.Combine(Constants.ArtifactsPackagesShipping, $"{packageId}.{MSTestVersion}.nupkg");
            string restored = Path.Combine(isolatedPackages, lowerId, MSTestVersion.ToLowerInvariant(), $"{lowerId}.{MSTestVersion.ToLowerInvariant()}.nupkg");
            string shippingHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(shipping)));
            string restoredHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(restored)));
            Assert.AreEqual(shippingHash, restoredHash, $"The consumer must restore the current shipping {packageId} package.");
            TestContext.WriteLine($"{packageId}: {restored}; SHA256: {restoredHash}");
        }

        string analyzerPath = Path.Combine(isolatedPackages, "mstest.analyzers", MSTestVersion.ToLowerInvariant(), "analyzers", "dotnet", "cs", "MSTest.Analyzers.dll");
        Assert.IsTrue(File.Exists(analyzerPath), analyzerPath);
        using ZipArchive package = ZipFile.OpenRead(Path.Combine(Constants.ArtifactsPackagesShipping, $"MSTest.Analyzers.{MSTestVersion}.nupkg"));
        ZipArchiveEntry? analyzerEntry = package.GetEntry("analyzers/dotnet/cs/MSTest.Analyzers.dll");
        Assert.IsNotNull(analyzerEntry);
        using Stream analyzerStream = analyzerEntry.Open();
        string packagedAnalyzerHash = Convert.ToHexString(SHA256.HashData(analyzerStream));
        string restoredAnalyzerHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(analyzerPath)));
        Assert.AreEqual(packagedAnalyzerHash, restoredAnalyzerHash, "The compiler must receive the current packaged analyzer binary.");
        TestContext.WriteLine($"Packaged analyzer: {analyzerPath}; SHA256: {restoredAnalyzerHash}; build log: {binlogPath}");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task MSTEST0001(bool isMTP, bool isAdapterReferenced)
    {
        string code = $$"""
#file TestForMSTEST0001.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFrameworks$</TargetFramework>
    <RunAnalyzers>true</RunAnalyzers>
    <MSTestAnalysisMode>all</MSTestAnalysisMode>

    {{(isMTP ? """
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    """ : string.Empty)}}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    {{(isAdapterReferenced ? "<PackageReference Include=\"MSTest.TestAdapter\" Version=\"$MSTestVersion$\" />" : string.Empty)}}
  </ItemGroup>
</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod()
    {
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("TestForMSTEST0001", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        if (isAdapterReferenced)
        {
            result.AssertOutputContains("warning MSTEST0001");
        }
        else
        {
            result.AssertOutputDoesNotContain("warning MSTEST0001");
        }
    }

    [TestMethod]
    public async Task AnalyzersShouldBeEnabledWhenUsingMetapackage()
    {
        string code = """
#file AnalyzersMetapackage.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>

    <!-- This also serves as a test that VSTest is generating the entrypoint, even when it's a transitive dependency -->
    <OutputType>Exe</OutputType>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    public void TestMethod()
    {
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("AnalyzersMetapackage", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContains("MSTEST0014");
    }

    [TestMethod]
    public async Task AnalyzersShouldBeEnabledWhenUsingTestFrameworkPackage()
    {
        string code = """
#file AnalyzersTestFrameworkPackage.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    public void TestMethod()
    {
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("AnalyzersTestFrameworkPackage", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContains("MSTEST0014");
    }

    [TestMethod]
    public async Task AnalyzerMessagesShouldBeLocalized()
    {
        string code = """
#file Analyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>
  </PropertyGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    public void TestMethod()
    {
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync("Analyzers", code);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            environmentVariables: new()
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "it-IT",
                ["PreferredUILang"] = "it-IT",
                ["VSLang"] = "1040",
            },
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContains("DataRow deve essere impostato solo su un metodo di test");
    }

    [TestMethod]
    [DataRow("None", new string[0], new[] { "MSTEST0003", "MSTEST0004", "MSTEST0014", "MSTEST0016", "MSTEST0021" })]
    [DataRow("", new[] { "warning MSTEST0003", "warning MSTEST0014" }, new[] { "MSTEST0004", "MSTEST0016", "MSTEST0021" })]
    [DataRow("Default", new[] { "warning MSTEST0003", "warning MSTEST0014" }, new[] { "MSTEST0004", "MSTEST0016", "MSTEST0021" })]
    [DataRow("Recommended", new[] { "error MSTEST0003", "warning MSTEST0014", "warning MSTEST0016" }, new[] { "MSTEST0004", "MSTEST0021" })]
    [DataRow("All", new[] { "error MSTEST0003", "warning MSTEST0004", "warning MSTEST0014", "warning MSTEST0016" }, new[] { "MSTEST0021" })]
    public async Task VerifyMSTestAnalysisModeForDifferentAnalyzers(string analysisMode, string[] contains, string[] doesNotContain)
    {
        string code = """
#file VerifyMSTestAnalysisModeForDifferentAnalyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <RunAnalyzers>true</RunAnalyzers>
    <MSTestAnalysisMode>$MSTestAnalysisMode$</MSTestAnalysisMode>
  </PropertyGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    [TestMethod]
    public void TestMethod1()
    {
    }

    [DataRow(0)]
    public void TestMethod2(int x)
    {
    }
}

public class UnitTest2
{
}

[TestClass]
public class UnitTest3
{
    [TestCleanup]
    public void MyTestCleanup()
    {
    }
}

[TestClass]
public sealed class MyEmptyTestClass // generates MSTEST0016
{
}

[TestClass]
public class UnitTest4
{
    [TestMethod]
    public async void TestMethod1()
    {
        await Task.CompletedTask;
    }
}
""".PatchTargetFrameworks(TargetFrameworks.NetCurrent)
    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    .PatchCodeWithReplace("$MSTestAnalysisMode$", analysisMode);

        // MSTEST0003 is TestMethodShouldBeValidAnalyzer, which is escalated to error in Recommended and All.
        // MSTEST0004 is PublicTypeShouldBeTestClassAnalyzer. Info and not enabled by default.
        // MSTEST0014 is DataRowShouldBeValidAnalyzer. Warn and enabled by default.
        // MSTEST0016 is TestClassShouldHaveTestMethodAnalyzer. Info and enabled by default.
        // MSTEST0021 is PreferDisposeOverTestCleanupAnalyzer, which is disabled even in All mode.
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(nameof(VerifyMSTestAnalysisModeForDifferentAnalyzers), code);
        await AssertAnalysisModeAsync(analysisMode, contains, doesNotContain, testAsset.TargetAssetPath);
    }

    private static async Task AssertAnalysisModeAsync(string mode, string[] contains, string[] doesNotContain, string targetAssetPath)
    {
        async Task<DotnetMuxerResult> BuildTaskAsync() => await DotnetCli.RunAsync($"build {targetAssetPath}", warnAsError: false);

        string output;
        if (mode is "Recommended" or "All")
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(BuildTaskAsync);
            output = ex.Message;
        }
        else
        {
            output = (await BuildTaskAsync()).StandardOutput;
        }

        foreach (string containsElement in contains)
        {
            Assert.Contains(containsElement, output, $"Expected to find '{containsElement}' for analysisMode {mode}");
        }

        foreach (string doesNotContainElement in doesNotContain)
        {
            Assert.DoesNotContain(doesNotContainElement, output, $"Expected to not find '{doesNotContainElement}' for analysisMode {mode}");
        }
    }

    public TestContext TestContext { get; set; }
}
