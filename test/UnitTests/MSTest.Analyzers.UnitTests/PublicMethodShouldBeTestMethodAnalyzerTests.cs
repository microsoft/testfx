// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PublicMethodShouldBeTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PublicMethodShouldBeTestMethodAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenMethodIsPrivate_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsPublicButWithInvalidTestMethodSignature_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }


    public async Task WhenMethodIsPublicAndMarkedAsTestMethod_NoDiagnostic()
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

    public async Task WhenMethodIsPrivateButNotInsideTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsPublicAndMarkedAsDerivedTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [DerivedTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethodWithInheritanceFromBaseTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Base
            {
            }
            
            public class MyTestClass : Base
            {
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsPrivateAndNotMarkedAsTestMethodWithInheritedTestClassAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            public class DerivedTestClass : TestClassAttribute
            {
            }
            
            [DerivedTestClass]
            public class MyTestClass
            {
                private void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(PublicMethodShouldBeTestMethodAnalyzer.PublicMethodShouldBeTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenMethodIsPublicAndNotMarkedAsTestMethodWithInheritedTestClassAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            public class DerivedTestClass : TestClassAttribute
            {
            }
            
            [DerivedTestClass]
            public class MyTestClass
            {
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(PublicMethodShouldBeTestMethodAnalyzer.PublicMethodShouldBeTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }
}
