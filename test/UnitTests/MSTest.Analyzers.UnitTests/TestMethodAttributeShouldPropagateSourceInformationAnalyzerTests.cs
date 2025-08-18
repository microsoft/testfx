// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodAttributeShouldPropagateSourceInformationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestMethodAttributeShouldPropagateSourceInformationAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodAttribute_NoDiagnostic()
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
    public async Task WhenDataTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenSTATestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [STATestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithCallerInfoParameters_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public MyTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithoutCallerInfoParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public [|MyTestMethodAttribute|]()
                    : base()
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithOnlyCallerFilePath_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public [|MyTestMethodAttribute|]([CallerFilePath] string callerFilePath = "")
                    : base(callerFilePath)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithOnlyCallerLineNumber_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public [|MyTestMethodAttribute|]([CallerLineNumber] int callerLineNumber = -1)
                    : base(callerLineNumber: callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithMultipleConstructors_SomeValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public [|MyTestMethodAttribute|](string displayName)
                    : base()
                {
                }

                public MyTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeWithMultipleConstructors_NoneValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                public [|MyTestMethodAttribute|](string displayName, int i)
                    : base()
                {
                }

                public [|MyTestMethodAttribute|]([CallerFilePath] string callerFilePath = "")
                    : base(callerFilePath)
                {
                }
            }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAbstractDerivedTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class [|MyTestMethodAttribute|] : TestMethodAttribute
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonPublicConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestMethodAttribute : TestMethodAttribute
            {
                private [|MyTestMethodAttribute|]()
                    : base()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodAttributeDerivedClass_NoDiagnostic()
    {
        string code = """
            using System;

            public class MyAttribute : Attribute
            {
                public MyAttribute()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassDoesNotInheritFromTestMethodAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyCustomClass
            {
                public MyCustomClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDeeplyDerivedTestMethodAttribute_WithCallerInfo_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Runtime.CompilerServices;

            public class BaseTestMethodAttribute : TestMethodAttribute
            {
                public BaseTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }

            public class DerivedTestMethodAttribute : BaseTestMethodAttribute
            {
                public DerivedTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
