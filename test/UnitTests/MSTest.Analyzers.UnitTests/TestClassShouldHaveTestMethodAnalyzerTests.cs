// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassShouldHaveTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestClassShouldHaveTestMethodAnalyzerTests
{
    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public async Task WhenStaticTestClassWithGlobalTestInitialize_DoesNotHaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [GlobalTestInitialize]
                public static void GlobalTestInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStaticTestClassWithGlobalTestCleanup_DoesNotHaveTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
                [GlobalTestCleanup]
                public static void GlobalTestCleanup(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonStaticTestClassWithGlobalTestInitialize_DoesNotHaveTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                [GlobalTestInitialize]
                public static void GlobalTestInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    [TestMethod]
    public async Task WhenNonStaticTestClassWithGlobalTestCleanup_DoesNotHaveTestMethod_Diagnostic()
    {
        // GlobalTestCleanup only exempts static test classes from needing a test method,
        // not non-static ones. This mirrors the existing GlobalTestInitialize counterpart.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                [GlobalTestCleanup]
                public static void GlobalTestCleanup(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestClassShouldHaveTestMethodAnalyzer.TestClassShouldHaveTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));
    }

    [TestMethod]
    public async Task WhenTestClassHasDataTestMethod_NoDiagnostic()
    {
        // DataTestMethodAttribute inherits from TestMethodAttribute, so the Inherits() check
        // should recognise it as a test method and suppress the diagnostic.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                public void MyTestMethod(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDerivedTestMethodAttribute_NoDiagnostic()
    {
        // A custom attribute that derives from TestMethodAttribute should satisfy the
        // Inherits() check and prevent the "no test method" diagnostic.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestMethodAttribute : TestMethodAttribute { }

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
}
