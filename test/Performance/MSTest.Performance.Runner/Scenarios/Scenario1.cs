// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal enum ExecutionScope
{
    ClassLevel = 0,
    MethodLevel = 1,
}

internal enum TestPlatform
{
    Mtp,
    VSTest,
}

internal enum MSTestSourceGenerationMode
{
    Disabled,
    Rooting,
    ReflectionFree,
    NativeAot,
}

internal class Scenario1 : IStep<NoInputOutput, SingleProject>
{
    private const string NuGetPackageExtensionName = ".nupkg";
    private const string MSTestTestFrameworkPackageNamePrefix = "MSTest.TestFramework.";

    private readonly int _numberOfClass;
    private readonly int _methodsPerClass;
    private readonly string _tfm;
    private readonly ExecutionScope _executionScope;
    private readonly int _workers;
    private readonly TestPlatform _testPlatform;
    private readonly MSTestSourceGenerationMode _sourceGenerationMode;

    public Scenario1(
        int numberOfClass,
        int methodsPerClass,
        string tfm,
        ExecutionScope executionScope,
        int workers = 0,
        TestPlatform testPlatform = TestPlatform.Mtp,
        MSTestSourceGenerationMode sourceGenerationMode = MSTestSourceGenerationMode.Disabled)
    {
        _numberOfClass = numberOfClass;
        _methodsPerClass = methodsPerClass;
        _tfm = tfm;
        _executionScope = executionScope;
        _workers = workers;
        _testPlatform = testPlatform;
        _sourceGenerationMode = sourceGenerationMode;
    }

    public string Description => "create Scenario1";

    public async Task<SingleProject> ExecuteAsync(NoInputOutput payload, IContext context)
    {
        Console.WriteLine(
            $"Creating Scenario1 {_numberOfClass} classes, {_methodsPerClass} methods per class, " +
            $"ExecutionScope {_executionScope} with {_workers} workers, platform {_testPlatform}, source generation {_sourceGenerationMode}");
        var cpmPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        string microsoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;
        string msTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, MSTestTestFrameworkPackageNamePrefix);

        StringBuilder stringBuilder = new();
        for (int i = 0; i < _numberOfClass; i++)
        {
            stringBuilder.AppendLine(
                CultureInfo.InvariantCulture,
                $$"""

                  [TestClass]
                  public class UnitTest{{i}}
                  {
                  """);
            for (int k = 1; k < _methodsPerClass + 1; k++)
            {
                if (k % 2 == 0)
                {
                    stringBuilder.AppendLine(
                        CultureInfo.InvariantCulture,
                        $$"""

                                  [TestMethod]
                                  [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                                  public System.Threading.Tasks.Task TestMethod{{k}}()
                                  {
                                      return System.Threading.Tasks.Task.CompletedTask;
                                  }

                          """);
                }
                else
                {
                    stringBuilder.AppendLine(
                        CultureInfo.InvariantCulture,
                        $$"""

                                  [TestMethod]
                                  [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                                  public void TestMethod{{k}}()
                                  {
                                  }

                          """);
                }
            }

            stringBuilder.AppendLine("}");
        }

