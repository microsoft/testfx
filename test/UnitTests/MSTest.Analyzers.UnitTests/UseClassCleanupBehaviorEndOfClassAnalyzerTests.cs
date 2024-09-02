// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseClassCleanupBehaviorEndOfClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class UseClassCleanupBehaviorEndOfClassAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task UsingClassCleanup_WithoutCleanupBehaviorEndOfClass_AndNotInsideTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithoutCleanupBehaviorEndOfClass_AndInsideTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithCleanupBehaviorEndOfClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithCleanupBehaviorEndOfAssembly_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithoutCleanupBehavior_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithoutCleanupBehaviorAndWithInheritanceBehavior_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithCleanupBehaviorEndOFAssemblyAndWithInheritanceBehavior_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfAssembly)]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_WithCleanupBehaviorEndOFClassAndWithInheritanceBehavior_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_InsideTestClass_WithClassCleanupExecutionWithEndOfClassBehavior_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: ClassCleanupExecutionAttribute(ClassCleanupBehavior.EndOfClass)]
            
            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_InsideTestClass_WithClassCleanupExecutionWithEndOfAsseblyBehavior_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: ClassCleanupExecutionAttribute(ClassCleanupBehavior.EndOfAssembly)]
            
            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_InsideTestClass_WithClassCleanupExecutionWithEndOfClassBehavior_WithCleanupBehaviorEndOfAssembly_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: ClassCleanupExecutionAttribute(ClassCleanupBehavior.EndOfClass)]
            
            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)]
                public static void [|ClassCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task UsingClassCleanup_InsideTestClass_WithClassCleanupExecutionWithEndOfAssemblyBehavior_WithCleanupBehaviorEndOfClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: ClassCleanupExecutionAttribute(ClassCleanupBehavior.EndOfAssembly)]
            
            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
