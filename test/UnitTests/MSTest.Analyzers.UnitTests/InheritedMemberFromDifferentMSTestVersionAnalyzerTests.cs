// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
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
    public async Task WhenInheritedTestInitializeFromUnreferencedFrameworkAssembly_Diagnostic()
    {
        // The realistic graph: the base library references the old framework, but the consumer compilation sees that
        // framework only through the base library metadata (it is not a direct reference of the test project).
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

    // Adds a base library whose MSTest lifecycle/test attributes are defined in an assembly named like the legacy v3
    // framework. It deliberately does NOT reference the real framework, so the attributes it applies bind to its own
    // types, reproducing the different-type-identity situation from issue #10505.
    private static void AddLegacyFrameworkBaseProject(VerifyCS.Test test, string libraryCode)
    {
        var libraryProject = new ProjectState(LegacyFrameworkAssemblyName, LanguageNames.CSharp, "/LegacyBase/", "cs");
        libraryProject.Sources.Add(("LegacyBase.cs", libraryCode));
        test.TestState.AdditionalProjects.Add(LegacyFrameworkAssemblyName, libraryProject);
        test.TestState.AdditionalProjectReferences.Add(LegacyFrameworkAssemblyName);
    }

    // Adds a legacy framework assembly plus a base library that references it, and links only the base library into
    // the consumer. The consumer therefore sees the legacy framework attribute as an unresolved/missing type reached
    // through the base library metadata — the realistic dotnet test graph.
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
}
