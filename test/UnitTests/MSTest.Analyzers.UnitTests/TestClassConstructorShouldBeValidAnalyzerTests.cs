// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestClassConstructorShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestClassConstructorShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasPublicParameterlessConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithTestContext_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasNoExplicitConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPrivateParameterlessConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasProtectedConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                protected MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasInternalConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                internal MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithMultipleParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(int x, string y)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithWrongParameterType_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(string testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassHasBothValidAndInvalidConstructors_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                private MyTestClass(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicParameterlessAndPublicTestContextConstructors_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                public MyTestClass(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasInvalidConstructor_NoDiagnostic()
    {
        string code = """
            public class MyClass
            {
                private MyClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPrivateAndInternalConstructors_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }

                internal MyTestClass(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassInheritsFromBase_AndHasNoExplicitConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseClass
            {
                public BaseClass()
                {
                }
            }

            [TestClass]
            public class MyTestClass : BaseClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasPublicConstructorWithTestContextAndOthers_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|#0:MyTestClass|}
            {
                public MyTestClass(TestContext testContext, int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenTestClassIsAbstract_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                protected MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassIsStatic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public static class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDerivedTestClassAttributeHasPrivateConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [STATestClass]
            public class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenDerivedTestClassAttributeHasPublicParameterlessConstructor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [STATestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCustomDerivedTestClassAttributeHasInternalConstructor_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Class)]
            public class MyTestClassAttribute : TestClassAttribute
            {
            }

            [MyTestClass]
            public class {|#0:MyTestClass|}
            {
                internal MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenCustomDerivedTestClassAttributeHasPublicConstructor_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Class)]
            public class MyTestClassAttribute : TestClassAttribute
            {
            }

            [MyTestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenHostTestClassInjectionIsEnabled_ServiceConstructorHasNoDiagnostic()
    {
        string code = """
            #pragma warning disable MSTESTEXP

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class ApplicationService
            {
            }

            [TestClass]
            public sealed class MyTestClass(ApplicationService service)
            {
                private readonly ApplicationService _service = service;

                [TestMethod]
                public void TestMethod() => Assert.IsNotNull(_service);
            }

            public static class HostSetup
            {
                public static void Configure(IServiceCollection services)
                    => services.AddMSTestTestClassInjection();
            }
            """;

        var test = new VerifyCS.Test { TestCode = code };
        AddHostInjectionReferences(test);

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenHostTestClassInjectionIsEnabled_NonPublicConstructorHasDiagnostic()
    {
        string code = """
            #pragma warning disable MSTESTEXP

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class {|#0:MyTestClass|}
            {
                private MyTestClass()
                {
                }
            }

            public static class HostSetup
            {
                public static void Configure(IServiceCollection services)
                    => services.AddMSTestTestClassInjection();
            }
            """;

        var test = new VerifyCS.Test { TestCode = code };
        AddHostInjectionReferences(test);
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenLookalikeHostInjectionMethodIsUsed_ServiceConstructorHasDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class ApplicationService
            {
            }

            [TestClass]
            public sealed class {|#0:MyTestClass|}(ApplicationService service)
            {
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public static class MSTestHostingServiceCollectionExtensions
                {
                    public static object AddMSTestTestClassInjection(this object services) => services;
                }
            }

            public static class HostSetup
            {
                public static void Configure()
                    => Microsoft.Extensions.DependencyInjection.MSTestHostingServiceCollectionExtensions.AddMSTestTestClassInjection(new object());
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.TestClassConstructorShouldBeValidRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"),
            code);
    }

    [TestMethod]
    public async Task WhenHostInjectionHasMultiplePreferredConstructors_Diagnostic()
    {
        string code = """
            #pragma warning disable MSTESTEXP

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class FirstService
            {
            }

            public sealed class SecondService
            {
            }

            [TestClass]
            public sealed class {|#0:MyTestClass|}
            {
                [ActivatorUtilitiesConstructor]
                public MyTestClass(FirstService service)
                {
                }

                [ActivatorUtilitiesConstructor]
                public MyTestClass(SecondService service)
                {
                }
            }

            public static class HostSetup
            {
                public static void Configure(IServiceCollection services)
                    => services.AddMSTestTestClassInjection();
            }
            """;

        var test = new VerifyCS.Test { TestCode = code };
        AddHostInjectionReferences(test);
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.MultiplePreferredConstructorsRule)
                .WithLocation(0)
                .WithArguments("MyTestClass"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenHostInjectionUsesDerivedTestContext_Diagnostic()
    {
        string code = """
            #pragma warning disable MSTESTEXP

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class DerivedTestContext : TestContext
            {
            }

            [TestClass]
            public sealed class {|#0:MyTestClass|}
            {
                public MyTestClass(DerivedTestContext testContext)
                {
                }
            }

            public static class HostSetup
            {
                public static void Configure(IServiceCollection services)
                    => services.AddMSTestTestClassInjection();
            }
            """;

        var test = new VerifyCS.Test { TestCode = code };
        AddHostInjectionReferences(test);
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.DerivedTestContextRule)
                .WithLocation(0)
                .WithArguments("MyTestClass", "DerivedTestContext"));

        await test.RunAsync();
    }

    [DataRow("PublishAot")]
    [DataRow("RunAOTCompilation")]
    [DataRow("EnableMSTestSourceGeneration")]
    [DataRow("browser-wasm")]
    [TestMethod]
    public async Task WhenHostInjectionBuildModeIsUnsupported_Diagnostic(string buildMode)
    {
        string code = """
            #pragma warning disable MSTESTEXP

            using Microsoft.Extensions.DependencyInjection;

            [assembly: System.Reflection.AssemblyMetadata("MSTestHostTestClassInjectionUnsupportedMode", "$BUILD_MODE$")]

            public static class HostSetup
            {
                public static void Configure(IServiceCollection services)
                    => {|#0:services.AddMSTestTestClassInjection()|};
            }
            """.Replace("$BUILD_MODE$", buildMode);

        var test = new VerifyCS.Test { TestCode = code };
        AddHostInjectionReferences(test);
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(TestClassConstructorShouldBeValidAnalyzer.MSTestHostTestClassInjectionNotSupportedRule)
                .WithLocation(0)
                .WithArguments(buildMode));

        await test.RunAsync();
    }

    private static void AddHostInjectionReferences(VerifyCS.Test test)
    {
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.Extensions.DependencyInjection.MSTestHostingServiceCollectionExtensions).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));
    }
}