        TestAsset generator = await TestAsset.GenerateAssetAsync(
            nameof(Scenario1),
            CurrentMSTestSourceCode
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{_tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", microsoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", msTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", $"<EnableMSTestRunner>{(_testPlatform == TestPlatform.Mtp ? "true" : "false")}</EnableMSTestRunner>")
            .PatchCodeWithReplace("$OutputType$", _testPlatform == TestPlatform.Mtp ? "<OutputType>Exe</OutputType>" : string.Empty)
            .PatchCodeWithReplace("$SourceGenerationProperties$", GetSourceGenerationProperties())
            .PatchCodeWithReplace("$SourceGenerationPackage$", GetSourceGenerationPackage(msTestVersion))
            .PatchCodeWithReplace("$Extra$", string.Empty)
            .PatchCodeWithReplace("$Tests$", stringBuilder.ToString())
            .PatchCodeWithReplace("$ExecutionScope$", _executionScope.ToString())
            .PatchCodeWithReplace("$Workers$", _workers.ToString(CultureInfo.InvariantCulture)),
            addPublicFeeds: true);

        if (_testPlatform == TestPlatform.VSTest)
        {
            string globalJson = await File.ReadAllTextAsync(Path.Combine(RootFinder.Find(), "global.json"));
            const string mtpRunner = "\"runner\": \"Microsoft.Testing.Platform\"";
            const string vstestRunner = "\"runner\": \"VSTest\"";
            string vstestGlobalJson = globalJson.Replace(mtpRunner, vstestRunner, StringComparison.Ordinal);
            if (string.Equals(globalJson, vstestGlobalJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Could not find '{mtpRunner}' in the repository global.json.");
            }

            await File.WriteAllTextAsync(Path.Combine(generator.TargetAssetPath, "global.json"), vstestGlobalJson);
        }

        context.AddDisposable(generator);
        return new SingleProject(
            [_tfm],
            generator,
            nameof(Scenario1),
            _testPlatform,
            _sourceGenerationMode,
            _numberOfClass * _methodsPerClass,
            _workers == 0 ? Environment.ProcessorCount : _workers);
    }

    private string GetSourceGenerationProperties()
        => _sourceGenerationMode switch
        {
            MSTestSourceGenerationMode.Disabled => string.Empty,
            MSTestSourceGenerationMode.Rooting => "<MSTestSourceGenMode>Rooting</MSTestSourceGenMode>",
            MSTestSourceGenerationMode.ReflectionFree or MSTestSourceGenerationMode.NativeAot
                => "<MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>",
            _ => throw new InvalidOperationException($"Unknown source-generation mode '{_sourceGenerationMode}'."),
        };

    private string GetSourceGenerationPackage(string version)
        => _sourceGenerationMode == MSTestSourceGenerationMode.Disabled
            ? string.Empty
            : $"<PackageReference Include=\"MSTest.SourceGeneration\" Version=\"{version}\" />";

    private static string ExtractVersionFromPackage(string rootFolder, string packagePrefixName)
    {
        string[] matches = Directory.GetFiles(rootFolder, packagePrefixName + "*" + NuGetPackageExtensionName, SearchOption.TopDirectoryOnly);

        if (matches.Length > 1)
        {
            // For some packages the find pattern will match multiple packages, for example:
            // Microsoft.Testing.Platform.1.0.0.nupkg
            // Microsoft.Testing.Platform.Extensions.1.0.0.nupkg
            // Let's take shortest name which should be closest to the package we are looking for.
            matches = [matches.OrderBy(x => x.Length).First()];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Was expecting to find a single NuGet package named '{packagePrefixName}' in '{rootFolder}' but found {matches.Length}.");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(packagePrefixName.Length, packageFullName.Length - packagePrefixName.Length - NuGetPackageExtensionName.Length);
    }

    protected const string CurrentMSTestSourceCode = """
#file Scenario1.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    $TargetFramework$
    $OutputType$
    $EnableMSTestRunner$
    $SourceGenerationProperties$
    $Extra$
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    $SourceGenerationPackage$
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = $Workers$, Scope = ExecutionScope.$ExecutionScope$)]

$Tests$
""";
}

internal class SingleProject : IPayload
{
    public SingleProject(
        string[] tfms,
        TestAsset testAsset,
        string assetName,
        TestPlatform testPlatform,
        MSTestSourceGenerationMode sourceGenerationMode,
        int expectedTestCount,
        int workerCount)
    {
        Tfms = tfms;
        TestAsset = testAsset;
        AssetName = assetName;
        TestPlatform = testPlatform;
        SourceGenerationMode = sourceGenerationMode;
        ExpectedTestCount = expectedTestCount;
        WorkerCount = workerCount;
    }

    public string[] Tfms { get; }

    public TestAsset TestAsset { get; }

    public string AssetName { get; }

    public TestPlatform TestPlatform { get; }

    public MSTestSourceGenerationMode SourceGenerationMode { get; }

    public int ExpectedTestCount { get; }

    public int WorkerCount { get; }
}
