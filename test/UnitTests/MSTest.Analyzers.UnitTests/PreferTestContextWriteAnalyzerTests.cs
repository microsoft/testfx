// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferTestContextWriteAnalyzer,
    MSTest.Analyzers.CodeFixes.PreferTestContextWriteCodeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferTestContextWriteAnalyzerTests
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

    [TestMethod]
    public async Task WhenConsoleWriteUsedInTestInitializeMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                    [|Console.Write("test")|];
                    [|Console.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInTestCleanupMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                    [|Console.Write("test")|];
                    [|Console.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInClassInitializeMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInitialize(TestContext context)
                {
                    [|Console.Write("test")|];
                    [|Console.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInClassCleanupMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                    [|Console.Write("test")|];
                    [|Console.WriteLine("test")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInAssemblyInitializeMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AssemblyInitialize]
            public static void AssemblyInitialize(TestContext context)
            {
                [|Console.Write("test")|];
                [|Console.WriteLine("test")|];
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInAssemblyCleanupMethod_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AssemblyCleanup]
            public static void AssemblyCleanup()
            {
                [|Console.Write("test")|];
                [|Console.WriteLine("test")|];
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInMethodCalledByTestMethod_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void HelperMethod()
                {
                    Console.WriteLine("test");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                    HelperMethod();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenConsoleWriteUsedInTestMethodWithCodeFix()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Console.Write("test")|};
                    {|#1:Console.WriteLine("test")|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    TestContext.WriteLine("test");
                    TestContext.WriteLine("test");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTraceWriteUsedInTestMethodWithCodeFix()
    {
        string code = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Trace.Write("test")|};
                    {|#1:Trace.WriteLine("test")|};
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    TestContext.WriteLine("test");
                    TestContext.WriteLine("test");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenDebugWriteUsedInTestMethodWithCodeFix()
    {
        string code = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Debug.Write("test")|};
                    {|#1:Debug.WriteLine("test")|};
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    TestContext.WriteLine("test");
                    TestContext.WriteLine("test");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
