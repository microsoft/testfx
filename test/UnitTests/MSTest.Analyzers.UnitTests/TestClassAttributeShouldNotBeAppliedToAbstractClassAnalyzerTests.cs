// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassAttributeIsAppliedToAbstractClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [{|#0:TestClass|}]
            public abstract class BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }

    [TestMethod]
    public async Task WhenTestClassAttributeIsAppliedToAbstractRecord_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [{|#0:TestClass|}]
            public abstract record BaseTests;
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }

    [TestMethod]
    public async Task WhenDerivedTestClassAttributeIsAppliedToAbstractClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class CustomTestClassAttribute : TestClassAttribute
            {
            }

            [{|#0:CustomTestClass|}]
            public abstract class BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }

    [TestMethod]
    public async Task WhenTestClassAttributeIsAppliedToConcreteClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassAttributeIsAppliedToStaticClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class AssemblyFixtures
            {
                [AssemblyInitialize]
                public static void Initialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAbstractClassHasNoTestClassAttribute_NoDiagnostic()
    {
        string code = """
            public abstract class BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassAttributeIsAppliedToMustInheritClass_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <{|#0:TestClass|}>
            Public MustInherit Class BaseTests
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic(TestClassAttributeShouldNotBeAppliedToAbstractClassAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }
}
