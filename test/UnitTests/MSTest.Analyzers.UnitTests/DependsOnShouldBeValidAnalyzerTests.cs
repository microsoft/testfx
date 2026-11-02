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
    public async Task WhenReferencedTypeIsNotANamedType_NotATestClass()
    {
        // The attribute constructor takes a 'Type', so 'typeof(int[])' compiles - but an array can never
        // carry a '[TestClass]', so the reference is decidably dead.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(int[]))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(0)
                .WithArguments("int[]"));
    }

    [TestMethod]
    public async Task WhenReferencedTestClassIsInternalWithoutDiscoverInternals_NotATestClass()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            internal class PrerequisiteTests
            {
                [TestMethod]
                public void Initialize() { }
            }

            [TestClass]
            public class CheckoutTests
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(PrerequisiteTests))|}]
                public void SubmitOrder() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(0)
                .WithArguments("PrerequisiteTests"));
    }

    [TestMethod]
    public async Task WhenReferencedTestClassIsInternalWithDiscoverInternals_Cycle()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class PrerequisiteTests
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(CheckoutTests), nameof(CheckoutTests.SubmitOrder))|}]
                public void Initialize() { }
            }

            [TestClass]
            public class CheckoutTests
            {
                [TestMethod]
                [{|#1:DependsOn(typeof(PrerequisiteTests), nameof(PrerequisiteTests.Initialize))|}]
                public void SubmitOrder() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("PrerequisiteTests.Initialize > CheckoutTests.SubmitOrder > PrerequisiteTests.Initialize"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("CheckoutTests.SubmitOrder > PrerequisiteTests.Initialize > CheckoutTests.SubmitOrder"));
    }

    [TestMethod]
    public async Task WhenOverrideDropsTheBaseDependency_NoDiagnostic()
    {
        // '[DependsOn]' is not inherited across an override chain, so at run time 'Run' carries no
        // dependency and 'Run > Other > Run' is not a cycle. Reading the overridden base declaration's
        // attribute here would invent one and report a cycle against correct code.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [DependsOn("Other")]
                public virtual void Run() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public override void Run() { }

                [TestMethod]
                [DependsOn(nameof(Run))]
                public void Other() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenOverrideDropsTestMethodAttribute_NotATestMethod()
    {
        // The override is the declaration reflection surfaces, and '[TestMethod]' is not inherited across
        // overrides, so the base declaration no longer makes this name a test.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                public virtual void Run() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                public override void Run() { }

                [TestMethod]
                [{|#0:DependsOn(nameof(Run))|}]
                public void PlaceOrder() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestClass", "Run"));
    }

    [TestMethod]
    public async Task WhenTwoClassesWithOnlyInheritedTestsDependOnEachOther_Cycle()
    {
        // Both classes run the inherited test as their own, so this is a real run-time cycle even though
        // neither class declares a test method of its own.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                public void Run() { }
            }

            [TestClass]
            [{|#0:DependsOn(typeof(SecondTests))|}]
            public class FirstTests : BaseTests
            {
            }

            [TestClass]
            [{|#1:DependsOn(typeof(FirstTests))|}]
            public class SecondTests : BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("FirstTests.Run > SecondTests.Run > FirstTests.Run"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("SecondTests.Run > FirstTests.Run > SecondTests.Run"));
    }

    [TestMethod]
    public async Task WhenDeclaredOnMethodOfAbstractTestClass_NoDiagnostic()
    {
        // Discovery skips an abstract '[TestClass]' and enumerates its tests under each concrete derived
        // class, so an implicit target is resolved against the derived class exactly as for an unannotated
        // base. Validating it against the abstract class would report a method that does exist at run time.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
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
    public async Task WhenReferencedTypeIsAbstractTestClass_AbstractTarget()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class BaseTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(BaseTests))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.AbstractTargetRule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }

    [TestMethod]
    public async Task WhenAppliedToAbstractTestClass_NoEffect()
    {
        // The abstract class contributes no test of its own and '[DependsOn]' is not inherited, so the
        // class-level declaration produces no edge anywhere.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class SetupTests
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            [{|#0:DependsOn(typeof(SetupTests))|}]
            public abstract class BaseTests
            {
                [TestMethod]
                public void AddItem() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NoEffectOnClassRule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }

    [TestMethod]
    public async Task WhenWalkEntersAClassWithDuplicateSignatures_NoCycle()
    {
        // 'DuplicatedTests' has a duplicated 'Run(int)', so discovery's fallback keeps only the 'Run' closest
        // to the class and the 'Run(string)' dependency is gone. The walk starts in another class and enters
        // this node, so the bail-out has to apply per node rather than only where a walk begins.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                public void Run(int value) { }

                [TestMethod]
                [DependsOn(typeof(OtherTests), nameof(OtherTests.Start))]
                public void Run(string value) { }
            }

            [TestClass]
            public class DuplicatedTests : BaseTests
            {
                [TestMethod]
                public new void Run(int value) { }
            }

            [TestClass]
            public class OtherTests
            {
                [TestMethod]
                [DependsOn(typeof(DuplicatedTests), "Run")]
                public void Start() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsNotRunnable_NoCycle()
    {
        // Discovery rejects a private '[TestMethod]', so it never becomes a node and the "cycle" between the
        // two does not exist at run time. MSTEST0003 already reports the invalid method itself.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn(nameof(AddItem))]
                private void CreateCart() { }

                [TestMethod]
                [DependsOn(nameof(CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInternalTestMethodAndDiscoverInternals_Cycle()
    {
        // With '[assembly: DiscoverInternals]' an internal test method *is* discovered, so the cycle is real.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(nameof(AddItem))|}]
                internal void CreateCart() { }

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
    public async Task WhenTestClassIsGeneric_NoCycle()
    {
        // 'TypeValidator.IsValidTestClass' rejects a non-abstract generic type definition outright, so no
        // test runs under this class and there is no cycle to report.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [TestMethod]
                [DependsOn(nameof(AddItem))]
                public void CreateCart() { }

                [TestMethod]
                [DependsOn(nameof(CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNewHidesTheBaseDependency_NoDiagnostic()
    {
        // Discovery dedupes same-signature declarations and keeps the one closest to the test class, so the
        // hidden base declaration's dependency is gone at run time and 'Run > Other > Run' is not a cycle.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [DependsOn("Other")]
                public void Run() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public new void Run() { }

                [TestMethod]
                [DependsOn(nameof(Run))]
                public void Other() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTypeArgumentIsNull_NoDiagnostic()
    {
        // The constructor throws for a null type, so no dependency is ever recorded; reading past it would
        // misread this as an implicit same-class self reference.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn((System.Type)null!, nameof(AddItem))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNewHidesTheBaseDependencyWithRenamedTypeParameter_NoDiagnostic()
    {
        // 'Run<U>(U)' hides 'Run<T>(T)': the type parameter's name is not part of the signature, so discovery
        // dedupes them and keeps the derived declaration. Comparing the parameter types as source text would
        // see 'U' and 'T', keep the hidden base declaration, and invent its dependency back.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [DependsOn("Other")]
                public void Run<T>(T value) { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public new void Run<U>(U value) { }

                [TestMethod]
                [DependsOn(nameof(Run))]
                public void Other() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenBaseDeclaresGenuineOverload_NoDiagnostic()
    {
        // A different signature is a distinct test rather than a hidden one, so the base declaration stays in
        // the effective set and its dependency is real. 'CreateCart' exists, so nothing is reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [DependsOn("CreateCart")]
                public void Run(int value) { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                [TestMethod]
                public void Run() { }

                [TestMethod]
                public void CreateCart() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedTestIsShadowedByNonTestOverload_Cycle()
    {
        // 'MyTestClass' declares an 'A' overload that is not a test, so nothing starts a walk for the
        // inherited 'A' unless cycle analysis is driven from the test class rather than from the declaring
        // method. The inherited test still runs under 'MyTestClass', so the cycle is real.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [{|#0:DependsOn("Other")|}]
                public void A() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                public void A(int value) { }

                [TestMethod]
                [{|#1:DependsOn(nameof(A))|}]
                public void Other() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("MyTestClass.A > MyTestClass.Other > MyTestClass.A"),
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(1)
                .WithArguments("MyTestClass.Other > MyTestClass.A > MyTestClass.Other"));
    }

    [TestMethod]
    public async Task WhenTestClassIsInaccessible_NoCycle()
    {
        // A private nested '[TestClass]' is skipped by discovery, so no test runs under it and there is no
        // cycle. MSTEST0002 already reports the class itself.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Outer
            {
                [TestClass]
                private class MyTestClass
                {
                    [TestMethod]
                    [DependsOn(nameof(AddItem))]
                    public void CreateCart() { }

                    [TestMethod]
                    [DependsOn(nameof(CreateCart))]
                    public void AddItem() { }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReferencedTypeIsConstructedGeneric_NotATestClass()
    {
        // Discovery enumerates the assembly's type definitions and rejects the non-abstract generic one, so
        // no test exists under either the open or the constructed name.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class GenericTests<T>
            {
                [TestMethod]
                public void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(GenericTests<int>))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(0)
                .WithArguments("GenericTests"));
    }

    [TestMethod]
    public async Task WhenReferencedTypeIsStaticTestClass_NotATestClass()
    {
        // Roslyn reports a static class as abstract, but nothing derives from it, so the abstract-base
        // message would be misleading here.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class StaticTests
            {
                [TestMethod]
                public static void CreateCart() { }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(StaticTests))|}]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NotATestClassRule)
                .WithLocation(0)
                .WithArguments("StaticTests"));
    }

    [TestMethod]
    public async Task WhenInheritedTestReferencesItsOwnDerivedClass_Cycle()
    {
        // The test runs as 'MyTestClass.Run' and depends on 'MyTestClass.Run', so the run-time graph has a
        // self-cycle. AnalyzeTarget cannot see it - it compares the target against the *declaring* class -
        // so the cycle rule has to report it rather than assume a self reference was already flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [{|#0:DependsOn(typeof(MyTestClass), nameof(Run))|}]
                public void Run() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
                .WithLocation(0)
                .WithArguments("MyTestClass.Run > MyTestClass.Run"));
    }

    [TestMethod]
    public async Task WhenOnlyPrivateHelpersShareASignature_Cycle()
    {
        // A private helper hidden in the derived class is not a test, so discovery's duplicate fallback never
        // triggers and the real cycle must still be reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                private void Log() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                private void Log() { }

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
    public async Task WhenNonTestMemberHidesABaseTest_NoDiagnostic()
    {
        // The hiding declaration is not a test, so discovery still finds the base test and its dependency is
        // real. 'CreateCart' exists, so nothing is reported.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseTests
            {
                [TestMethod]
                [DependsOn("CreateCart")]
                public void Run() { }
            }

            [TestClass]
            public class MyTestClass : BaseTests
            {
                private new void Run() { }

                [TestMethod]
                public void CreateCart() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyIsInvalid_NoCycle()
    {
        // Discovery rejects the whole class when a declared 'TestContext' property has a static getter, so no
        // test runs under it and there is no cycle. MSTEST0005 already reports the property itself.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public static TestContext TestContext { get; set; }

                [TestMethod]
                [DependsOn(nameof(AddItem))]
                public void CreateCart() { }

                [TestMethod]
                [DependsOn(nameof(CreateCart))]
                public void AddItem() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertyIsValid_Cycle()
    {
        // The ordinary instance 'TestContext' property must not disturb anything.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

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
            VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.NoEffectOnClassRule)
                .WithLocation(0)
                .WithArguments("BaseTests"));
    }
}
