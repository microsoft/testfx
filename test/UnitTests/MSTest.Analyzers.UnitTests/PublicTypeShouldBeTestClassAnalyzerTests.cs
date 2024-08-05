// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PublicTypeShouldBeTestClassAnalyzer,
    MSTest.Analyzers.PublicTypeShouldBeTestClassFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class PublicTypeShouldBeTestClassAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenClassIsPublicAndNotTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class [|MyTestClass|]
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            fixedCode);
    }

    public async Task WhenClassIsPublicAndNotClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public struct MyTestStruct
            {
            }

            public interface MyTestInterface
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
           code,
           code);
    }

    public async Task WhenClassIsPublicAndAbstract_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
           code,
           code);
    }

    public async Task WhenClassIsPublicAndStatic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
           code,
           code);
    }

    public async Task WhenTypeIsNotPublicAndNotTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class MyClass
            {
            }

            internal struct MyStruct
            {
            }

            internal interface MyInterface
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
           code,
           code);
    }
}
