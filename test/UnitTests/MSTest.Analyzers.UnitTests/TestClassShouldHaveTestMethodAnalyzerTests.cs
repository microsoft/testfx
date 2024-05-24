// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassShouldHaveTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestClassShouldHaveTestMethodAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestClassHasTestMethod_NoDiagnostic()
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

    public async Task WhenStaticTestClassWithAssemblyCleanup_DoesNotHaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenStaticTestClassWithAssemblyInitialization_DoesNotHaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassDoesNotHaveTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenStaticTestClassWithoutAssemblyAttributes_DoesNotHaveTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class {|#0:MyTestClass|}
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromAbstractClassHasTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class BaseClass
            {
                [TestMethod]
                public void TestMethod1UsingName() { }
            }

            [TestClass]
            public class Derived : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromClassHasTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseClass
            {
                [TestMethod]
                public void TestMethod1UsingName() { }
            }

            [TestClass]
            public class Derived : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromTestClassHasTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class BaseClass
            {
                [TestMethod]
                public void TestMethod1UsingName() { }
            }

            [TestClass]
            public class Derived : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromAbstractTestClassHasTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class BaseClass
            {
                [TestMethod]
                public void TestMethod1UsingName() { }
            }

            [TestClass]
            public class Derived : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromBaseBaseClassHasTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseBase
            {
                [TestMethod]
                public void TestMethod1UsingName() { }
            }

            public class BaseClass : BaseBase
            {
            }
            
            [TestClass]
            public class Derived : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromClassDoesNotHaveTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseClass
            {
            }
            
            [TestClass]
            public class {|#0:Derived|} : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("Derived"));
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromClassHasAssemblyInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseClass
            {
               [AssemblyInitialize]
               public static void AssInit(TestContext testContext)
               {
               }
            }
            
            [TestClass]
            public class {|#0:Derived|} : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("Derived"));
    }

    public async Task WhenTestClassWithoutAssemblyAttributesAndTestMethod_InheritsFromBaseBaseClassHasAssemblyCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseBase
            {
               [AssemblyCleanup]
               public void AssInit()
               {
               }
            }

            public class BaseClass : BaseBase
            {
            }

            [TestClass]
            public class {|#0:Derived|} : BaseClass
            {
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("Derived"));
    }
}
