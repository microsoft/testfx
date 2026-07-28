// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DependsOnShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DependsOnShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenReferencingTestMethodOfSameClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void CreateCart() { }

                [TestMethod]
                [DependsOn(nameof(CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReferencingTestMethodOfAnotherClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn(typeof(SetupTests), nameof(SetupTests.CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReferencingWholeTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn(typeof(SetupTests))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReferencingInheritedTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class BaseTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                [DependsOn(nameof(CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassLevelAttributeNamesMethodOfSameClass_NoDiagnostic()
    {
        // The class-level declaration means "every *other* test waits for CreateCart", so the self-edge it
        // would generate for CreateCart is dropped by discovery and must not be reported as a cycle.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(MyTestClass.CreateCart))]
            public class MyTestClass
            {
                [TestMethod]
                public void CreateCart() { }

                [TestMethod]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodNameIsEmpty_NoDiagnostic()
    {
        // The attribute constructor already throws for these, so the analyzer stays quiet.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn("")]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDeclaredOnMethodOfNonTestBaseClass_NoDiagnostic()
    {
        // The implicit target is resolved against each derived test class at run time, so a name that is not
        // a member of the base class is not necessarily broken.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class BaseTests
            {
                [TestMethod]
                [DependsOn("DeclaredOnlyOnDerived")]
                public void AddItem() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public void DeclaredOnlyOnDerived() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReferencedMethodDoesNotExist_MethodNotFound()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn("CreateCat")|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.MethodNotFoundRule)
                .WithLocation(0)
                .WithArguments("MyTestClass", "CreateCat"));
    }

    [TestMethod]
    public async Task WhenReferencedMethodDoesNotExistOnOtherClass_MethodNotFound()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(SetupTests), "CreateCat")|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.MethodNotFoundRule)
                .WithLocation(0)
                .WithArguments("SetupTests", "CreateCat"));
    }

    [TestMethod]
    public async Task WhenReferencedMethodIsNotATestMethod_NotATestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void CreateCart() { }

                [TestMethod]
                [{|#0:DependsOn(nameof(CreateCart))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass", "CreateCart"));
    }

    [TestMethod]
    public async Task WhenReferencedMethodIsAFixtureMethod_NotATestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void CreateCart() { }

                [TestMethod]
                [{|#0:DependsOn(nameof(CreateCart))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass", "CreateCart"));
    }

    [TestMethod]
    public async Task WhenReferencedTypeIsNotATestClass_NotATestClass()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Helpers
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(Helpers))|}]
                public void AddItem() { }

                [TestMethod]
                [{|#1:DependsOn(typeof(Helpers), nameof(Helpers.CreateCart))|}]
                public void ApplyCoupon() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(0)
                .WithArguments("Helpers"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(1)
                .WithArguments("Helpers"));
    }

    [TestMethod]
    public async Task WhenTestReferencesItself_SelfReference()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(nameof(AddItem))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.SelfReferenceRule)
                .WithLocation(0)
                .WithArguments("AddItem"));
    }

    [TestMethod]
    public async Task WhenTestReferencesItselfThroughItsOwnType_SelfReference()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(MyTestClass), nameof(AddItem))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.SelfReferenceRule)
                .WithLocation(0)
                .WithArguments("AddItem"));
    }

    [TestMethod]
    public async Task WhenTwoTestsDependOnEachOther_Cycle()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(nameof(AddItem))|}]
                public void CreateCart() { }

                [TestMethod]
                [{|#1:DependsOn(nameof(CreateCart))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("MyTestClass.CreateCart > MyTestClass.AddItem > MyTestClass.CreateCart"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("MyTestClass.AddItem > MyTestClass.CreateCart > MyTestClass.AddItem"));
    }

    [TestMethod]
    public async Task WhenThreeTestsFormACycle_Cycle()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(nameof(C))|}]
                public void A() { }

                [TestMethod]
                [{|#1:DependsOn(nameof(A))|}]
                public void B() { }

                [TestMethod]
                [{|#2:DependsOn(nameof(B))|}]
                public void C() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("MyTestClass.A > MyTestClass.C > MyTestClass.B > MyTestClass.A"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("MyTestClass.B > MyTestClass.A > MyTestClass.C > MyTestClass.B"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(2)
                .WithArguments("MyTestClass.C > MyTestClass.B > MyTestClass.A > MyTestClass.C"));
    }

    [TestMethod]
    public async Task WhenTwoTestClassesDependOnEachOther_Cycle()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [{|#0:DependsOn(typeof(SecondTests))|}]
            public class FirstTests
            {
                [TestMethod]
                public void A() { }
            }

            [TestClass]
            [{|#1:DependsOn(typeof(FirstTests))|}]
            public class SecondTests
            {
                [TestMethod]
                public void B() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("FirstTests.A > SecondTests.B > FirstTests.A"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("SecondTests.B > FirstTests.A > SecondTests.B"));
    }

    [TestMethod]
    public async Task WhenReferencedTestClassIsInAnotherAssembly_OtherAssembly()
    {
        // Discovery resolves dependencies against the tests of one test source and stores a 'typeof' target
        // as its CLR full name, so a reference to a type from a referenced assembly matches nothing at run
        // time even though the type and the method both exist.
        string libraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(SetupTests))|}]
                public void AddItem() { }

                [TestMethod]
                [{|#1:DependsOn(typeof(SetupTests), nameof(SetupTests.CreateCart))|}]
                public void ApplyCoupon() { }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = consumerCode,
        };

        AddTestLibraryProject(test, libraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.OtherAssemblyRule)
                .WithLocation(0)
                .WithArguments("SetupTests"));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.OtherAssemblyRule)
                .WithLocation(1)
                .WithArguments("SetupTests"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenClassLevelReferenceIsInAnotherAssembly_OtherAssemblyAndNoCycle()
    {
        // The same rule applies to the dependency walk: an edge that crosses the assembly boundary does not
        // exist at run time, so it must not be traversed and must not contribute to a cycle.
        string libraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(SetupTests.CreateCart))]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }

                [TestMethod]
                public void AddItem() { }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [{|#0:DependsOn(typeof(SetupTests))|}]
            public class MyTestClass
            {
                [TestMethod]
                public void PlaceOrder() { }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = consumerCode,
        };

        AddTestLibraryProject(test, libraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.OtherAssemblyRule)
                .WithLocation(0)
                .WithArguments("SetupTests"));

        await test.RunAsync();
    }

    private static void AddTestLibraryProject(VerifyCS.Test test, string libraryCode)
    {
        var libraryProject = new ProjectState("TestLib", LanguageNames.CSharp, "/TestLib/", "cs");
        libraryProject.Sources.Add(("Library.cs", libraryCode));
        libraryProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location));
        libraryProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(TestContext).Assembly.Location));
        test.TestState.AdditionalProjects.Add("TestLib", libraryProject);
        test.TestState.AdditionalProjectReferences.Add("TestLib");
    }

    [TestMethod]
    public async Task WhenAppliedToNonTestMethod_NoEffect()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void CreateCart() { }

                [{|#0:DependsOn(nameof(CreateCart))|}]
                public void Helper() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NoEffectRule)
                .WithLocation(0)
                .WithArguments("Helper"));
    }

    [TestMethod]
    public async Task WhenAppliedToNonTestClass_NoEffect()
    {
        // '[DependsOn]' is not inherited, so an application on a shared base class never produces an edge.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [{|#0:DependsOn(typeof(SetupTests))|}]
            public abstract class BaseTests
            {
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NoEffectRule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }
}
