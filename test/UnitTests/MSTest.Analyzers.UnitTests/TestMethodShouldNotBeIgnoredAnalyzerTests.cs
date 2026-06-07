// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldNotBeIgnoredAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestMethodShouldNotBeIgnoredAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodIsNotIgnored_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task UsingIgnoreWithoutTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsIgnored_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                [TestMethod]
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldNotBeIgnoredAnalyzer.TestMethodShouldNotBeIgnoredRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeIsIgnored_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                [DerivedTestMethod]
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldNotBeIgnoredAnalyzer.TestMethodShouldNotBeIgnoredRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenDataTestMethodIsIgnored_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                [DataTestMethod]
                [DataRow(1)]
                public void {|#0:MyTestMethod|}(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldNotBeIgnoredAnalyzer.TestMethodShouldNotBeIgnoredRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenMultipleTestMethodsAndOnlyOneIsIgnored_DiagnosticOnlyForIgnoredMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NotIgnoredMethod()
                {
                }

                [Ignore]
                [TestMethod]
                public void {|#0:IgnoredMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldNotBeIgnoredAnalyzer.TestMethodShouldNotBeIgnoredRule)
                .WithLocation(0)
                .WithArguments("IgnoredMethod"));
    }

    [TestMethod]
    public async Task WhenOnlyClassLevelIgnoreWithNoMethodLevelIgnore_NoDiagnostic()
    {
        // MSTEST0015 only checks for [Ignore] on the method itself.
        // A class-level [Ignore] silences the whole class but is not flagged by this rule.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenIgnoredTestMethodHasMessage_Diagnostic()
    {
        // MSTEST0015 fires even when [Ignore] has a justification message.
        // The presence of a message satisfies MSTEST0066 (IgnoreShouldHaveJustification),
        // but the method is still ignored and MSTEST0015 still applies.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore("Tracked by #12345")]
                [TestMethod]
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldNotBeIgnoredAnalyzer.TestMethodShouldNotBeIgnoredRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }
}
