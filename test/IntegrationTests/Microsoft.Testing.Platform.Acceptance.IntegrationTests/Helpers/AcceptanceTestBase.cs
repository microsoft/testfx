// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// All the properties of this class should be non static.
/// At the moment are static because we need to share them between perclass/id fixtures and
/// it's not supported at the moment.
/// </summary>
public abstract class AcceptanceTestBase : TestBase
{
    private const string MSTestTestFrameworkPackageNamePrefix = "MSTest.TestFramework.";
    private const string NuGetPackageExtensionName = ".nupkg";

    internal static string RID { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    public static string MSTestCurrentVersion { get; private set; }

    public static string MicrosoftNETTestSdkVersion { get; private set; }

    static AcceptanceTestBase()
    {
        var mstestTestFrameworkPackage = Path.GetFileName(Directory.GetFiles(Constants.ArtifactsPackagesShipping, MSTestTestFrameworkPackageNamePrefix + "*" + NuGetPackageExtensionName, SearchOption.AllDirectories).Single());
        MSTestCurrentVersion = mstestTestFrameworkPackage.Substring(
            MSTestTestFrameworkPackageNamePrefix.Length,
            mstestTestFrameworkPackage.Length - MSTestTestFrameworkPackageNamePrefix.Length - NuGetPackageExtensionName.Length);

        XDocument versionsPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "eng", "Versions.props"));
        MicrosoftNETTestSdkVersion = versionsPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;
    }

    protected AcceptanceTestBase(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    protected const string CurrentMSTestSourceCode = """
#file MSTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    $OutputType$
    $EnableMSTestRunner$
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

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";

    internal static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)>> GetBuildMatrixTfmBuildVerbConfiguration()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                foreach (Verb verb in Enum.GetValues<Verb>())
                {
                    yield return new TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)>((tfm.Arguments, compilationMode, verb), $"{tfm.Arguments},{compilationMode},{verb}");
                }
            }
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)>> GetBuildMatrixTfmBuildConfiguration()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                yield return new TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)>((tfm.Arguments, compilationMode), $"{tfm.Arguments},{compilationMode}");
            }
        }
    }
}
