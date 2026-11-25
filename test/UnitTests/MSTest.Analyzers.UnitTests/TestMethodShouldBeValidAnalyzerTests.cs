// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldBeValidAnalyzer,
    MSTest.Analyzers.TestMethodShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestMethodShouldBeValidAnalyzerTests
{
    [TestMethod]
    [DataRow("int", DisplayName = "UnrelatedScalar")]
    [DataRow("System.Collections.Generic.List<int>", DisplayName = "UnrelatedConstructedGeneric")]
    [DataRow("int[]", DisplayName = "UnrelatedArrayElement")]
    public async Task GenericParameter_NotUsedByAnyDataParameter_ReportsTheMethod(string parameterType)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void {|#0:Check|}<T>({{parameterType}} value) { }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"));
    }

    [TestMethod]
    public async Task WhenTestMethodIsPublic_NoDiagnostic()
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeIsAppliedToStaticMethod_DiagnosticWithoutFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [CustomTestMethod]
                public static void {|#0:MyTestMethod|}() { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestMethod"), code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeIsAppliedToValidMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [CustomTestMethod]
                public void MyTestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DerivedNonPublicGenericTest_FixPreservesInferableTypeParameter()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod, DataRow(1)]
                private void {|#0:Check|}<T>(T value) { }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod, DataRow(1)]
                public void Check<T>(T value) { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"), fixedCode);
    }

    [TestMethod]
    public async Task UninferableGenericTest_ReferencedTypeParameterDoesNotOfferFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                [TestMethod]
                public void {|#0:Check|}<T>() { _ = typeof(T); }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"), code);
    }

    [TestMethod]
    public async Task UninferableGenericTest_CalledWithTypeArguments_DoesNotOfferFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod]
                public void {|#0:Check|}<T>() { }

                public void Call() => Check<int>();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"), code);
    }

    [TestMethod]
    public async Task DerivedAsyncVoidTest_WithoutTaskImport_DoesNotOfferFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Task { }
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod]
                public async void {|#0:Check|}() => await System.Threading.Tasks.Task.Yield();
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"), code);
    }

    [TestMethod]
    public async Task DerivedSignatureChangesWithCallersOrOverrides_DoNotOfferFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            public class Base
            {
                public virtual void Check() { }
            }
            [TestClass]
            public class Tests : Base
            {
                [CustomTestMethod]
                public override async void {|#0:Check|}() => await System.Threading.Tasks.Task.Yield();

                [CustomTestMethod]
                public static void {|#1:StaticCheck|}() { }

                public void Call() => Tests.StaticCheck();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [VerifyCS.Diagnostic().WithLocation(0).WithArguments("Check"), VerifyCS.Diagnostic().WithLocation(1).WithArguments("StaticCheck")],
            code);
    }

    [TestMethod]
    public async Task DerivedTestWithReturnValue_DoesNotOfferUnsafeFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            [TestClass]
            public class Tests
            {
                [CustomTestMethod]
                public int {|#0:Expression|}() => 5;

                [CustomTestMethod]
                public int {|#1:Block|}() { return Verify(); }

                private int Verify() => 1;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [VerifyCS.Diagnostic().WithLocation(0).WithArguments("Expression"), VerifyCS.Diagnostic().WithLocation(1).WithArguments("Block")],
            code);
    }

    [TestMethod]
    public async Task DerivedTestAttributeOnOperator_DiagnosticWithoutFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomTestMethodAttribute : TestMethodAttribute { }
            public class Tests
            {
                [CustomTestMethod]
                public static Tests operator {|#0:+|}(Tests left, Tests right) => left;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("op_Addition"), code);
    }

    [DataRow("protected")]
    [DataRow("internal")]
    [DataRow("internal protected")]
    [DataRow("private")]
    [TestMethod]
    public async Task WhenTestMethodIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                {{accessibility}} void [|MyTestMethod1|]()
                {
                }

                [TestMethod]
                {{accessibility}} async Task [|MyTestMethod2|]()
                {
                }
            }
            """;

        string fixedCode =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod1()
                {
                }

                [TestMethod]
                public async Task MyTestMethod2()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsNotPublicAndNotTestMethod_NoDiagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void PrivateMethod()
                {
                }

                protected void ProtectedMethod()
                {
                }

                internal protected void InternalProtectedMethod()
                {
                }

                internal void InternalMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public static void [|MyTestMethod|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsAbstract_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestMethod]
                public abstract void [|MyTestMethod|]();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsGeneric_CanBeInferred_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod1<T>(T[] t)
                {
                }

                [TestMethod]
                public void TestMethod2<T>(List<T> t)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void [|MyTestMethod|]<T>()
                {
                }

                [TestMethod]
                [DataRow(0)]
                public void MyTestMethod<T>(T t)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                ~[|MyTestClass|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

#if NET
    [TestMethod]
    public async Task WhenTestMethodReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public int [|MyTestMethod0|]()
                {
                    return 42;
                }

                [TestMethod]
                public string [|MyTestMethod1|]()
                {
                    return "42";
                }

                [TestMethod]
                public Task<int> [|MyTestMethod2|]()
                {
                    return Task.FromResult(42);
                }

                [TestMethod]
                public ValueTask<int> [|MyTestMethod3|]()
                {
                    return ValueTask.FromResult(42);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod0()
                {
                }

                [TestMethod]
                public Task MyTestMethod1()
                {
                    return Task.CompletedTask;
                }

                [TestMethod]
                public ValueTask MyTestMethod2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif

    [TestMethod]
    public async Task WhenTestMethodIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void [|MyTestMethod|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsInternalAndDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass2
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass3
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsPrivateAndDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                private void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;
            
            [assembly: DiscoverInternals]
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
