// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public class AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzerTests
{
    private static async Task VerifyAsync(string code, bool publishAot, params DiagnosticResult[] expected)
        => await VerifyWithPropertiesAsync(code, expected, ("build_property.PublishAot", publishAot ? "true" : "false"));

    private static async Task VerifyWithPropertiesAsync(string code, DiagnosticResult[] expected, params (string Key, string Value)[] properties)
    {
        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        string propertyLines = string.Join(Environment.NewLine, properties.Select(p => $"{p.Key} = {p.Value}"));
        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            $"""
            is_global = true

            {propertyLines}
            """));

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenRunAOTCompilationAndAttributeIsUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: [|AssemblyFixtureProvider(typeof(GlobalFixtures))|]]

            public static class GlobalFixtures
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }
            """;

        await VerifyWithPropertiesAsync(code, [], ("build_property.RunAOTCompilation", "true"));
    }

    [TestMethod]
    public async Task WhenPublishAotAndAttributeIsUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: [|AssemblyFixtureProvider(typeof(GlobalFixtures))|]]

            public static class GlobalFixtures
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }
            """;

        await VerifyAsync(code, publishAot: true);
    }

    [TestMethod]
    public async Task WhenPublishAotAndAttributeUsedMultipleTimes_DiagnosticOnEach()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: [|AssemblyFixtureProvider(typeof(FixturesOne))|]]
            [assembly: [|AssemblyFixtureProvider(typeof(FixturesTwo))|]]

            public static class FixturesOne
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }

            public static class FixturesTwo
            {
                [AssemblyCleanup]
                public static void Cleanup()
                {
                }
            }
            """;

        await VerifyAsync(code, publishAot: true);
    }

    [TestMethod]
    public async Task WhenPublishAotAndAttributeOnReferencedAssembly_Diagnostic()
    {
        string providerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: AssemblyFixtureProvider(typeof(GlobalFixtures))]

            public static class GlobalFixtures
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTests
            {
                [TestMethod]
                public void MyTest()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = consumerCode,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            """
            is_global = true

            build_property.PublishAot = true
            """));

        var providerProject = new ProjectState("FixtureLib", LanguageNames.CSharp, "/FixtureLib/", "cs");
        providerProject.Sources.Add(("Provider.cs", providerCode));
        providerProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.ParallelizeAttribute).Assembly.Location));
        providerProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext).Assembly.Location));
        test.TestState.AdditionalProjects.Add("FixtureLib", providerProject);
        test.TestState.AdditionalProjectReferences.Add("FixtureLib");

        // The attribute lives in a referenced assembly, so the diagnostic has no source location.
        test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer.Rule).WithNoLocation());

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenNotPublishAot_AttributeOnReferencedAssembly_NoDiagnostic()
    {
        string providerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: AssemblyFixtureProvider(typeof(GlobalFixtures))]

            public static class GlobalFixtures
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTests
            {
                [TestMethod]
                public void MyTest()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = consumerCode,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            """
            is_global = true

            build_property.PublishAot = false
            """));

        var providerProject = new ProjectState("FixtureLib", LanguageNames.CSharp, "/FixtureLib/", "cs");
        providerProject.Sources.Add(("Provider.cs", providerCode));
        providerProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.ParallelizeAttribute).Assembly.Location));
        providerProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext).Assembly.Location));
        test.TestState.AdditionalProjects.Add("FixtureLib", providerProject);
        test.TestState.AdditionalProjectReferences.Add("FixtureLib");

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenNotPublishAot_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: AssemblyFixtureProvider(typeof(GlobalFixtures))]

            public static class GlobalFixtures
            {
                [AssemblyInitialize]
                public static void Init(TestContext context)
                {
                }
            }
            """;

        await VerifyAsync(code, publishAot: false);
    }

    [TestMethod]
    public async Task WhenPublishAotAndAttributeNotUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTests
            {
                [TestMethod]
                public void MyTest()
                {
                }
            }
            """;

        await VerifyAsync(code, publishAot: true);
    }
}
