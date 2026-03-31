// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UnitTests.UnusedParameterSuppressorTests.WarnForUnusedParameters,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class UnusedParameterSuppressorTests
{
    [TestMethod]
    public async Task AssemblyInitializeWithUnusedTestContext_DiagnosticIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SomeClass
            {
                [AssemblyInitialize]
                public static void Initialize(TestContext {|#0:context|})
                {
                    // TestContext parameter is unused but required by MSTest
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        // Verify issue is suppressed with suppressor
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(true),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task ClassInitializeWithUnusedTestContext_DiagnosticIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SomeClass
            {
                [ClassInitialize]
                public static void Initialize(TestContext {|#0:context|})
                {
                    // TestContext parameter is unused but required by MSTest
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        // Verify issue is suppressed with suppressor
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(true),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task GlobalTestInitializeWithUnusedTestContext_DiagnosticIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class SomeClass
            {
                [GlobalTestInitialize]
                public static void Initialize(TestContext [|context|])
                {
                    // TestContext parameter is unused but required by MSTest
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
        }.RunAsync();

        // Verify issue is suppressed with suppressor
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
        }.RunAsync();
    }

    [TestMethod]
    public async Task GlobalTestCleanupWithUnusedTestContext_DiagnosticIsSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class SomeClass
            {
                [GlobalTestCleanup]
                public static void Cleanup(TestContext [|context|])
                {
                    // TestContext parameter is unused but required by MSTest
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
        }.RunAsync();

        // Verify issue is suppressed with suppressor
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
        }.RunAsync();
    }

    [TestMethod]
    public async Task TestMethodWithUnusedParameter_DiagnosticIsNotSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SomeClass
            {
                [TestMethod]
                public void TestMethod(int {|#0:unusedParam|})
                {
                    // This should not be suppressed
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("unusedParam")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        // Verify issue is still reported with suppressor (not suppressed)
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("unusedParam")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task RegularMethodWithUnusedTestContext_DiagnosticIsNotSuppressed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SomeClass
            {
                public void RegularMethod(TestContext {|#0:context|})
                {
                    // This should not be suppressed as it's not AssemblyInitialize or ClassInitialize
                }
            }
            """;

        // Verify issue is reported without suppressor
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();

        // Verify issue is still reported with suppressor (not suppressed)
        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic(WarnForUnusedParameters.Rule)
                    .WithLocation(0)
                    .WithArguments("context")
                    .WithIsSuppressed(false),
            },
        }.RunAsync();
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1041:Compiler extensions should be implemented in assemblies targeting netstandard2.0", Justification = "For suppression test only.")]
    public class WarnForUnusedParameters : DiagnosticAnalyzer
    {
        [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1017:DiagnosticId for analyzers must be a non-null constant.", Justification = "For suppression test only.")]
        public static readonly DiagnosticDescriptor Rule = new(UnusedParameterSuppressor.Rule.SuppressedDiagnosticId, "Remove unused parameter", "Remove unused parameter '{0}' if it is not part of a shipped public API", "Style", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Parameter);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            if (context.Symbol is IParameterSymbol parameter)
            {
                // Simple mock: report all parameters as unused for testing purposes
                var diagnostic = Diagnostic.Create(
                    Rule,
                    parameter.Locations.FirstOrDefault(),
                    parameter.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    internal sealed class TestWithSuppressor : VerifyCS.Test
    {
        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
        {
            foreach (DiagnosticAnalyzer analyzer in base.GetDiagnosticAnalyzers())
            {
                yield return analyzer;
            }

            yield return new UnusedParameterSuppressor();
        }
    }
}
