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
        // The base lifecycle method is overridden by a more-derived method, so the override (compiled against the
        // current version) is what runs; the base attribute is moot.
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
        // A same-signature derived method hides the inherited test, so it is the derived one that runs.
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
                public class SampleTests : TestBase
                {
                    public new void Run() { }
                }
            }
            """;

        var test = new VerifyCS.Test { TestCode = consumerCode };
        AddLegacyFrameworkBaseProject(test, legacyBaseCode);

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

    // Adds a base library whose MSTest lifecycle/test attributes are defined in an assembly named like the legacy v3
    // framework. It deliberately does NOT reference the real framework, so the attributes it applies bind to its own
    // types, reproducing the different-type-identity situation from issue #10505.
    private static void AddLegacyFrameworkBaseProject(VerifyCS.Test test, string libraryCode)
        => AddLegacyFrameworkBaseProject(test.TestState, libraryCode);

    private static void AddLegacyFrameworkBaseProject(SolutionState testState, string libraryCode)
    {
        var libraryProject = new ProjectState(LegacyFrameworkAssemblyName, LanguageNames.CSharp, "/LegacyBase/", "cs");
        libraryProject.Sources.Add(("LegacyBase.cs", libraryCode));
        testState.AdditionalProjects.Add(LegacyFrameworkAssemblyName, libraryProject);
        testState.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
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
