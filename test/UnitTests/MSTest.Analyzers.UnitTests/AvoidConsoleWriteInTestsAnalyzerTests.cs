// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidConsoleWriteInTestsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidConsoleWriteInTestsAnalyzerTests
{
    [TestMethod]
    public async Task WhenConsoleWriteUsedInTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Console.Write("test")|];
                    [|Console.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTraceWriteUsedInTestMethod_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Trace.Write("test")|];
                    [|Trace.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDebugWriteUsedInTestMethod_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Debug.Write("test")|];
                    [|Debug.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInHelperMethodInTestClass_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void MyHelperMethod()
                {
                    Console.WriteLine("test");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                    MyHelperMethod();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
    [TestMethod]
    public async Task WhenConsoleWriteUsedInNonTestClass_NoDiagnostic()
    {
        string code = """
            using System;

            public class MyClass
            {
                public void MyMethod()
                {
                    Console.Write("test");
                    Console.WriteLine("test");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenOtherConsoleMethodsUsed_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string input = Console.ReadLine();
                    Console.Beep();
                    Console.Clear();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextWriteLineUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    TestContext.WriteLine("test");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCustomWriteMethodUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var myObject = new MyClass();
                    myObject.Write("test");
                }
            }

            public class MyClass
            {
                public void Write(string message) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}