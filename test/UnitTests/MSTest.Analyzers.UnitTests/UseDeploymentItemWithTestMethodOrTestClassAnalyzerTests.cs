// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseDeploymentItemWithTestMethodOrTestClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseDeploymentItemWithTestMethodOrTestClassAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasDeploymentItem_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DeploymentItem("")]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasDeploymentItem_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DeploymentItem("")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedTestClassAttributeHasDeploymentItem_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class InheritedTestClass : TestClassAttribute
            {}

            [InheritedTestClass]
            [DeploymentItem("")]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodAttributeHasDeploymentItem_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class InheritedTestMethod : TestMethodAttribute
            {}
            
            [TestClass]
            public class MyTestClass
            {
                [InheritedTestMethod]
                [DeploymentItem("")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAnAbstractClassHasDeploymentItem_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [DeploymentItem("")]
            public abstract class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAClassHasDeploymentItem_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [DeploymentItem("")]
            public class [|MyTestClass|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAMethodHasDeploymentItem_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DeploymentItem("")]
                public void [|MyTestMethod|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAbstractMethodHasDeploymentItemWithoutTestMethod_Diagnostic()
    {
        // Abstract classes are skipped (DeploymentItem is inherited), but abstract methods are not.
        // An abstract method with [DeploymentItem] but no [TestMethod] is flagged as misuse.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class MyBaseClass
            {
                [DeploymentItem("")]
                public abstract void [|SomeMethod|]();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStaticClassHasDeploymentItem_Diagnostic()
    {
        // Unlike abstract classes (which are skipped because [DeploymentItem] is inherited by subclasses),
        // static classes are NOT skipped. A static class with [DeploymentItem] but no [TestClass] is flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [DeploymentItem("")]
            public static class [|MyStaticHelper|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodHasDeploymentItem_NoDiagnostic()
    {
        // [DataTestMethod] inherits from [TestMethod], so the Inherits() check passes
        // and [DeploymentItem] on a [DataTestMethod] method should not be flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DeploymentItem("")]
                public void MyDataTest()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasMultipleDeploymentItems_OneDiagnostic()
    {
        // Multiple [DeploymentItem] attributes on a non-test class produce exactly one
        // diagnostic for the class (the analyzer uses a boolean flag, not a per-attribute count).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [DeploymentItem("file1.txt")]
            [DeploymentItem("file2.txt")]
            public class [|MyClass|]
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
