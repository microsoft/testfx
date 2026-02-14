// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DuplicateTestMethodAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DuplicateTestMethodAttributeAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasSingleAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasSingleDerivedAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethod : TestMethodAttribute
            {
                public MyTestMethod([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) 
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasDataTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                public void TestMethod1(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasBothTestMethodAndDerivedAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethod : TestMethodAttribute
            {
                public MyTestMethod([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) 
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [MyTestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasTestMethodAndDataTestMethodAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataTestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasMultipleDerivedAttributes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethod1 : TestMethodAttribute
            {
                public MyTestMethod1([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) 
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            public class MyTestMethod2 : TestMethodAttribute
            {
                public MyTestMethod2([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) 
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod1]
                [MyTestMethod2]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasThreeTestMethodAttributes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethod : TestMethodAttribute
            {
                public MyTestMethod([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) 
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataTestMethod]
                [MyTestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasDuplicateAttributes_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void RegularMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
