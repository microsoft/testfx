// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace MSTest.Analyzers.Test;

public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic()"/>
    public static DiagnosticResult Diagnostic()
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic()"/>
    public static DiagnosticResult DiagnosticIgnoringAdditionalLocations()
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic().WithOptions(DiagnosticOptions.IgnoreAdditionalLocations);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(string)"/>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(descriptor);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    public static async Task VerifyAnalyzerAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source, params DiagnosticResult[] expected)
        => await VerifyAnalyzerAsync(source, useMSTestV4: false, expected);

    public static async Task VerifyAnalyzerAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source, bool useMSTestV4, params DiagnosticResult[] expected)
    {
        var test = new Test(useMSTestV4)
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, string)"/>
    public static async Task VerifyCodeFixAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source,
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string fixedSource,
        bool useMSTestV4 = false)
        => await VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource, useMSTestV4);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult, string)"/>
    public static async Task VerifyCodeFixAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source,
        DiagnosticResult expected,
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string fixedSource,
        bool useMSTestV4 = false)
        => await VerifyCodeFixAsync(source, [expected], fixedSource, useMSTestV4);

    /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult[], string)"/>
    public static async Task VerifyCodeFixAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source,
        DiagnosticResult[] expected,
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string fixedSource,
        bool useMSTestV4 = false)
    {
        var test = new Test(useMSTestV4)
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    public static Task VerifyCodeFixWithLegacyMSTestAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source,
        DiagnosticResult expected,
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string fixedSource)
        => VerifyCodeFixWithLegacyMSTestAsync(source, [expected], fixedSource);

    // Bind legacy calls to a published contract, but check the final migration against the current framework.
    // Intermediate fixes still need the old types, so their package also includes the modern Assert targets.
    public static async Task VerifyCodeFixWithLegacyMSTestAsync(
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string source,
        DiagnosticResult[] expected,
#if NETCOREAPP
        [StringSyntax("C#-test")]
#endif
        string fixedSource)
    {
        var test = new Test(useMSTestV4: true)
        {
            ReferenceAssemblies = AnalyzerTestReferenceAssemblies.MSTestWithLegacyAssertions,
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.FixedState.ReferenceAssemblies = AnalyzerTestReferenceAssemblies.Default;
        test.FixedState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location));
        test.FixedState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(TestContext).Assembly.Location));
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }
}
