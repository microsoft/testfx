// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestClassShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenClassIsPublicAndTestClass_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassIsInternalAndTestClass_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            internal class {|#0:MyTestClass|}
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    [Arguments("private")]
    [Arguments("internal")]
    public async Task WhenClassIsInnerAndNotPublicTestClass_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class OuterClass
            {
                [TestClass]
                {{accessibility}} class {|#0:MyTestClass|}
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenClassIsInternalAndNotTestClass_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassIsPublicAndTestClassAsInnerOfInternalClass_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class OuterClass
            {
                [TestClass]
                public class {|#0:MyTestClass|}
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenClassIsStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenClassIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}<T>
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenClassIsNotGenericButAsOuterGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClass<T>
            {
                [TestClass]
                public class {|#0:MyTestClass|}
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenMultipleViolations_MultipleDiagnostics()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            internal static class {|#0:{|#1:{|#2:MyTestClass|}|}|}<T>
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(1)
                .WithArguments("MyTestClass"),
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(2)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenDiscoverInternalsAndTypeIsInternal_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDiscoverInternalsAndTypeIsPrivate_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            public class A
            {
                [TestClass]
                private class {|#0:MyTestClass|}
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldBeValidAnalyzer.PublicOrInternalRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }
}
