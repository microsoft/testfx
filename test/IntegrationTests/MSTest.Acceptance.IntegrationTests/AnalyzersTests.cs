// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AnalyzersTests : AcceptanceTestBase<AnalyzersTests.TestAssetFixture>
{
    [TestMethod]
    public void AnalyzerMessagesShouldBeLocalized()
    {
        TestAsset testAsset = AssetFixture.GetTestAsset("Analyzers");
        testAsset.DotnetResult!.AssertOutputContains("DataRow ne doit être défini que sur une méthode de test");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "Analyzers";

        public string ProjectPath => GetAssetPath(ProjectName);

        protected override Dictionary<string, string?> DotNetBuildEnvironmentVariables { get; } = new()
        {
            // This is fr-FR
            ["VSLang"] = "1036",
        };

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file Analyzers.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [DataRow(0)]
    public void TestMethod()
    {
    }
}
""";
    }
}
