// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

/// <summary>
/// Class-initialization overhead scenario: each test class has a <c>[ClassInitialize]</c> and a
/// <c>[ClassCleanup]</c> method (both minimal bodies, marked <c>NoInlining</c> so the JIT
/// cannot collapse them). These hooks run once per class (not per test), so the scenario
/// isolates the per-class cost of the MSTest class-lifecycle machinery
/// (ClassInitialize/ClassCleanup invocation dispatch, ExecutionContext capture, timeout-dictionary
/// lookups, inheritance-chain walking, …).
///
/// Additionally, a single <c>[AssemblyInitialize]</c> is defined on a dedicated class, measuring the
/// one-time assembly-init dispatch cost included in the first run.
///
/// Designed to pair with Scenario3 for direct overhead comparison — same total execution
/// count (100 classes × 100 methods = 10 000 test executions), the only differences being
/// that per-test hooks (TestInitialize/TestCleanup) are absent, and class-level hooks are present.
/// </summary>
internal class Scenario4 : IStep<NoInputOutput, SingleProject>
{
    private const string NuGetPackageExtensionName = ".nupkg";
    private const string MSTestTestFrameworkPackageNamePrefix = "MSTest.TestFramework.";

    private readonly int _numberOfClass;
    private readonly int _methodsPerClass;
    private readonly string _tfm;
    private readonly ExecutionScope _executionScope;
    private readonly int _workers;

    public Scenario4(int numberOfClass, int methodsPerClass, string tfm, ExecutionScope executionScope, int workers = 0)
    {
        _numberOfClass = numberOfClass;
        _methodsPerClass = methodsPerClass;
        _tfm = tfm;
        _executionScope = executionScope;
        _workers = workers;
    }

    public string Description => "create Scenario4 (class-initialization overhead)";

    public async Task<SingleProject> ExecuteAsync(NoInputOutput payload, IContext context)
    {
        Console.WriteLine($"Creating Scenario4 {_numberOfClass} classes, {_methodsPerClass} methods per class, ExecutionScope {_executionScope} with {_workers} workers");
        var cpmPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        string microsoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;
        string msTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, MSTestTestFrameworkPackageNamePrefix);

        StringBuilder stringBuilder = new();

        // One dedicated class carries the [AssemblyInitialize] and [AssemblyCleanup] hooks.
        stringBuilder.AppendLine("""

              [TestClass]
              public class AssemblyHooks
              {
                  [AssemblyInitialize]
                  [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                  public static void AssemblyInitialize(TestContext context)
                  {
                  }

                  [AssemblyCleanup]
                  [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                  public static void AssemblyCleanup()
                  {
                  }
              }
              """);

        for (int i = 0; i < _numberOfClass; i++)
        {
            stringBuilder.AppendLine(
                CultureInfo.InvariantCulture,
                $$"""

                  [TestClass]
                  public class UnitTest{{i}}
                  {
                      private static int _classState;

                      [ClassInitialize]
                      [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                      public static void ClassInitialize(TestContext context)
                      {
                          _classState = 1;
                      }

                      [ClassCleanup]
                      [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                      public static void ClassCleanup()
                      {
                          _classState = 0;
                      }
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
            nameof(Scenario4),
            CurrentMSTestSourceCode
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{_tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", microsoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", msTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
            .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
            .PatchCodeWithReplace("$Extra$", string.Empty)
            .PatchCodeWithReplace("$Tests$", stringBuilder.ToString())
            .PatchCodeWithReplace("$ExecutionScope$", _executionScope.ToString())
            .PatchCodeWithReplace("$Workers$", _workers.ToString(CultureInfo.InvariantCulture)));

        context.AddDisposable(generator);
        return new SingleProject([_tfm], generator, nameof(Scenario4));
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
#file Scenario4.csproj
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
