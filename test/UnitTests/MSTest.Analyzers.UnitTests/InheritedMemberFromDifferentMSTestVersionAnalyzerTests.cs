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
        // The base library defines its own [TestInitialize] in the MSTest namespace inside a differently named
        // assembly, exactly like a base compiled against MSTest v3.
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
