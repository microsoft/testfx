// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UnitTests.NonNullableReferenceNotInitializedSuppressorTests.DoNothingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class NonNullableReferenceNotInitializedSuppressorTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task TestContextPropertyOnTestClass_DiagnosticIsSuppressed()
    {
        string code = @"
#nullable enable

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public TestContext [|TestContext|] { get; set; }
}
";

        // Verify issues are reported
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
        }.RunAsync();

        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
        }.RunAsync();
    }

    public async Task TestContextPropertyOnNonTestClass_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class SomeClass
{
    public TestContext [|TestContext|] { get; set; }
}
";

        // Verify issues are reported
        await new VerifyCS.Test
        {
            TestState = { Sources = { code } },
        }.RunAsync();

        await new TestWithSuppressor
        {
            TestState = { Sources = { code } },
        }.RunAsync();
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "For suppression test only.")]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1041:Compiler extensions should be implemented in assemblies targeting netstandard2.0", Justification = "For suppression test only.")]
    public class DoNothingAnalyzer : DiagnosticAnalyzer
    {
        [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1017:DiagnosticId for analyzers must be a non-null constant.", Justification = "For suppression test only.")]
        public static readonly DiagnosticDescriptor Rule = new(NonNullableReferenceNotInitializedSuppressor.Rule.SuppressedDiagnosticId, "Title", "Message", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
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

            yield return new NonNullableReferenceNotInitializedSuppressor();
        }
    }
}
