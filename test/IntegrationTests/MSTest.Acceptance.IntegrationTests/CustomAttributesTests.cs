// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class CustomAttributesTests : AcceptanceTestBase<CustomAttributesTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DuplicateTestMethodAttribute_ShouldFail(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.DuplicateTestMethodProjectPath, TestAssetFixture.DuplicateTestMethodProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string DuplicateTestMethodProjectName = "DuplicateTestMethodAttribute";

        public string DuplicateTestMethodProjectPath => GetAssetPath(DuplicateTestMethodProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (DuplicateTestMethodProjectName, DuplicateTestMethodProjectName,
                DuplicateTestMethodSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string DuplicateTestMethodSourceCode = """
#file DuplicateTestMethodAttribute.csproj
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
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    [TestMethod]
    [MyTestMethod]
    public void Test1()
    {
    }

    [TestMethod]
    public void Test2()
    {
    }
}

public class MyTestMethodAttribute : TestMethodAttribute
{
    public MyTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }
}
""";
    }
}
