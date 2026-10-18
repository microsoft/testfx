// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestMethodCancellationTokenTests : AcceptanceTestBase<TestMethodCancellationTokenTests.TestAssetFixture>
{
    private const string AssetName = "TestMethodCancellationToken";

    private static readonly string[] ExpectedPassingNames =
    [
        "PlainVoid",
        "PlainTask",
        "PlainValueTask",
        "OptionalToken",
        "InlineRows (7,\"inline-a\")",
        "InlineRows (11,\"inline-b\")",
        "singleton object-array token",
        "typed-array token",
        "generic whole-array token",
        "generic type-array token",
        "OptionalData (13)",
        "OptionalData (17,\"provided\")",
        "DynamicPlain (19,\"dynamic\")",
        "DynamicTyped (23)",
        "DynamicTyped (29)",
        "TupleExpanded (31,\"tuple\")",
        "TupleAsOne ((31, tuple))",
        "ObjectTupleAsOne ((31, tuple))",
        "ObjectArrayAsOne ([37,\"array\"])",
        "ObjectArrayTupleAsOne ([(31, tuple)])",
        "OptionalObjectArrayAsOne ([[17,\"array\"]])",
        "metadata row a",
        "metadata row b",
        "FoldedRows (47)",
        "FoldedRows (53)",
        "GenericRow (59)",
        "Combinations (61,True)",
        "Combinations (61,False)",
        "Combinations (67,True)",
        "Combinations (67,False)",
        "explicit user token",
        "ExplicitObjectTuple (83,System.Threading.CancellationToken)",
        "ExplicitObjectTuple (wrapped tuple)",
        "ExplicitObjectTupleFolded (83,System.Threading.CancellationToken)",
        "ExplicitObjectTupleFolded (wrapped tuple)",
        "ExplicitObjectArrayTuple ([89,\"tuple-array\"],System.Threading.CancellationToken)",
        "ExplicitObjectArrayTuple (wrapped tuple)",
        "ExplicitObjectArrayTupleFolded ([89,\"tuple-array\"],System.Threading.CancellationToken)",
        "ExplicitObjectArrayTupleFolded (wrapped tuple)",
        "CustomInvocation",
        "CombinatorialTokenOnly ()",
    ];

    private static readonly string[] ExpectedFailingNames =
    [
        "MissingRealData ()",
        "WrongRealData (\"wrong\")",
        "ExtraRealData (79,null,\"extra\")",
    ];

    public TestContext TestContext { get; set; }

    public static IEnumerable<object[]> MetadataCases
    {
        get
        {
            yield return [MetadataMode.Reflection];
            if (!AcceptanceSourceGen.IsGloballyDisabled)
            {
                yield return [MetadataMode.SourceGeneration];
                yield return [MetadataMode.AotSourceGeneration];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(MetadataCases))]
    public async Task Mtp_InjectsTokensWithoutChangingUserData(MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        TestHostResult discovery = await host.ExecuteAsync(
            "--list-tests --filter ClassName=InjectedTokenTests",
            cancellationToken: TestContext.CancellationToken);

        discovery.AssertExitCodeIs(ExitCode.Success);
        // Each of the three folded methods has one discovery node but executes two concrete cases.
        string[] expectedNames = metadataMode == MetadataMode.AotSourceGeneration
            ? [.. ExpectedPassingNames.Where(name => name is not "GenericRow (59)" and not "generic whole-array token" and not "generic type-array token")]
            : ExpectedPassingNames;
        discovery.AssertOutputContains($"found {expectedNames.Length - 3} test(s)");
        discovery.AssertOutputContains("InlineRows (7,\"inline-a\")");
        discovery.AssertOutputContains("singleton object-array token");
        discovery.AssertOutputContains("typed-array token");
        discovery.AssertOutputContains("ObjectArrayAsOne ([37,\"array\"])");
        discovery.AssertOutputContains("TupleAsOne ((31, tuple))");
        discovery.AssertOutputContains("ObjectTupleAsOne ((31, tuple))");
        discovery.AssertOutputContains("ObjectArrayTupleAsOne ([(31, tuple)])");
        discovery.AssertOutputContains("OptionalObjectArrayAsOne ([[17,\"array\"]])");
        discovery.AssertOutputContains("ExplicitObjectTuple (83,System.Threading.CancellationToken)");
        discovery.AssertOutputContains("ExplicitObjectTuple (wrapped tuple)");
        discovery.AssertOutputContains("ExplicitObjectArrayTuple ([89,\"tuple-array\"],System.Threading.CancellationToken)");
        discovery.AssertOutputContains("ExplicitObjectArrayTuple (wrapped tuple)");
        if (metadataMode != MetadataMode.AotSourceGeneration)
        {
            discovery.AssertOutputContains("GenericRow (59)");
            discovery.AssertOutputContains("generic whole-array token");
            discovery.AssertOutputContains("generic type-array token");
        }

        using TempDirectory results = new();
        TestHostResult result = await host.ExecuteAsync(
            $"--filter ClassName=InjectedTokenTests --output Detailed --report-trx --report-trx-filename tokens.trx --results-directory \"{results.Path}\"",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: expectedNames.Length, skipped: 0);
        AssertResults(Path.Combine(results.Path, "tokens.trx"), expectedNames, "Passed");
    }

    [TestMethod]
    [DynamicData(nameof(MetadataCases))]
    public async Task Mtp_RejectsInvalidDataWithoutInvokingUserCode(MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        using TempDirectory results = new();
        TestHostResult result = await host.ExecuteAsync(
            $"--filter ClassName=InvalidDataTests --output Detailed --report-trx --report-trx-filename invalid.trx --results-directory \"{results.Path}\"",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 3, passed: 0, skipped: 0);
        result.AssertOutputDoesNotContain("INVALID_SHAPE_BODY_EXECUTED");
        XElement[] cases = AssertResults(Path.Combine(results.Path, "invalid.trx"), ExpectedFailingNames, "Failed");
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        foreach (XElement testCase in cases)
        {
            string error = testCase.Descendants(ns + "Message").Single().Value;
            if (metadataMode == MetadataMode.AotSourceGeneration && (string?)testCase.Attribute("testName") == "WrongRealData (\"wrong\")")
            {
                Assert.Contains("System.InvalidCastException:", error);
            }
            else
            {
                Assert.Contains("Test data doesn't match method parameters", error);
            }

            Assert.DoesNotContain("INVALID_SHAPE_BODY_EXECUTED", error);
        }
    }

    [TestMethod]
    [DynamicData(nameof(MetadataCases))]
    public async Task Mtp_RebindsCancellationTokenForRetry(MetadataMode metadataMode)
    {
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        TestHostResult result = await host.ExecuteAsync(
            "--filter ClassName=RetryTokenTests --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        result.AssertOutputContains("failed (try 1) RebindsForRetry");
        result.AssertOutputContains("passed (try 2) RebindsForRetry");
    }

    [TestMethod]
    public async Task Vstest_InjectsTokensWithoutChangingUserData()
    {
        string projectPath = AssetFixture.GetAssetPath(AssetName);
        var host = TestHost.LocateFrom(projectPath, AssetName, TargetFrameworks.NetCurrent);
        string assemblyPath = Path.Combine(host.DirectoryName, AssetName + ".dll");
        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        string dotnetPath = Path.Combine(dotnetRoot, $"dotnet{Constants.ExecutableExtension}");
        Dictionary<string, string?> environmentVariables = new() { ["DOTNET_ROOT"] = dotnetRoot };
        using TempDirectory results = new();
        using CommandLine commandLine = new();

        int discoveryExitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /ListTests /TestCaseFilter:\"FullyQualifiedName~InjectedTokenTests\"",
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, discoveryExitCode, $"{commandLine.StandardOutput}{Environment.NewLine}{commandLine.ErrorOutput}");
        Assert.Contains("InlineRows (7,\"inline-a\")", commandLine.StandardOutput);
        Assert.Contains("ObjectArrayAsOne ([37,\"array\"])", commandLine.StandardOutput);
        Assert.Contains("OptionalObjectArrayAsOne ([[17,\"array\"]])", commandLine.StandardOutput);
        Assert.Contains("ExplicitObjectTuple", commandLine.StandardOutput);
        Assert.Contains("ExplicitObjectArrayTuple", commandLine.StandardOutput);

        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{dotnetPath}\" vstest \"{assemblyPath}\" /TestCaseFilter:\"FullyQualifiedName~InjectedTokenTests\" /Logger:\"trx;LogFileName=tokens.trx\" /ResultsDirectory:\"{results.Path}\"",
            environmentVariables: environmentVariables,
            workingDirectory: projectPath,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, exitCode, $"{commandLine.StandardOutput}{Environment.NewLine}{commandLine.ErrorOutput}");
        AssertResults(Path.Combine(results.Path, "tokens.trx"), ExpectedPassingNames, "Passed");
    }

    private static XElement[] AssertResults(string trxPath, string[] expectedNames, string expectedOutcome)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement[] results = XDocument.Load(trxPath).Descendants(ns + "UnitTestResult")
            .Where(result => !result.Descendants(ns + "UnitTestResult").Any())
            .ToArray();

        Assert.AreSequenceEqual(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            results.Select(result => (string?)result.Attribute("testName")).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        foreach (XElement result in results)
        {
            Assert.AreEqual(expectedOutcome, (string?)result.Attribute("outcome"), (string?)result.Attribute("testName"));
        }

        Assert.AreEqual(expectedNames.Length, results.Select(result => (string?)result.Attribute("executionId")).Distinct().Count());
        return results;
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [MetadataMode.SourceGeneration, MetadataMode.AotSourceGeneration];

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));

        private const string SourceCode = """
#file TestMethodCancellationToken.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <RunAnalyzers>true</RunAnalyzers>
    <WarningsAsErrors>$(WarningsAsErrors);MSTEST0014</WarningsAsErrors>
    <DefineConstants Condition="'$(MSTestSourceGenMode)' == 'ReflectionFree'">$(DefineConstants);REFLECTION_FREE</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
  </ItemGroup>
</Project>

#file .editorconfig
root = true
[*.cs]
dotnet_diagnostic.MSTEST0014.severity = error

#file InjectedTokenTests.cs
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 2)]

[TestClass]
public sealed class InjectedTokenTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PlainVoid(CancellationToken token)
        => TokenAssertions.Injected(TestContext, token, null);

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task PlainTask(CancellationToken token)
    {
        await Task.CompletedTask;
        TokenAssertions.Injected(TestContext, token, null);
    }

    [TestMethod]
    public ValueTask PlainValueTask(CancellationToken token)
    {
        TokenAssertions.Injected(TestContext, token, null);
        return default;
    }

    [TestMethod]
    public void OptionalToken(CancellationToken token = default)
        => TokenAssertions.Injected(TestContext, token, null);

    [TestMethod]
    [DataRow(7, "inline-a")]
    [DataRow(11, "inline-b")]
    public void InlineRows(int value, string label, CancellationToken token)
    {
        Assert.AreEqual(value == 7 ? "inline-a" : "inline-b", label);
        Assert.IsTrue(value is 7 or 11);
        TokenAssertions.Injected(TestContext, token, [value, label]);
    }

    [TestMethod]
    [DataRow(7, DisplayName = "singleton object-array token")]
    public void SingletonObjectArray(object[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new object[] { 7 }, values);
        TokenAssertions.Injected(TestContext, token, [7]);
    }

    [TestMethod]
    [DataRow(new int[] { 1, 2 }, DisplayName = "typed-array token")]
    public void TypedArray(int[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new int[] { 1, 2 }, values);
        TokenAssertions.Injected(TestContext, token, [values]);
    }

    [TestMethod]
    [DataRow(13)]
    [DataRow(17, "provided")]
    public void OptionalData(int value, string label = "fallback", CancellationToken token = default)
    {
        Assert.IsTrue(value is 13 or 17);
        Assert.AreEqual(value == 13 ? "fallback" : "provided", label);
        TokenAssertions.Injected(TestContext, token, value == 13 ? [13] : [17, "provided"]);
    }

    [TestMethod]
    [DynamicData(nameof(PlainRows))]
    public void DynamicPlain(int value, string label, CancellationToken token)
    {
        Assert.AreEqual(19, value);
        Assert.AreEqual("dynamic", label);
        TokenAssertions.Injected(TestContext, token, [19, "dynamic"]);
    }

    [TestMethod]
    [DynamicData(nameof(TypedRows))]
    public void DynamicTyped(int value, CancellationToken token)
    {
        Assert.IsTrue(value is 23 or 29);
        TokenAssertions.Injected(TestContext, token, [value]);
    }

    [TestMethod]
    [DynamicData(nameof(TupleRows))]
    public void TupleExpanded(int value, string label, CancellationToken token)
    {
        Assert.AreEqual(31, value);
        Assert.AreEqual("tuple", label);
        TokenAssertions.Injected(TestContext, token, [31, "tuple"]);
    }

    [TestMethod]
    [DynamicData(nameof(TupleRows))]
    public void TupleAsOne((int Number, string Label) value, CancellationToken token)
    {
        Assert.AreEqual((31, "tuple"), value);
        TokenAssertions.Injected(TestContext, token, [(31, "tuple")]);
    }

    [TestMethod]
    [DynamicData(nameof(TupleRows))]
    public void ObjectTupleAsOne(object value, CancellationToken token)
    {
        Assert.AreEqual((object)(31, "tuple"), value);
        TokenAssertions.Injected(TestContext, token, [(31, "tuple")]);
    }

    [TestMethod]
    [DynamicData(nameof(ObjectRows))]
    public void ObjectArrayAsOne(object[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new object[] { 37, "array" }, values);
        TokenAssertions.Injected(TestContext, token, [37, "array"]);
    }

    [TestMethod]
    [DynamicData(nameof(TupleRows))]
    public void ObjectArrayTupleAsOne(object[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new object[] { (31, "tuple") }, values);
        TokenAssertions.Injected(TestContext, token, [(31, "tuple")]);
    }

    [TestMethod]
    [DynamicData(nameof(WrappedObjectRows))]
    public void OptionalObjectArrayAsOne(object[] values, CancellationToken token = default)
    {
        Assert.AreSequenceEqual(new object[] { 17, "array" }, values);
        TokenAssertions.Injected(TestContext, token, [values]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DynamicData(nameof(MetadataRows))]
    public void MetadataUnfolded(int value, string label, CancellationToken token)
    {
        Assert.IsTrue(value is 41 or 43);
        Assert.AreEqual(value == 41 ? "row-a" : "row-b", label);
        TokenAssertions.Injected(TestContext, token, [value, label]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(FoldedData))]
    public void FoldedRows(int value, CancellationToken token)
    {
        Assert.IsTrue(value is 47 or 53);
        TokenAssertions.Injected(TestContext, token, [value]);
    }

#if !REFLECTION_FREE
    // Generic test methods are not supported by the existing reflection-free generator.
    [TestMethod]
    [DataRow(59)]
    public void GenericRow<T>(T value, CancellationToken token)
    {
        Assert.AreEqual(typeof(int), typeof(T));
        Assert.AreEqual((object)59, value);
        TokenAssertions.Injected(TestContext, token, [59]);
    }

    [TestMethod]
    [DataRow(new int[] { 3, 5 }, DisplayName = "generic whole-array token")]
    public void GenericWholeArray<T>(T value, CancellationToken token)
    {
        Assert.AreEqual(typeof(int[]), typeof(T));
        int[] values = Assert.IsInstanceOfType<int[]>(value);
        Assert.AreSequenceEqual(new int[] { 3, 5 }, values);
        TokenAssertions.Injected(TestContext, token, [values]);
    }

    [TestMethod]
    [DataRow((object)new Type[] { typeof(int) }, DisplayName = "generic type-array token")]
    public void GenericTypeArray<T>(T value, CancellationToken token)
    {
        Assert.AreEqual(typeof(Type[]), typeof(T));
        Type[] values = Assert.IsInstanceOfType<Type[]>(value);
        Assert.AreSequenceEqual(new Type[] { typeof(int) }, values);
        TokenAssertions.Injected(TestContext, token, [values]);
    }
#endif

    [TestMethod]
    [CombinatorialData]
    public void Combinations([CombinatorialValues(61, 67)] int value, bool flag, CancellationToken token)
    {
        Assert.IsTrue(value is 61 or 67);
        TokenAssertions.Injected(TestContext, token, [value, flag]);
    }

    [TestMethod]
    [DynamicData(nameof(ExplicitTokenRows))]
    public void ExplicitTokenData(int value, CancellationToken token)
    {
        Assert.AreEqual(71, value);
        TokenAssertions.Explicit(TestContext, token, [71, new CancellationToken(canceled: true)]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DynamicData(nameof(ExplicitObjectTupleRows))]
    [DynamicData(nameof(ExplicitObjectTupleMetadataRows))]
    public void ExplicitObjectTuple(object value, CancellationToken token)
    {
        Assert.AreEqual((object)83, value);
        TokenAssertions.Explicit(TestContext, token, [83, new CancellationToken(canceled: true)]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(ExplicitObjectTupleRows))]
    [DynamicData(nameof(ExplicitObjectTupleMetadataRows))]
    public void ExplicitObjectTupleFolded(object value, CancellationToken token)
    {
        Assert.AreEqual((object)83, value);
        TokenAssertions.Explicit(TestContext, token, [83, new CancellationToken(canceled: true)]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DynamicData(nameof(ExplicitObjectArrayTupleRows))]
    [DynamicData(nameof(ExplicitObjectArrayTupleMetadataRows))]
    public void ExplicitObjectArrayTuple(object[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new object[] { 89, "tuple-array" }, values);
        TokenAssertions.Explicit(TestContext, token, [values, new CancellationToken(canceled: true)]);
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(ExplicitObjectArrayTupleRows))]
    [DynamicData(nameof(ExplicitObjectArrayTupleMetadataRows))]
    public void ExplicitObjectArrayTupleFolded(object[] values, CancellationToken token)
    {
        Assert.AreSequenceEqual(new object[] { 89, "tuple-array" }, values);
        TokenAssertions.Explicit(TestContext, token, [values, new CancellationToken(canceled: true)]);
    }

    [ExplicitTokenTestMethod]
    public void CustomInvocation(CancellationToken token)
        => TokenAssertions.Explicit(TestContext, token, null);

    [TestMethod]
    [CombinatorialData]
    public void CombinatorialTokenOnly(CancellationToken token)
        => TokenAssertions.Injected(TestContext, token, []);

    public static IEnumerable<object[]> PlainRows => [new object[] { 19, "dynamic" }];

    public static IEnumerable<int> TypedRows => [23, 29];

    public static IEnumerable<(int Number, string Label)> TupleRows => [(31, "tuple")];

    public static IEnumerable<object[]> ObjectRows => [new object[] { 37, "array" }];

    public static IEnumerable<object[]> WrappedObjectRows => [new object[] { new object[] { 17, "array" } }];

    public static IEnumerable<TestDataRow<(int, string)>> MetadataRows =>
    [
        new TestDataRow<(int, string)>((41, "row-a")) { DisplayName = "metadata row a" },
        new TestDataRow<(int, string)>((43, "row-b")) { DisplayName = "metadata row b" },
    ];

    public static IEnumerable<int> FoldedData => [47, 53];

    public static IEnumerable<TestDataRow<(int, CancellationToken)>> ExplicitTokenRows =>
    [
        new TestDataRow<(int, CancellationToken)>((71, new CancellationToken(canceled: true))) { DisplayName = "explicit user token" },
    ];

    public static IEnumerable<(int, CancellationToken)> ExplicitObjectTupleRows => [(83, new CancellationToken(canceled: true))];

    public static IEnumerable<TestDataRow<(int, CancellationToken)>> ExplicitObjectTupleMetadataRows =>
    [
        new TestDataRow<(int, CancellationToken)>((83, new CancellationToken(canceled: true))) { ArgumentsDisplayName = "wrapped tuple" },
    ];

    public static IEnumerable<(object[], CancellationToken)> ExplicitObjectArrayTupleRows =>
    [
        (new object[] { 89, "tuple-array" }, new CancellationToken(canceled: true)),
    ];

    public static IEnumerable<TestDataRow<(object[], CancellationToken)>> ExplicitObjectArrayTupleMetadataRows =>
    [
        new TestDataRow<(object[], CancellationToken)>((new object[] { 89, "tuple-array" }, new CancellationToken(canceled: true)))
        {
            ArgumentsDisplayName = "wrapped tuple",
        },
    ];
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ExplicitTokenTestMethodAttribute(
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = -1) : TestMethodAttribute(callerFilePath, callerLineNumber)
{
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        Assert.IsNull(testMethod.Arguments);
        return [await testMethod.InvokeAsync([new CancellationToken(canceled: true)])];
    }
}

internal static class TokenAssertions
{
    public static void Injected(TestContext context, CancellationToken token, object?[]? expectedData)
    {
        Assert.AreEqual(context.CancellationToken, token);
        Assert.IsFalse(token.IsCancellationRequested);
        AssertData(context, expectedData);
        int cancellations = 0;
        using CancellationTokenRegistration registration = token.Register(() => cancellations++);

        context.CancellationTokenSource.Cancel();

        Assert.AreEqual(1, cancellations);
        Assert.IsTrue(token.IsCancellationRequested);
    }

    public static void Explicit(TestContext context, CancellationToken token, object?[]? expectedData)
    {
        Assert.AreEqual(new CancellationToken(canceled: true), token);
        Assert.AreNotEqual(context.CancellationToken, token);
        AssertData(context, expectedData);
        int cancellations = 0;
        using CancellationTokenRegistration registration = token.Register(() => cancellations++);

        Assert.AreEqual(1, cancellations);
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.IsFalse(context.CancellationToken.IsCancellationRequested);
    }

    private static void AssertData(TestContext context, object?[]? expectedData)
    {
        if (expectedData is null)
        {
            Assert.IsNull(context.TestData);
        }
        else
        {
            Assert.AreSequenceEqual(expectedData, context.TestData!);
        }
    }
}

#file RetryTokenTests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class RetryTokenTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Retry(1, MillisecondsDelayBetweenRetries = 0)]
    public void RebindsForRetry(CancellationToken token)
    {
        TokenAssertions.Injected(TestContext, token, null);
        if (TestContext.TestRunCount == 1)
        {
            Assert.Fail("The first attempt deliberately fails after checking and canceling its injected token.");
        }

        Assert.AreEqual(2, TestContext.TestRunCount);
    }
}

#file InvalidDataTests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// These rows compile so the runtime's data-validation boundary is exercised.
#pragma warning disable MSTEST0014

[TestClass]
public sealed class InvalidDataTests
{
    [TestMethod]
    [DataRow()]
    public void MissingRealData(int value, CancellationToken token)
        => Assert.Fail("INVALID_SHAPE_BODY_EXECUTED");

    [TestMethod]
    [DataRow("wrong")]
    public void WrongRealData(int value, CancellationToken token)
        => Assert.Fail("INVALID_SHAPE_BODY_EXECUTED");

    [TestMethod]
    [DataRow(79, null, "extra")]
    public void ExtraRealData(int value, CancellationToken token)
        => Assert.Fail("INVALID_SHAPE_BODY_EXECUTED");
}
""";
    }
}
