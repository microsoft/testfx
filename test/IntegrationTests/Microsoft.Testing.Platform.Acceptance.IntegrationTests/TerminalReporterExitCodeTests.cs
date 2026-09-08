// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TerminalReporterExitCodeTests : AcceptanceTestBase<TerminalReporterExitCodeTests.TestAssetFixture>
{
    private const string AssetName = "TerminalReporterExitCode";
    private const string DiscoveryExitCodeMessage = "Test discovery completed with non-success exit code: 5. The command-line arguments are invalid. See: https://aka.ms/testingplatform/exitcodes";
    private const string RunExitCodeMessage = "Test run completed with non-success exit code: 5. The command-line arguments are invalid. See: https://aka.ms/testingplatform/exitcodes";

    [DataRow("", RunExitCodeMessage)]
    [DataRow("discover", DiscoveryExitCodeMessage)]
    [TestMethod]
    public async Task InvalidCommandLineExitCode_PrintsDescriptionAsFinalLine(string command, string expected)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath,
            AssetName,
            TargetFrameworks.NetCurrent);

        TestHostResult result = await testHost.ExecuteAsync(command, cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual(expected, result.StandardOutput.TrimEnd().Split(Environment.NewLine)[^1]);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
            #file TerminalReporterExitCode.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="$PolyfillsPath$/**/*.cs" Link="Polyfills/%(RecursiveDir)%(Filename)%(Extension)" />
                <Using Include="System.Collections" />
                <Using Include="System.Collections.Concurrent" />
                <Using Include="System.Diagnostics" />
                <Using Include="System.Diagnostics.CodeAnalysis" />
                <Using Include="System.Globalization" />
                <Using Include="System.Reflection" />
                <Using Include="System.Runtime.CompilerServices" />
                <Using Include="System.Runtime.InteropServices" />
                <Using Include="System.Runtime.Versioning" />
                <Using Include="System.Text" />
                <Using Include="System.Text.RegularExpressions" />
                <Using Include="System.Xml" />
                <Using Include="System.Xml.Linq" />
                <Using Include="System.Xml.XPath" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
              </ItemGroup>
              <Import Project="$TerminalReporterContractProps$" />
              <ItemGroup>
                <EmbeddedResource Update="$TerminalResourcesResx$"
                                  GenerateSource="false"
                                  LogicalName="Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalResources.resources" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Platform.Helpers;
            using Microsoft.Testing.Platform.OutputDevice.Terminal;

            bool isDiscovery = Array.IndexOf(args, "discover") >= 0;
            using var reporter = new TerminalTestReporter(
                new SystemConsole(),
                new TerminalTestReporterOptions
                {
                    AnsiMode = AnsiMode.NoAnsi,
                    ShowProgress = static () => false,
                    ShowAssembly = true,
                });

            reporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery, isHelp: false, isRetry: false);
            reporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: (int)ExitCode.InvalidCommandLine);
            return 0;
            """;

        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            string repoRoot = RootFinder.Find().Replace('\\', '/').TrimEnd('/');
            string terminalResourcesResx = $"{repoRoot}/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalResources.resx";
            string code = Sources
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$PolyfillsPath$", $"{repoRoot}/src/Polyfills")
                .PatchCodeWithReplace(
                    "$TerminalReporterContractProps$",
                    $"{repoRoot}/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalReporterContract.props")
                .PatchCodeWithReplace("$TerminalResourcesResx$", terminalResourcesResx);

            return (
                AssetName,
                AssetName,
                $"{code}{Environment.NewLine}#file TerminalResources.cs{Environment.NewLine}{CreateTerminalResourcesAccessor(terminalResourcesResx)}");
        }

        private static string CreateTerminalResourcesAccessor(string resxPath)
        {
            // Generated acceptance assets do not import the repo's Arcade resx source generator. Emit the same
            // strongly typed accessor shape so this external consumer still compiles the real neutral resource.
            var builder = new StringBuilder(
                """
                using System.Resources;

                namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

                internal static partial class TerminalResources
                {
                    private static readonly ResourceManager ResourceManager = new(typeof(TerminalResources));

                    internal static string GetResourceString(string resourceKey) => ResourceManager.GetString(resourceKey)!;

                """);

            foreach (string name in XDocument.Load(resxPath).Root!.Elements("data").Select(static element => element.Attribute("name")!.Value))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"    internal static string @{name} => GetResourceString(\"{name}\");");
            }

            builder.Append('}');
            return builder.ToString();
        }
    }

    public TestContext TestContext { get; set; }
}
