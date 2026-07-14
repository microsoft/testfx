// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public class AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzerTests
{
    private static async Task VerifyAsync(string code, bool publishAot, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            $"""
            is_global = true

            build_property.PublishAot = {(publishAot ? "true" : "false")}
            """));

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
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
