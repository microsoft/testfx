// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DuplicateTestClassAttributeTests : AcceptanceTestBase<DuplicateTestClassAttributeTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DuplicateTestClassAttribute_ShouldFail(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.DuplicateTestClassProjectPath, TestAssetFixture.DuplicateTestClassProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.AssertStandardErrorContains("Only one attribute of type 'Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute' is allowed, but multiple were found.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string DuplicateTestClassProjectName = "DuplicateTestClassAttribute";

        public string DuplicateTestClassProjectPath => GetAssetPath(DuplicateTestClassProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (DuplicateTestClassProjectName, DuplicateTestClassProjectName,
                DuplicateTestClassSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string DuplicateTestClassSourceCode = """
#file DuplicateTestClassAttribute.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[MyTestClass]
public class TestClass2
{
    [TestMethod]
    public void Test1()
    {
    }
}

public class MyTestClassAttribute : TestClassAttribute
{
}
""";
    }
}
