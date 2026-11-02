// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferAssertFailOverAlwaysFalseConditionsAnalyzer,
    MSTest.Analyzers.PreferAssertFailOverAlwaysFalseConditionsFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.PreferAssertFailOverAlwaysFalseConditionsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferAssertFailOverAlwaysFalseConditionsAnalyzerTests
{
    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterIsNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int? var = null;
                    Assert.IsNull(var);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterIsNotNullable_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int var = 3;
                    [|Assert.IsNull(var)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int var = 3;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterAsPropertySymbolIsNotNullable_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int var = 3;

                [TestMethod]
                public void Test()
                {
                    [|Assert.IsNull(var)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int var = 3;

                [TestMethod]
                public void Test()
                {
                    Assert.Fail();
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterAsPropertySymbolIsNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class MyTestClass
            {
                private int? var = 3;

                [TestMethod]
                public void Test()
                {
                    Assert.IsNull(var);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass obj = new ObjectClass();
                    Assert.IsNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNotNullable_WithoutEnableNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass obj = new ObjectClass();
                    Assert.IsNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueParameterAsReferenceObjectIsNullable_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #nullable enable
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    ObjectClass? obj = null;

                    Assert.IsNull(obj);
                }
            }

            public class ObjectClass
            {

            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedTrue_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedTrue_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedTrue_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(message: "message", condition: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedFalse_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedFalse_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(false, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedFalse_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(message: "message", condition: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(message: "message", condition: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedTrue_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedTrue_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(true, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedTrue_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsFalse(message: "message", condition: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedFalse_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedFalse_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedFalse_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(message: "message", condition: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(message: "message", condition: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedNull_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(null)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedNull_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(null, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedNull_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsNotNull(message: "message", value: null)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(GetObject());
                }

                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(GetObject(), "message");
                }

                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsNotNullIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsNotNull(message: "message", value: GetObject());
                }

                private static object GetObject() => new object();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(true, true, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: true, actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(expected: true, message: "message", actual: true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(true, false, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(message: "message", expected: true, actual: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(expected: true, message: "message", actual: false)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(GetBoolean(), GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(GetBoolean(), GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(message: "message", expected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedUnknown_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(expected: GetBoolean(), message: "message", actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedEqual_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(true, true, "message")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail("message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageFirst_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(message: "message", notExpected: true, actual: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedEqual_WithMessageSecond_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(notExpected: true, message: "message", actual: true)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(message: "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedNonEqual_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(true, false, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: true, actual: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedNonEqual_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(notExpected: true, message: "message", actual: false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedUnknown_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(GetBoolean(), GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(GetBoolean(), GetBoolean(), "message");
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessageFirst_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedUnknown_WithMessageSecond_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(message: "message", notExpected: GetBoolean(), actual: GetBoolean());
                }

                private static bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ReferenceTypeInNullableDisableContext_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics.CodeAnalysis;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class MyTests
            {
            #nullable disable
                [TestMethod]
                public void Repro()
                {
                    Assert.IsFalse(TryGetPool(poolKey: null, out var pool));
                    Assert.IsNull(pool);
                }
            #nullable restore

            #nullable enable
                internal bool TryGetPool(string? poolKey, [MaybeNullWhen(false)] out object pool)
                {
                    if (poolKey is not null)
                    {
                        pool = new object();
                        return true;
                    }

                    pool = null;
                    return false;
                }
            #nullable restore
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenIsNullAssertion_ValueTypeParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    int value = 42;
                    [|Assert.IsNull(value)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void Test()
                {
                    int value = 42;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueIsPassedFalse_MultiLineWithDifferentIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(
                        false,
                        "this will always fail")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(
                        "this will always fail");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqual_MultiLineWithDifferentIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreEqual(
                        true,
                        false,
                        "values will never match")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail(
                        "values will never match");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocal_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreNotEqual(x, x)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(int x)
                {
                    [|Assert.AreNotEqual(x, x)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(int x)
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameField_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int _value;

                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreNotEqual(_value, _value)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int _value;

                [TestMethod]
                public void TestMethod()
                {
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreNotEqual(x, x, "should differ")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.Fail("should differ");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedDifferentLocals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    int y = 2;
                    Assert.AreNotEqual(x, y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameMethodCall_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreNotEqual(GetValue(), GetValue());
                }

                private int GetValue() => 1;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotSameIsPassedSameLocal_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreNotSame(x, x)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotSameIsPassedSameLocalNamed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreNotSame(actual: x, notExpected: x)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotSameIsPassedSameLocalWithMessage_Diagnostic()
    {
        // Mirror the AreNotEqual(..., "should differ") test below so the code fix's
        // message-forwarding path is exercised for AreNotSame too.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    [|Assert.AreNotSame(x, x, "references must differ")|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    Assert.Fail("references must differ");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameProperty_Diagnostic()
    {
        // Exercises IsEquivalentReferenceTo's IPropertyReferenceOperation arm directly so a
        // regression that breaks property equivalence detection is caught here.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Holder
            {
                public int Value { get; set; }
                public Holder Inner { get; set; } = new Holder();
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var h = new Holder();
                    [|Assert.AreNotEqual(h.Value, h.Value)|];
                    [|Assert.AreNotEqual(h.Inner.Value, h.Inner.Value)|];
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Holder
            {
                public int Value { get; set; }
                public Holder Inner { get; set; } = new Holder();
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var h = new Holder();
                    Assert.Fail();
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotSameIsPassedDifferentLocals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new object();
                    var y = new object();
                    Assert.AreNotSame(x, y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithUserDefinedConversion_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new Wrapper();
                    Assert.AreNotEqual((int)x, (int)x);
                }

                private sealed class Wrapper
                {
                    private static int _counter;
                    public static explicit operator int(Wrapper value) => ++_counter;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithOverriddenEquals_NoDiagnostic()
    {
        // The type overrides object.Equals, so Assert.AreNotEqual routes through user code and the
        // self-comparison is a legitimate way to exercise the equality contract (see issue #9972).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    Assert.AreNotEqual(x, x);
                }

                private sealed class MyType
                {
                    public override bool Equals(object obj) => false;
                    public override int GetHashCode() => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithoutCustomEquality_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    [|Assert.AreNotEqual(x, x)|];
                }

                private sealed class MyType
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var x = new MyType();
                    Assert.Fail();
                }

                private sealed class MyType
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualIsPassedNonEqualConstantsWithCustomComparer_NoDiagnostic()
    {
        // A caller-supplied comparer can return any result, so the comparison is not provably always false.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreEqual(1, 2, new AlwaysEqualComparer());
                }

                private sealed class AlwaysEqualComparer : IEqualityComparer<int>
                {
                    public bool Equals(int x, int y) => true;
                    public int GetHashCode(int obj) => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithCustomComparer_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.AreNotEqual(x, x, new AlwaysEqualComparer());
                }

                private sealed class AlwaysEqualComparer : IEqualityComparer<int>
                {
                    public bool Equals(int x, int y) => true;
                    public int GetHashCode(int obj) => 0;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithNullComparer_Diagnostic()
    {
        // MSTest treats a null comparer as EqualityComparer<T>.Default, so the self-comparison is still
        // provably always false and must be flagged.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreNotEqual(x, x, (IEqualityComparer<int>)null)|];
                }
            }
            """;
        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualIsPassedSameLocalWithDefaultEqualityComparer_Diagnostic()
    {
        // EqualityComparer<T>.Default passed explicitly is the default comparer, so the self-comparison is
        // still provably always false and must be flagged.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    [|Assert.AreNotEqual(x, x, EqualityComparer<int>.Default)|];
                }
            }
            """;
        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 1;
                    Assert.Fail();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    [DataRow("Assert.IsExactInstanceOfType<AbstractBase>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractBase))")]
    [DataRow("Assert.IsExactInstanceOfType(expectedType: typeof(AbstractBase), value: value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, (typeof(AbstractBase)))")]
    [DataRow("Assert.IsExactInstanceOfType(value, (System.Type)(object)typeof(AbstractBase))")]
    [DataRow("Assert.IsExactInstanceOfType<AbstractGeneric<int>>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractGeneric<int>))")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractGeneric<>))")]
    [DataRow("Assert.IsExactInstanceOfType<System.IO.Stream>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(System.IO.Stream))")]
    public async Task WhenAssertIsExactInstanceOfTypeExpectsAbstractType_DiagnosticAndCodeFix(string assertion)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    [|{{assertion}}|];
                }

                private abstract class AbstractBase { }
                private abstract class AbstractGeneric<T> { }
            }
            """;
        string fixedCode = code.Replace($"[|{assertion}|]", "Assert.Fail()");

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    [DataRow("Assert.IsExactInstanceOfType<AbstractBase>(value, \"message\")", "Assert.Fail(\"message\")")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractBase), \"message\")", "Assert.Fail(\"message\")")]
    [DataRow("Assert.IsExactInstanceOfType<AbstractBase>(message: \"message\", value: value)", "Assert.Fail(message: \"message\")")]
    [DataRow("Assert.IsExactInstanceOfType(message: \"message\", expectedType: typeof(AbstractBase), value: value)", "Assert.Fail(message: \"message\")")]
    [DataRow("Assert.IsExactInstanceOfType<AbstractBase>(value, $\"message {value}\")", "Assert.Fail($\"message {value}\")")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractBase), $\"message {value}\")", "Assert.Fail($\"message {value}\")")]
    public async Task WhenAssertIsExactInstanceOfTypeExpectsAbstractTypeWithMessage_CodeFixPreservesMessage(string assertion, string fixedAssertion)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    [|{{assertion}}|];
                }

                private abstract class AbstractBase { }
            }
            """;
        string fixedCode = code.Replace($"[|{assertion}|]", fixedAssertion);

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    [DataRow("public void Test(object value) { _ = [|Assert.IsExactInstanceOfType<AbstractBase>(value)|]; }", DisplayName = "Discarded result")]
    [DataRow("public void Test(object value) { var result = [|Assert.IsExactInstanceOfType<AbstractBase>(value)|]; }", DisplayName = "Assigned result")]
    [DataRow("public AbstractBase Test(object value) => [|Assert.IsExactInstanceOfType<AbstractBase>(value)|];", DisplayName = "Expression-bodied return")]
    [DataRow("public AbstractBase Test(object value) { return [|Assert.IsExactInstanceOfType<AbstractBase>(value)|]; }", DisplayName = "Returned result")]
    [DataRow("public void Test(object value) { Consume([|Assert.IsExactInstanceOfType<AbstractBase>(value)|]); }", DisplayName = "Argument result")]
    public async Task WhenAssertIsExactInstanceOfTypeExpectsAbstractTypeAndResultIsUsed_DiagnosticWithoutCodeFix(string member)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                {{member}}

                private static void Consume(object value) { }
                public abstract class AbstractBase { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    [DataRow("Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsExactInstanceOfType<AbstractBase>(value)", "Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail()", "")]
    [DataRow("global::Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsExactInstanceOfType(value, typeof(AbstractBase))", "global::Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail()", "")]
    [DataRow("MSTestAssert.IsExactInstanceOfType<AbstractBase>(value)", "MSTestAssert.Fail()", "using MSTestAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;")]
    [DataRow("MSTestAssert.IsExactInstanceOfType(value, typeof(AbstractBase))", "MSTestAssert.Fail()", "using MSTestAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;")]
    [DataRow("IsExactInstanceOfType<AbstractBase>(value)", "global::Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail()", "using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;")]
    [DataRow("IsExactInstanceOfType(value, typeof(AbstractBase))", "global::Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail()", "using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;")]
    public async Task WhenAssertIsExactInstanceOfTypeUsesQualifiedOrImportedAssert_CodeFixTargetsMSTest(string assertion, string fixedAssertion, string imports)
    {
        string code = $$"""
            {{imports}}

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    [|{{assertion}}|];
                }

                private static void Fail() { }
                private abstract class AbstractBase { }
            }
            """;
        string fixedCode = code.Replace($"[|{assertion}|]", fixedAssertion);

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    [DataRow(
        "var message = new Assert.AssertIsExactInstanceOfTypeInterpolatedStringHandler(0, 0, value, typeof(AbstractBase), out _);",
        "Assert.IsExactInstanceOfType(value, typeof(AbstractBase), ref message)",
        DisplayName = "Non-generic ref handler")]
    [DataRow(
        "var message = new Assert.AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<AbstractBase>(0, 0, value, out _);",
        "Assert.IsExactInstanceOfType<AbstractBase>(value, ref message)",
        DisplayName = "Generic ref handler")]
    public async Task WhenAssertIsExactInstanceOfTypeUsesExplicitRefMessage_DiagnosticWithoutCodeFix(string declaration, string assertion)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    {{declaration}}
                    [|{{assertion}}|];
                }

                private abstract class AbstractBase { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsExactInstanceOfTypeMixesFixableAndUnfixableCalls_CodeFixLeavesUnfixableDiagnostics()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    [|Assert.IsExactInstanceOfType(value, typeof(AbstractBase))|];
                    _ = [|Assert.IsExactInstanceOfType<AbstractBase>(value)|];
                    var message = new Assert.AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<AbstractBase>(0, 0, value, out _);
                    [|Assert.IsExactInstanceOfType<AbstractBase>(value, ref message)|];
                }

                private abstract class AbstractBase { }
            }
            """;
        string fixedCode = code.Replace("[|Assert.IsExactInstanceOfType(value, typeof(AbstractBase))|]", "Assert.Fail()");

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
        };
        test.FixedState.ExpectedDiagnostics.Add(VerifyCS.DiagnosticIgnoringAdditionalLocations().WithSpan(8, 13, 8, 62).WithArguments("IsExactInstanceOfType"));
        test.FixedState.ExpectedDiagnostics.Add(VerifyCS.DiagnosticIgnoringAdditionalLocations().WithSpan(10, 9, 10, 71).WithArguments("IsExactInstanceOfType"));

        await test.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    [DataRow("Assert.IsExactInstanceOfType<Derived>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(Derived))")]
    [DataRow("Assert.IsExactInstanceOfType<AbstractBase[]>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(AbstractBase[]))")]
    [DataRow("Assert.IsExactInstanceOfType<T>(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, typeof(T))")]
    [DataRow("Assert.IsExactInstanceOfType(value, expectedType)")]
    [DataRow("Assert.IsInstanceOfType<AbstractBase>(value)")]
    [DataRow("Assert.IsInstanceOfType(value, typeof(AbstractBase))")]
    [DataRow("Assert.IsNotExactInstanceOfType<AbstractBase>(value)")]
    [DataRow("Assert.IsNotExactInstanceOfType(value, typeof(AbstractBase))")]
    public async Task WhenAssertIsExactInstanceOfTypeIsNotKnownToFail_NoDiagnostic(string assertion)
    {
        string code = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                private void TestMethod<T>(AbstractBase value, Type expectedType)
                    where T : AbstractBase
                {
                    {{assertion}};
                }

                private abstract class AbstractBase { }
                private sealed class Derived : AbstractBase { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsExactInstanceOfTypeIsNotMSTestAssert_NoDiagnostic()
    {
        string code = """
            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    Assert.IsExactInstanceOfType<AbstractBase>(value);
                    Assert.IsExactInstanceOfType(value, typeof(AbstractBase));
                }

                private abstract class AbstractBase { }
            }

            public static class Assert
            {
                public static T IsExactInstanceOfType<T>(object value) => (T)value;
                public static void IsExactInstanceOfType(object value, System.Type expectedType) { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsExactInstanceOfTypeExpectedTypeUsesUserDefinedConversion_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                public void TestMethod(object value)
                {
                    Assert.IsExactInstanceOfType(value, (Type)(TypeWrapper)typeof(AbstractBase));
                }

                private abstract class AbstractBase { }
                private sealed class Derived : AbstractBase { }

                private readonly struct TypeWrapper
                {
                    public static implicit operator TypeWrapper(Type type) => default;
                    public static implicit operator Type(TypeWrapper value) => typeof(Derived);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    [DataRow("Assert.IsExactInstanceOfType(Of AbstractBase)(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, GetType(AbstractBase))")]
    [DataRow("Assert.IsExactInstanceOfType(expectedType:=GetType(AbstractBase), value:=value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, (GetType(AbstractBase)))")]
    [DataRow("Assert.IsExactInstanceOfType(value, DirectCast(DirectCast(GetType(AbstractBase), Object), System.Type))")]
    public async Task WhenAssertIsExactInstanceOfTypeExpectsAbstractTypeInVisualBasic_Diagnostic(string assertion)
    {
        string code = $$"""
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Class MyTestClass
                Public Sub TestMethod(value As Object)
                    [|{{assertion}}|]
                End Sub

                Private MustInherit Class AbstractBase
                End Class
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("Assert.IsExactInstanceOfType(Of Derived)(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, GetType(Derived))")]
    [DataRow("Assert.IsExactInstanceOfType(Of T)(value)")]
    [DataRow("Assert.IsExactInstanceOfType(value, GetType(T))")]
    [DataRow("Assert.IsExactInstanceOfType(value, expectedType)")]
    [DataRow("Assert.IsInstanceOfType(Of AbstractBase)(value)")]
    [DataRow("Assert.IsInstanceOfType(value, GetType(AbstractBase))")]
    [DataRow("Assert.IsNotExactInstanceOfType(Of AbstractBase)(value)")]
    [DataRow("Assert.IsNotExactInstanceOfType(value, GetType(AbstractBase))")]
    public async Task WhenAssertIsExactInstanceOfTypeIsNotKnownToFailInVisualBasic_NoDiagnostic(string assertion)
    {
        string code = $$"""
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Class MyTestClass
                Private Sub TestMethod(Of T As AbstractBase)(value As AbstractBase, expectedType As Type)
                    {{assertion}}
                End Sub

                Private MustInherit Class AbstractBase
                End Class

                Private NotInheritable Class Derived
                    Inherits AbstractBase
                End Class
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
