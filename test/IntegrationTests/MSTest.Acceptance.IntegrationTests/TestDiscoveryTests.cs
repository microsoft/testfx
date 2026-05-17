// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TestDiscoveryTests : AcceptanceTestBase<TestDiscoveryTests.TestAssetFixture>
{
    private const string AssetName = "TestDiscovery";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_FindsAllTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Test1");
        testHostResult.AssertOutputContains("Test2");
        testHostResult.AssertOutputContains("Display name: 1, one");
        testHostResult.AssertOutputContains("Display name: 2, two");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_WithFilter_FindsOnlyFilteredOnes(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --filter Name=Test1", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Test1");
        testHostResult.AssertOutputDoesNotContain("Test2");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_WithJsonOutput_ProducesValidJsonDocumentWithExpectedFields(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests json", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Stdout must be the JSON document only — no banner, no progress, no summary.
        // Trim because the test infrastructure may append a final newline.
        string output = testHostResult.StandardOutput.Trim();
        Assert.IsTrue(output.StartsWith('{'), $"Expected stdout to start with '{{' but starts with: {output[..Math.Min(40, output.Length)]}");
        Assert.IsTrue(output.EndsWith('}'), $"Expected stdout to end with '}}' but ends with: {output[^Math.Min(40, output.Length)..]}");

        // No error noise on stderr for a successful discovery.
        Assert.AreEqual(string.Empty, testHostResult.StandardError.Trim());

        // The JSON document must be valid and parseable.
        using var document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());

        JsonElement tests = root.GetProperty("tests");
        Assert.IsGreaterThanOrEqualTo(2, tests.GetArrayLength(), $"Expected at least 2 tests but got {tests.GetArrayLength()}.");

        // Collect all display names and assert the expected ones are present.
        var displayNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tests.GetArrayLength(); i++)
        {
            displayNames.Add(tests[i].GetProperty("displayName").GetString()!);
        }

        Assert.Contains("Test1", displayNames);
        Assert.Contains("Test2", displayNames);

        // Each test should expose a unique uid.
        var uids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tests.GetArrayLength(); i++)
        {
            JsonElement test = tests[i];
            string uid = test.GetProperty("uid").GetString()!;
            Assert.IsTrue(uids.Add(uid), $"Duplicated uid '{uid}'.");
        }

        // Test1 should expose its TestMethodIdentifierProperty data, pinning every v1 schema field
        // for that node from the outside. Note: MSTest's VSTestBridge currently leaves
        // assemblyFullName and returnTypeFullName empty (TODO in MSTestBridgedTestFramework);
        // assert presence only for those.
        bool foundTest1WithType = false;
        for (int i = 0; i < tests.GetArrayLength(); i++)
        {
            JsonElement test = tests[i];
            if (test.GetProperty("displayName").GetString() == "Test1"
                && test.TryGetProperty("type", out JsonElement type))
            {
                Assert.AreEqual(JsonValueKind.String, type.GetProperty("assemblyFullName").ValueKind);
                Assert.AreEqual("TestClass", type.GetProperty("typeName").GetString());
                Assert.AreEqual("Test1", type.GetProperty("methodName").GetString());
                Assert.AreEqual(0, type.GetProperty("methodArity").GetInt32());
                Assert.AreEqual(JsonValueKind.String, type.GetProperty("returnTypeFullName").ValueKind);
                Assert.AreEqual(JsonValueKind.Array, type.GetProperty("parameterTypeFullNames").ValueKind);
                foundTest1WithType = true;
                break;
            }
        }

        Assert.IsTrue(foundTest1WithType, "Expected at least one Test1 entry to expose 'type' metadata.");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverTests_WithInvalidJsonArgument_FailsWithValidationError(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests xml", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--list-tests' received unexpected value 'xml'");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestDiscovery.csproj
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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    public void Test1() {}

    [TestMethod]
    public void Test2() {}

    [DynamicData(nameof(GetData), DynamicDataDisplayName = nameof(GetDisplayName))]
    [TestMethod]
    public void TestWithData(int _1, string _2)
    {
    }

    public static IEnumerable<(int, string)> GetData()
    {
        yield return (1, "one");
        yield return (2, "two");
    }

    public static string GetDisplayName(MethodInfo methodInfo, object[] data)
    {
        return $"Display name: {data[0]}, {data[1]}";
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
