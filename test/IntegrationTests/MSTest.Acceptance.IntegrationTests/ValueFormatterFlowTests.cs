// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance tests for <c>Assert.AddValueFormatter</c> scope flow-down across the
/// AssemblyInitialize / ClassInitialize / TestMethod lifecycle. The registry is backed by
/// <c>AsyncLocal&lt;T&gt;</c>, so these tests run a real MSTest process to prove that a formatter
/// registered in an outer scope (assembly / class) is visible in the inner scopes (class / test),
/// that inner registrations layer on top of outer ones, and that a test-local registration is
/// isolated to the test that created it. See https://github.com/microsoft/testfx/issues/9089.
/// </summary>
[TestClass]
public sealed class ValueFormatterFlowTests : AcceptanceTestBase<ValueFormatterFlowTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ValueFormatterScopesFlowDownCorrectly(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The custom test assets assert the flow-down behavior themselves; a non-zero exit code or a
        // failing test surfaces the violated expectation (the rendered failure messages are echoed to
        // stdout to aid diagnosis).
        testHostResult.AssertExitCodeIs(0);

        // AssemblyScopeTests: 1, ClassScopeTests: 1, TestLocalScopeTests: 2 => 4 passing tests.
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "ValueFormatterFlow";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file ValueFormatterFlow.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <!-- Assert.AddValueFormatter is an experimental API. -->
    <NoWarn>$(NoWarn);MSTESTEXP</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// A type whose default rendering ("DEFAULT:<name>") is distinct from any registered formatter so the
// rendered failure message reveals exactly which formatter (if any) handled the value.
public sealed class Marker
{
    public Marker(string name) => Name = name;

    public string Name { get; }

    public override string ToString() => "DEFAULT:" + Name;
}

internal static class FormatterProbe
{
    // Forces an Assert.AreEqual failure between two distinct Markers and returns the rendered message
    // (which routes both values through AssertionValueRenderer.RenderValue and therefore the registry).
    public static string CaptureFailureMessage(string expected, string actual)
    {
        try
        {
            Assert.AreEqual(new Marker(expected), new Marker(actual));
        }
        catch (AssertFailedException ex)
        {
            return ex.Message;
        }

        throw new InvalidOperationException("Assert.AreEqual unexpectedly passed for two distinct Markers.");
    }
}

// Registers an assembly-scoped formatter and verifies it flows down to a test in a class that has no
// ClassInitialize formatter of its own.
[TestClass]
public sealed class AssemblyScopeTests
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
        => Assert.AddValueFormatter<Marker>(m => "asm:" + m.Name);

    [TestMethod]
    public void AssemblyFormatterFlowsToTest()
    {
        string message = FormatterProbe.CaptureFailureMessage("x", "y");
        Console.WriteLine("[AssemblyFormatterFlowsToTest] " + message);

        Assert.Contains("asm:x", message);
        Assert.Contains("asm:y", message);
    }
}

// Registers a class-scoped formatter and verifies it both wins over and layers on top of the
// assembly-scoped formatter.
[TestClass]
public sealed class ClassScopeTests
{
    [ClassInitialize]
    public static void ClassInit(TestContext context)
        // Handles every Marker except "fall", which it defers (returns null) so the underlying
        // assembly formatter handles it -- proving the chain layers class-over-assembly.
        => Assert.AddValueFormatter<Marker>(m => m.Name == "fall" ? null : "class:" + m.Name);

    [TestMethod]
    public void ClassFormatterWinsAndLayersOnAssemblyFormatter()
    {
        string message = FormatterProbe.CaptureFailureMessage("a", "fall");
        Console.WriteLine("[ClassFormatterWinsAndLayersOnAssemblyFormatter] " + message);

        // The class formatter wins for "a"...
        Assert.Contains("class:a", message);
        // ...and defers "fall" to the assembly formatter underneath it.
        Assert.Contains("asm:fall", message);
    }
}

// Verifies a test-local registration applies within its test and does not leak into a sibling test.
[TestClass]
public sealed class TestLocalScopeTests
{
    [TestMethod]
    public void LocalFormatterAppliesWithinTest()
    {
        using (Assert.AddValueFormatter<Marker>(m => "local:" + m.Name))
        {
            string message = FormatterProbe.CaptureFailureMessage("p", "q");
            Console.WriteLine("[LocalFormatterAppliesWithinTest] " + message);

            Assert.Contains("local:p", message);
            Assert.Contains("local:q", message);
        }
    }

    [TestMethod]
    public void LocalFormatterDoesNotLeakIntoSiblingTest()
    {
        // No local formatter registered here; the local formatter from the sibling test must not be
        // visible. The assembly formatter still applies (assembly scope), so values render as "asm:".
        string message = FormatterProbe.CaptureFailureMessage("p", "q");
        Console.WriteLine("[LocalFormatterDoesNotLeakIntoSiblingTest] " + message);

        Assert.DoesNotContain("local:", message);
        Assert.Contains("asm:p", message);
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
