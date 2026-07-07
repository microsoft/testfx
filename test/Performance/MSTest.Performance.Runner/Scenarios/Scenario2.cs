// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

/// <summary>
/// Data-driven test scenario: each test method is decorated with multiple
/// <c>[DataRow]</c> attributes so the test runner exercises the data-driven hot path
/// (CloneForDataDrivenIteration, ReflectionTestMethodInfo reuse, ParameterTypes cache, …).
///
/// Designed to pair with Scenario1 for direct overhead comparison:
///   Scenario1 → 100 classes × 100 methods × 1  row  = 10 000 test executions
///   Scenario2 → 100 classes × 10  methods × 10 rows = 10 000 test executions
/// </summary>
internal class Scenario2 : IStep<NoInputOutput, SingleProject>
{
    private const string NuGetPackageExtensionName = ".nupkg";
    private const string MSTestTestFrameworkPackageNamePrefix = "MSTest.TestFramework.";

    private readonly int _numberOfClass;
    private readonly int _methodsPerClass;
    private readonly int _dataRowsPerMethod;
    private readonly string _tfm;
    private readonly ExecutionScope _executionScope;
    private readonly int _workers;

    public Scenario2(int numberOfClass, int methodsPerClass, int dataRowsPerMethod, string tfm, ExecutionScope executionScope, int workers = 0)
    {
        _numberOfClass = numberOfClass;
        _methodsPerClass = methodsPerClass;
        _dataRowsPerMethod = dataRowsPerMethod;
        _tfm = tfm;
        _executionScope = executionScope;
        _workers = workers;
    }

    public string Description => "create Scenario2 (data-driven)";

    public async Task<SingleProject> ExecuteAsync(NoInputOutput payload, IContext context)
    {
        Console.WriteLine($"Creating Scenario2 {_numberOfClass} classes, {_methodsPerClass} methods per class, {_dataRowsPerMethod} data rows per method, ExecutionScope {_executionScope} with {_workers} workers");

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
                // Emit [DataRow] attributes before [TestMethod] — same count for every method.
                for (int d = 0; d < _dataRowsPerMethod; d++)
                {
                    stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"          [DataRow({d})]");
                }

                if (k % 2 == 0)
                {
                    stringBuilder.AppendLine(
                        CultureInfo.InvariantCulture,
                        $$"""
                                  [TestMethod]
                                  [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                                  public System.Threading.Tasks.Task TestMethod{{k}}(int data)
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
                                  public void TestMethod{{k}}(int data)
                                  {
                                  }

                          """);
                }
            }

            stringBuilder.AppendLine("}");
        }

        TestAsset generator = await TestAsset.GenerateAssetAsync(
            nameof(Scenario2),
            CurrentMSTestSourceCode
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{_tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", microsoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", msTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
            .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
            .PatchCodeWithReplace("$Extra$", string.Empty)
            .PatchCodeWithReplace("$Tests$", stringBuilder.ToString())
            .PatchCodeWithReplace("$ExecutionScope$", _executionScope.ToString())
            .PatchCodeWithReplace("$Workers$", _workers.ToString(CultureInfo.InvariantCulture)),
            addPublicFeeds: true);

        context.AddDisposable(generator);
        return new SingleProject(["net9.0"], generator, nameof(Scenario2));
    }

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
#file Scenario2.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PlatformTarget>x64</PlatformTarget>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    $TargetFramework$
    $OutputType$
    $EnableMSTestRunner$
    $Extra$
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = $Workers$, Scope = ExecutionScope.$ExecutionScope$)]

$Tests$
""";
}
