// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.InheritedMemberFromDifferentMSTestVersionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.InheritedMemberFromDifferentMSTestVersionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class InheritedMemberFromDifferentMSTestVersionAnalyzerTests
{
    // Reproduces https://github.com/microsoft/testfx/issues/10505: the v4 framework assembly was renamed from
    // "Microsoft.VisualStudio.TestPlatform.TestFramework" to "MSTest.TestFramework", so a base class compiled against
    // v3 exposes its lifecycle/test attributes under a different type identity. This constant names the simulated v3
    // framework assembly; a base library that defines its own MSTest attributes there behaves like a v3-compiled base.
    private const string LegacyFrameworkAssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework";
    private const string LegacyFrameworkExtensionsAssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions";
    private const string CurrentFrameworkAssemblyName = "MSTest.TestFramework";

    [TestMethod]
    public async Task WhenInheritedTestInitializeComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        // An inherited [TestMethod] from a v3-compiled base is not even discovered under v4.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void InheritedTest() { }
                }
            }
            """;

        // The derived class intentionally declares no [TestMethod] of its own; the only test method is the inherited,
        // mismatched one.
        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("InheritedTest", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedCustomTestMethodSubclassComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        // A custom [TestMethod] subclass compiled against the old framework has the same silent discovery failure,
        // because the v4 adapter matches on the v4 TestMethodAttribute identity, which the v3-based subclass is not.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public sealed class RetryTestAttribute : TestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public void InheritedTest() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("InheritedTest", "TestBase", "RetryTest", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeFromSeparatelyCompiledBaseLibrary_Diagnostic()
    {
        // The base library references the old framework and is linked into the consumer through a Roslyn project
        // reference. Project references preserve the referenced symbols, so the legacy attribute still resolves here
        // (its assembly name is available) — this is the resolved cross-assembly case. The truly-absent PE case, where
        // the attribute cannot bind, is covered separately by the emitted-PE tests below.
        string legacyFrameworkCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }
            """;

        string baseLibraryCode = """
            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkAndBaseProjects(test, legacyFrameworkCode, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassInitializeIsInheritableAndComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }

                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute() { }
                    public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassInitialize(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseClassInitialize", "TestBase", "ClassInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassInitializeUsesLegacyExtensionsTestContext_Diagnostic()
    {
        string legacyFrameworkCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }
            """;

        string legacyExtensionsCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }
            }
            """;

        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassInitialize(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkExtensionsAndBaseProjects(test, legacyFrameworkCode, legacyExtensionsCode, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseClassInitialize", "TestBase", "ClassInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeComesFromSameFrameworkAssembly_NoDiagnostic()
    {
        // A base library in another assembly, but compiled against the same MSTest framework, is fine.
        string baseCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddSameFrameworkBaseProject(test, baseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenCustomTestClassAttributeAndSameVersionInheritedTestInitialize_NoDiagnostic()
    {
        // A custom [TestClass] subclass is a supported extensibility point. The framework assembly must be resolved
        // from the canonical TestClassAttribute in the attribute's base chain, not from the custom attribute's own
        // assembly, otherwise a same-version inherited fixture is wrongly flagged.
        string baseCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public sealed class MyTestClassAttribute : TestClassAttribute { }

                [MyTestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddSameFrameworkBaseProject(test, baseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeIsDeclaredInSameAssembly_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class TestBase
            {
                [TestInitialize]
                public void BaseInitialize() { }
            }

            [TestClass]
            public class SampleTests : TestBase
            {
                [TestMethod]
                public void MyTest() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedPrivateTestInitializeComesFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // A non-public base fixture is not run by MSTest even in the same version, so recompiling the base cannot
        // help; warning about it would be a false positive.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    private void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeIsOverriddenInDerivedType_NoDiagnostic()
    {
        // The base lifecycle method is overridden by a more-derived method, and the standard [TestInitialize] uses
        // Inherited=false, so the override (compiled against the current version) does not carry the base attribute and
        // is what runs; the base attribute is moot.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public virtual void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public override void BaseInitialize() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsInheritableCustomAttributeFromDifferentFrameworkAssembly_Diagnostic()
    {
        // A custom [RetryTest : DataTestMethod] with Inherited=true flows onto the override via reflection
        // (the adapter reads attributes with inherit:true). Because it derives from the legacy framework type, the
        // override is discoverable only after recompiling the base, so the mismatch must be reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public override void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "RetryTest", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsNonInheritableCustomAttributeFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // With Inherited=false (like the standard attributes) the override does not carry the base attribute, so it is
        // not a test either before or after recompiling the base — nothing to report.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public override void Run() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsInheritableCustomAttributeButHasOwnCurrentAttribute_NoDiagnostic()
    {
        // Even when the inherited custom attribute is inheritable, an override that carries its own current-version
        // [TestMethod] is discoverable regardless of the base version, so there is no migration break to report.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public override void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenMostDerivedOverrideInheritsInheritableAttributeThroughIntermediateWithOwnAttribute_Diagnostic()
    {
        // Multi-level override chain: only the most-derived override (which reflection surfaces) matters. It carries no
        // current attribute and still inherits the inheritable legacy [RetryTest], so it is a real break. The
        // intermediate override's own current [TestMethod] is non-inheritable and must not suppress the warning.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class Intermediate : TestBase
                {
                    [TestMethod]
                    public override void Run() { }
                }

                [TestClass]
                public class {|#0:SampleTests|} : Intermediate
                {
                    public override void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "RetryTest", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassInitializeIsNotInheritable_NoDiagnostic()
    {
        // A base [ClassInitialize] without InheritanceBehavior.BeforeEachDerivedClass does not run on derived classes
        // even in the same version, so recompiling the base cannot make it run.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }

                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute() { }
                    public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassInitialize]
                    public static void BaseClassInitialize(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedAssemblyInitializeComesFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // Assembly-level fixtures are discovered per-assembly, never inherited, so a base library's assembly fixture
        // never runs on a derived test class regardless of the framework version.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class AssemblyInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [AssemblyInitialize]
                    public static void BaseAssemblyInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenBaseComesFromDifferentFrameworkAssemblyButDerivedIsNotATestClass_NoDiagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            namespace Repro
            {
                public class NotATestClass : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeIsHiddenWithNewInDerivedType_NoDiagnostic()
    {
        // The base lifecycle method is hidden with `new` in the derived type; MSTest suppresses the inherited member
        // whenever a more-derived type declares one with the same name.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public new void BaseInitialize() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeHasInvalidSignatureFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // A [TestInitialize] with parameters is not a valid instance fixture and would not run even in the same
        // version, so recompiling the base cannot help.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize(int value) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeFromEmittedMissingFrameworkPe_Diagnostic()
    {
        // A real emitted PE graph: the base library is compiled against the legacy framework, but only the base library
        // PE is linked into the consumer (the legacy framework PE is absent). Roslyn then sees the base library's
        // [TestInitialize] as missing metadata whose constructor cannot bind — instance fixtures carry no constructor
        // arguments, so detection still works. This is the shape a project reference cannot reproduce.
        string baseLibraryCode = """
            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddEmittedMissingFrameworkBaseLibrary(test, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassInitializeFromEmittedMissingFrameworkPe_NoDiagnostic()
    {
        // Known limitation (documented on the analyzer): when the legacy framework PE is absent, the class-fixture
        // attribute constructor cannot bind, so its ConstructorArguments are empty and InheritanceBehavior cannot be
        // read. The analyzer conservatively does not flag it — treating an undecodable argument as
        // BeforeEachDerivedClass would falsely flag the default None.
        string baseLibraryCode = """
            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassInitialize(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddEmittedMissingFrameworkBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassCleanupFromEmittedMissingFrameworkPe_NoDiagnostic()
    {
        // Same limitation as ClassInitialize: with the legacy framework PE absent, the constructor argument carrying
        // InheritanceBehavior cannot be decoded, so the inherited class cleanup is conservatively not reported.
        string baseLibraryCode = """
            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassCleanup(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddEmittedMissingFrameworkBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeReturnsTaskOfT_NoDiagnostic()
    {
        // Task<T> is not a valid fixture return type, so recompiling the base would not make it run.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public System.Threading.Tasks.Task<int> BaseInitialize() => System.Threading.Tasks.Task.FromResult(0);
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeReturnsTask_Diagnostic()
    {
        // Non-generic Task is a valid fixture return type.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public System.Threading.Tasks.Task BaseInitialize() => System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedParameterlessClassInitialize_NoDiagnostic()
    {
        // ClassInitialize requires a TestContext parameter; a parameterless one is invalid and would not run even
        // after recompiling the base.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }

                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute() { }
                    public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedParameterlessClassCleanupIsInheritable_Diagnostic()
    {
        // Unlike ClassInitialize, ClassCleanup may be parameterless, so an inheritable one is a real mismatch.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassCleanupAttribute : System.Attribute
                {
                    public ClassCleanupAttribute() { }
                    public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassCleanup() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseClassCleanup", "TestBase", "ClassCleanup", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeShadowedByInvalidDerivedMethod_Diagnostic()
    {
        // A private same-name method in the derived class is not a valid fixture, so it does not suppress the
        // inherited public fixture; the mismatch must still be reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    private new void BaseInitialize() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByDifferentSignature_Diagnostic()
    {
        // A different-signature overload does not hide the inherited test method, so it stays discoverable and the
        // mismatch is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public void Run(int value) { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodHiddenBySameSignature_NoDiagnostic()
    {
        // A derived method that is itself a current-version [TestMethod] with the same signature hides the inherited
        // test, so the derived one runs and recompiling the base cannot change behavior. The base uses [DataTestMethod]
        // (also a test-method kind) so the consumer can apply the real [TestMethod] without a type-name collision.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByNonTestMethod_Diagnostic()
    {
        // A derived same-signature method without [TestMethod] is not a valid test method, so MSTest discovery skips
        // it and still discovers the inherited base test — the mismatch must be reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByDifferentReturnType_Diagnostic()
    {
        // A derived 'new int Run()' has a different CLR signature (return type) and an invalid test return type, so
        // discovery keeps it distinct and rejects it, leaving the inherited base test discoverable.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public new int Run() => 0;
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByStaticMethod_Diagnostic()
    {
        // A derived 'new static void Run()' has a different (static) calling convention and is an invalid test method,
        // so discovery rejects it and still discovers the inherited instance base test.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public static new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedClassInitializeWithSameNameDerivedMethod_Diagnostic()
    {
        // Class fixtures are accumulated independently, so a same-name derived method does not suppress an inherited
        // class fixture.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }

                public enum InheritanceBehavior { None, BeforeEachDerivedClass }

                public sealed class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute() { }
                    public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                    public static void BaseClassInitialize(TestContext context) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public static void BaseClassInitialize() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseClassInitialize", "TestBase", "ClassInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByInternalMethodWithDiscoverInternals_NoDiagnostic()
    {
        // With [assembly: DiscoverInternals] an internal derived [TestMethod] is discoverable, so it hides the inherited
        // test and recompiling the base cannot change behavior. The base uses [DataTestMethod] so the consumer can apply
        // the real [TestMethod] without a type-name collision.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    internal new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByInternalMethodWithoutDiscoverInternals_Diagnostic()
    {
        // Without DiscoverInternals an internal derived method is not discoverable, so it does not hide the inherited
        // test and the mismatch is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    internal new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "DataTestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByProtectedMethodWithDiscoverInternals_Diagnostic()
    {
        // Even with DiscoverInternals the adapter only discovers public or internal methods, so a protected derived
        // method does not hide the inherited test and the mismatch is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    protected new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "DataTestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenNestedProtectedTestClassInheritsLegacyFixture_NoDiagnostic()
    {
        // A nested protected test class is not discovered by the adapter (even with DiscoverInternals, only nested
        // public/internal classes with a fully public/internal container chain are), so no inherited member runs and
        // there is nothing to report.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            namespace Repro
            {
                public class Outer
                {
                    [TestClass]
                    protected class InnerTests : TestBase
                    {
                        [TestMethod]
                        public void MyTest() { }
                    }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenTestClassNestedInGenericContainerInheritsLegacyFixture_NoDiagnostic()
    {
        // A concrete test class nested in a generic container cannot be constructed without the container's type
        // arguments, so the adapter never discovers it and no inherited member runs — nothing to report.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public class Outer<T>
                {
                    [TestClass]
                    public class InnerTests : TestBase
                    {
                        [TestMethod]
                        public void MyTest() { }
                    }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideOfEmittedMissingFrameworkFixture_NoDiagnostic()
    {
        // Missing-framework-PE override: the base library's [TestInitialize] is unresolved metadata, so its
        // AttributeUsage cannot be read. The standard attribute is Inherited=false, so an attribute-less override does
        // not carry it and is not discoverable even after recompiling — the analyzer must not report here.
        string baseLibraryCode = """
            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public virtual void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public override void BaseInitialize() { }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddEmittedMissingFrameworkBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedGenericTestMethodShadowedByRenamedTypeParameter_Diagnostic()
    {
        // MSTest de-duplicates by MethodInfo.ToString(), which prints the type parameter name, so 'Run<U>(U)' and
        // 'Run<T>(T)' have different discovery keys. The derived method therefore does not hide the inherited generic
        // test, and after recompiling the base both would be discovered, so the mismatch is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run<T>(T value) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public new void Run<U>(U value) { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "DataTestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedGenericTestMethodShadowedBySameTypeParameterName_NoDiagnostic()
    {
        // 'Run<T>(T)' and 'Run<T>(T)' produce the same MethodInfo.ToString() key, so the derived current-version test
        // method hides the inherited generic test and no mismatch is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void Run<T>(T value) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public new void Run<T>(T value) { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByGenericMethod_Diagnostic()
    {
        // A generic overload has a different arity, so it does not hide the inherited non-generic test method.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public void Run<T>() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodShadowedByByRefOverload_Diagnostic()
    {
        // A by-ref parameter is a different signature, so the overload does not hide the inherited test method.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run(int value) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public void Run(ref int value) { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenAbstractTestClassInheritsFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // An abstract test class is never instantiated, so no inherited fixture runs for it; only concrete derived
        // test classes are reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public abstract class SampleTestsBase : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenGenericTestClassInheritsFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // A generic test class is rejected by the adapter (open generics are not instantiated), so no inherited
        // fixture runs for it.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests<T> : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedDataTestMethodComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [DataTestMethod]
                    public void InheritedDataTest() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("InheritedDataTest", "TestBase", "DataTestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestCleanupComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestCleanupAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestCleanup]
                    public void BaseCleanup() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseCleanup", "TestBase", "TestCleanup", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedGenericTestMethodWithUninferableTypeParameterComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        // Generic method definitions are discovered even when type inference later fails during execution. Recompiling
        // the base therefore changes this test from silently absent to a discovered failing test.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    public void Run<T>() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedGenericTestMethodWithInferableTypeParameterComesFromDifferentFrameworkAssembly_Diagnostic()
    {
        // MSTest discovers a generic test method and constructs it from its data when every type parameter is inferable
        // from the parameters, so an inherited one from a differently named framework assembly is still silently skipped.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }

                public sealed class DataRowAttribute : System.Attribute
                {
                    public DataRowAttribute(object data) { }
                }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestMethod]
                    [DataRow(1)]
                    public void Run<T>(T value) { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "TestMethod", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedGenericTestInitializeComesFromDifferentFrameworkAssembly_NoDiagnostic()
    {
        // Fixtures are invoked with no inferable arguments, so a generic fixture never runs regardless of framework
        // version and must not be reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void Initialize<T>() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInternalTestClassWithoutDiscoverInternals_NoDiagnostic()
    {
        // An internal test class is not discovered without [assembly: DiscoverInternals], so no inherited member runs.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                internal class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInternalTestClassWithDiscoverInternals_Diagnostic()
    {
        // With [assembly: DiscoverInternals] an internal test class is discovered, so the inherited mismatch is still
        // reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            namespace Repro
            {
                [TestClass]
                internal class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeFromDifferentFrameworkAssemblyInVisualBasic_Diagnostic()
    {
        // The analyzer is symbol-based, so it reports the same mismatch for a Visual Basic test class inheriting a
        // base compiled against a differently named framework.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Namespace Repro
                <TestClass>
                Public Class {|#0:SampleTests|}
                    Inherits TestBase

                    <TestMethod>
                    Public Sub MyTest()
                    End Sub
                End Class
            End Namespace
            """;

        var test = new VerifyVB.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test.TestState, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyVB.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenInheritedTestInitializeShadowedByValidMethodInVisualBasic_NoDiagnostic()
    {
        // A valid same-name Shadows method in the derived Visual Basic class hides the inherited fixture, so the
        // mismatch is not reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Namespace Repro
                <TestClass>
                Public Class SampleTests
                    Inherits TestBase

                    Public Shadows Sub BaseInitialize()
                    End Sub

                    <TestMethod>
                    Public Sub MyTest()
                    End Sub
                End Class
            End Namespace
            """;

        var test = new VerifyVB.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test.TestState, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsInheritableCustomAttributeFromDifferentFrameworkAssemblyInVisualBasic_Diagnostic()
    {
        // Same inheritable-custom-attribute migration break as the C# case, verified for a Visual Basic Overrides.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Namespace Repro
                <TestClass>
                Public Class {|#0:SampleTests|}
                    Inherits TestBase

                    Public Overrides Sub Run()
                    End Sub
                End Class
            End Namespace
            """;

        var test = new VerifyVB.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test.TestState, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyVB.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Run", "TestBase", "RetryTest", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsNonInheritableCustomAttributeFromDifferentFrameworkAssemblyInVisualBasic_NoDiagnostic()
    {
        // With Inherited=false the Visual Basic override does not carry the base attribute, so nothing is reported.
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class DataTestMethodAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
                public sealed class RetryTestAttribute : DataTestMethodAttribute { }

                public abstract class TestBase
                {
                    [RetryTest]
                    public virtual void Run() { }
                }
            }
            """;

        string consumerCode = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Namespace Repro
                <TestClass>
                Public Class SampleTests
                    Inherits TestBase

                    Public Overrides Sub Run()
                    End Sub

                    <TestMethod>
                    Public Sub MyTest()
                    End Sub
                End Class
            End Namespace
            """;

        var test = new VerifyVB.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test.TestState, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenTestClassHasInvalidStaticTestContextProperty_NoDiagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public static TestContext TestContext { get; }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenTestClassHasNestedLookalikeTestContextProperty_Diagnostic()
    {
        string legacyBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Outer
                {
                    public sealed class TestContext { }
                }
            }

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    public static Outer.TestContext TestContext { get; }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenLookalikeAttributeComesFromUnrelatedAssembly_NoDiagnostic()
    {
        string lookalikeBaseCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : System.Attribute { }
            }

            namespace Repro
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAdditionalProject(test.TestState, "Contoso.Helpers", lookalikeBaseCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenLegacyFrameworkIsAlsoReferencedThroughAlias_Diagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedLegacyFrameworkBaseLibrary(test, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenLegacyTestClassAttributePrecedesCurrentAttribute_Diagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            extern alias legacy;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [legacy::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedLegacyFrameworkBaseLibrary(test, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenCurrentTestClassAttributeIsAliasedInLegacyProject_Diagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            extern alias current;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                [current::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedCurrentFrameworkBaseLibrary(test, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("BaseInitialize", "TestBase", "TestInitialize", CurrentFrameworkAssemblyName));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOnlyLegacyTestClassAttributeIsAppliedInCurrentProject_NoDiagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            extern alias legacy;

            namespace Repro
            {
                [legacy::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                public class SampleTests : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedLegacyFrameworkWithCurrentBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOnlyCurrentTestClassAttributeIsAppliedInLegacyProject_NoDiagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            extern alias current;

            namespace Repro
            {
                [current::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                public class SampleTests : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedCurrentFrameworkWithLegacyBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenLegacyFrameworkIsAliasedAndTestContextPropertyIsInvalid_NoDiagnostic()
    {
        string baseLibraryCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [TestInitialize]
                    public void BaseInitialize() { }
                }
            }
            """;

        string consumerCode = """
            extern alias legacy;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class SampleTests : TestBase
                {
                    public static legacy::Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; }

                    [TestMethod]
                    public void MyTest() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddAliasedLegacyFrameworkBaseLibrary(test, baseLibraryCode);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenCustomAttributeIsDeclaredInSeparateLegacyLibrary_Diagnostic()
    {
        string legacyFrameworkCode = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : System.Attribute { }
            }
            """;

        string customAttributeCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro.Attributes
            {
                public sealed class RetryTestAttribute : TestMethodAttribute { }
            }
            """;

        string baseLibraryCode = """
            using Repro.Attributes;

            namespace Repro
            {
                public abstract class TestBase
                {
                    [RetryTest]
                    public void InheritedTest() { }
                }
            }
            """;

        string consumerCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Repro
            {
                [TestClass]
                public class {|#0:SampleTests|} : TestBase
                {
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddSeparateCustomAttributeBaseProjects(test, legacyFrameworkCode, customAttributeCode, baseLibraryCode);

        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(InheritedMemberFromDifferentMSTestVersionAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("InheritedTest", "TestBase", "RetryTest", LegacyFrameworkAssemblyName));

        await test.RunAsync();
    }

    // Adds a base library whose MSTest lifecycle/test attributes are defined in an assembly named like the legacy v3
    // framework. It deliberately does NOT reference the real framework, so the attributes it applies bind to its own
    // types, reproducing the different-type-identity situation from issue #10505.
    private static void AddLegacyFrameworkBaseProject(VerifyCS.Test test, string libraryCode)
        => AddLegacyFrameworkBaseProject(test.TestState, libraryCode);

    private static void AddLegacyFrameworkBaseProject(SolutionState testState, string libraryCode)
        => AddAdditionalProject(testState, LegacyFrameworkAssemblyName, libraryCode);

    private static void AddAdditionalProject(SolutionState testState, string assemblyName, string source)
    {
        var project = new ProjectState(assemblyName, LanguageNames.CSharp, $"/{assemblyName}/", "cs");
        project.Sources.Add(($"{assemblyName}.cs", source));
        testState.AdditionalProjects.Add(assemblyName, project);
        testState.AdditionalProjectReferences.Add(assemblyName);
    }

    // Adds a legacy framework assembly plus a base library that references it, and links only the base library into the
    // consumer via project references. Roslyn project references keep the referenced symbols resolvable, so this is the
    // resolved cross-assembly case, not the absent-PE case — see AddEmittedMissingFrameworkBaseLibrary for the latter.
    private static void AddLegacyFrameworkAndBaseProjects(VerifyCS.Test test, string frameworkCode, string baseLibraryCode)
    {
        var frameworkProject = new ProjectState(LegacyFrameworkAssemblyName, LanguageNames.CSharp, "/LegacyFramework/", "cs");
        frameworkProject.Sources.Add(("LegacyFramework.cs", frameworkCode));
        test.TestState.AdditionalProjects.Add(LegacyFrameworkAssemblyName, frameworkProject);

        var baseLibraryProject = new ProjectState("BaseLibrary", LanguageNames.CSharp, "/BaseLibrary/", "cs");
        baseLibraryProject.Sources.Add(("BaseLibrary.cs", baseLibraryCode));
        baseLibraryProject.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        test.TestState.AdditionalProjects.Add("BaseLibrary", baseLibraryProject);
        test.TestState.AdditionalProjectReferences.Add("BaseLibrary");
    }

    // Adds a base library that references the real MSTest framework, so its attributes share the current framework's
    // type identity (the legitimate, no-diagnostic case).
    private static void AddSameFrameworkBaseProject(VerifyCS.Test test, string libraryCode)
    {
        var libraryProject = new ProjectState("SameVersionBase", LanguageNames.CSharp, "/SameVersionBase/", "cs");
        libraryProject.Sources.Add(("SameVersionBase.cs", libraryCode));
        libraryProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location));
        libraryProject.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(TestContext).Assembly.Location));
        test.TestState.AdditionalProjects.Add("SameVersionBase", libraryProject);
        test.TestState.AdditionalProjectReferences.Add("SameVersionBase");
    }

    // Emits a real legacy framework PE and a base-library PE compiled against it, then links ONLY the base library into
    // the consumer. Because the legacy framework PE is absent, the consumer sees the base library's attributes as
    // missing metadata whose constructor cannot bind — the exact shape (unlike a Roslyn project reference) that occurs
    // after NuGet unifies the package to the current major and drops the old framework assembly.
    private static void AddEmittedMissingFrameworkBaseLibrary(VerifyCS.Test test, string baseLibraryCode)
    {
        MetadataReference legacyFramework = EmitAssembly(LegacyFrameworkAssemblyName, LegacyFrameworkSource);
        MetadataReference baseLibrary = EmitAssembly("BaseLibrary", baseLibraryCode, legacyFramework);

        // Only the base library is referenced; the legacy framework PE is intentionally left out.
        test.TestState.AdditionalReferences.Add(baseLibrary);
    }

    private static void AddAliasedLegacyFrameworkBaseLibrary(VerifyCS.Test test, string baseLibraryCode)
    {
        MetadataReference legacyFramework = EmitAssembly(LegacyFrameworkAssemblyName, LegacyFrameworkSource);
        MetadataReference baseLibrary = EmitAssembly("BaseLibrary", baseLibraryCode, legacyFramework);

        test.TestState.AdditionalReferences.Add(
            legacyFramework.WithProperties(legacyFramework.Properties.WithAliases(["legacy"])));
        test.TestState.AdditionalReferences.Add(baseLibrary);
    }

    private static void AddAliasedLegacyFrameworkWithCurrentBaseLibrary(VerifyCS.Test test, string baseLibraryCode)
    {
        MetadataReference currentFramework = MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location);
        MetadataReference legacyFramework = EmitAssembly(LegacyFrameworkAssemblyName, LegacyFrameworkSource);
        MetadataReference baseLibrary = EmitAssembly("BaseLibrary", baseLibraryCode, currentFramework);

        test.TestState.AdditionalReferences.Add(
            legacyFramework.WithProperties(legacyFramework.Properties.WithAliases(["legacy"])));
        test.TestState.AdditionalReferences.Add(baseLibrary);
    }

    private static void AddAliasedCurrentFrameworkBaseLibrary(VerifyCS.Test test, string baseLibraryCode)
    {
        MetadataReference currentFramework = MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location);
        MetadataReference legacyFramework = EmitAssembly(LegacyFrameworkAssemblyName, LegacyFrameworkSource);
        MetadataReference baseLibrary = EmitAssembly("BaseLibrary", baseLibraryCode, currentFramework);

        test.TestState.AdditionalReferences.Add(legacyFramework);
        test.TestState.AdditionalReferences.Add(baseLibrary);
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            Project project = solution.GetProject(projectId)!;
            IEnumerable<MetadataReference> references = project.MetadataReferences.Select(reference =>
                IsCurrentFrameworkReference(reference)
                    ? reference.WithProperties(reference.Properties.WithAliases(["current"]))
                    : reference);
            return project.WithMetadataReferences(references).Solution;
        });
    }

    private static void AddAliasedCurrentFrameworkWithLegacyBaseLibrary(VerifyCS.Test test, string baseLibraryCode)
    {
        MetadataReference currentFramework = MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location);
        MetadataReference legacyFramework = EmitAssembly(LegacyFrameworkAssemblyName, LegacyFrameworkSource);
        MetadataReference baseLibrary = EmitAssembly("BaseLibrary", baseLibraryCode, legacyFramework);

        test.TestState.AdditionalReferences.Add(legacyFramework);
        test.TestState.AdditionalReferences.Add(baseLibrary);
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            Project project = solution.GetProject(projectId)!;
            IEnumerable<MetadataReference> references = project.MetadataReferences.Select(reference =>
                IsCurrentFrameworkReference(reference)
                    ? reference.WithProperties(reference.Properties.WithAliases(["current"]))
                    : reference);
            return project.WithMetadataReferences(references).Solution;
        });
    }

    private static bool IsCurrentFrameworkReference(MetadataReference reference)
        => reference is PortableExecutableReference { FilePath: { } filePath }
            && string.Equals(Path.GetFileName(filePath), "MSTest.TestFramework.dll", StringComparison.OrdinalIgnoreCase);

    private static void AddSeparateCustomAttributeBaseProjects(
        VerifyCS.Test test,
        string legacyFrameworkCode,
        string customAttributeCode,
        string baseLibraryCode)
    {
        var frameworkProject = new ProjectState(LegacyFrameworkAssemblyName, LanguageNames.CSharp, "/LegacyFramework/", "cs");
        frameworkProject.Sources.Add(("LegacyFramework.cs", legacyFrameworkCode));
        test.TestState.AdditionalProjects.Add(LegacyFrameworkAssemblyName, frameworkProject);

        var customAttributeProject = new ProjectState("CustomAttributes", LanguageNames.CSharp, "/CustomAttributes/", "cs");
        customAttributeProject.Sources.Add(("CustomAttributes.cs", customAttributeCode));
        customAttributeProject.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        test.TestState.AdditionalProjects.Add("CustomAttributes", customAttributeProject);

        var baseLibraryProject = new ProjectState("BaseLibrary", LanguageNames.CSharp, "/BaseLibrary/", "cs");
        baseLibraryProject.Sources.Add(("BaseLibrary.cs", baseLibraryCode));
        baseLibraryProject.AdditionalProjectReferences.Add("CustomAttributes");
        baseLibraryProject.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        test.TestState.AdditionalProjects.Add("BaseLibrary", baseLibraryProject);
        test.TestState.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        test.TestState.AdditionalProjectReferences.Add("CustomAttributes");
        test.TestState.AdditionalProjectReferences.Add("BaseLibrary");
    }

    private static void AddLegacyFrameworkExtensionsAndBaseProjects(
        VerifyCS.Test test,
        string legacyFrameworkCode,
        string legacyExtensionsCode,
        string baseLibraryCode)
    {
        var frameworkProject = new ProjectState(LegacyFrameworkAssemblyName, LanguageNames.CSharp, "/LegacyFramework/", "cs");
        frameworkProject.Sources.Add(("LegacyFramework.cs", legacyFrameworkCode));
        test.TestState.AdditionalProjects.Add(LegacyFrameworkAssemblyName, frameworkProject);

        var extensionsProject = new ProjectState(LegacyFrameworkExtensionsAssemblyName, LanguageNames.CSharp, "/LegacyFrameworkExtensions/", "cs");
        extensionsProject.Sources.Add(("LegacyFrameworkExtensions.cs", legacyExtensionsCode));
        test.TestState.AdditionalProjects.Add(LegacyFrameworkExtensionsAssemblyName, extensionsProject);

        var baseLibraryProject = new ProjectState("BaseLibrary", LanguageNames.CSharp, "/BaseLibrary/", "cs");
        baseLibraryProject.Sources.Add(("BaseLibrary.cs", baseLibraryCode));
        baseLibraryProject.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        baseLibraryProject.AdditionalProjectReferences.Add(LegacyFrameworkExtensionsAssemblyName);
        test.TestState.AdditionalProjects.Add("BaseLibrary", baseLibraryProject);
        test.TestState.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
        test.TestState.AdditionalProjectReferences.Add(LegacyFrameworkExtensionsAssemblyName);
        test.TestState.AdditionalProjectReferences.Add("BaseLibrary");
    }

    private static MetadataReference EmitAssembly(string assemblyName, string source, params MetadataReference[] additionalReferences)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            FrameworkReferences.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(peStream);
        Assert.IsTrue(emitResult.Success, $"Failed to emit '{assemblyName}': {string.Join("; ", emitResult.Diagnostics)}");

        return MetadataReference.CreateFromImage(peStream.ToArray());
    }

    // The emitted assemblies must be compiled against the same reference assemblies the analyzer test uses for the
    // consumer, otherwise the base library's core-type references (e.g. System.Object) resolve to a different corlib
    // identity and the consumer fails with CS0012.
    private static readonly MetadataReference[] FrameworkReferences = ResolveFrameworkReferences();

    private static MetadataReference[] ResolveFrameworkReferences()
    {
        string nuGetConfig = Path.Combine(RootFinder.Find(), "NuGet.config");
#if NET462
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.NetFramework.Net462.Default.WithNuGetConfigFilePath(nuGetConfig);
#else
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net80.WithNuGetConfigFilePath(nuGetConfig);
#endif
        return referenceAssemblies.ResolveAsync(LanguageNames.CSharp, System.Threading.CancellationToken.None).GetAwaiter().GetResult().ToArray();
    }

    // The MSTest attribute surface a v3-compiled base library would have been compiled against, in an assembly named
    // like the legacy framework so its type identity differs from the current MSTest.TestFramework.
    private const string LegacyFrameworkSource = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            public class TestContext { }

            public enum InheritanceBehavior { None, BeforeEachDerivedClass }

            public class TestClassAttribute : System.Attribute { }

            public class TestMethodAttribute : System.Attribute { }

            public sealed class TestInitializeAttribute : System.Attribute { }

            public sealed class ClassInitializeAttribute : System.Attribute
            {
                public ClassInitializeAttribute() { }
                public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior) { }
            }

            public sealed class ClassCleanupAttribute : System.Attribute
            {
                public ClassCleanupAttribute() { }
                public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior) { }
            }
        }
        """;
}
