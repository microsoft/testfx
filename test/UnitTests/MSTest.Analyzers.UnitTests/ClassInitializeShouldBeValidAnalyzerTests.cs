// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.ClassInitializeShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class ClassInitializeShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenClassInitializeIsPublic_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassInitializeIsGenericWithInheritanceModeSet_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
                public static void ClassInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassInitializeIsGenericWithInheritanceModeSetToNone_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassInitialize(InheritanceBehavior.None)]
                public static void {|#0:ClassInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.NotAGenericClassUnlessInheritanceModeSetRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeIsGenericWithoutSettingInheritanceMode_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassInitialize]
                public static void {|#0:ClassInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.NotAGenericClassUnlessInheritanceModeSetRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassInitializeIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                internal static void {|#0:ClassInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenClassInitializeIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                {{accessibility}} static void {|#0:ClassInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeIsNotOrdinary_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                ~{|#0:MyTestClass|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenClassInitializeIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void {|#0:ClassInitialize|}<T>(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeIsNotStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public void {|#0:ClassInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.StaticRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeDoesNotHaveParameters_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void {|#0:ClassInitialize|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.SingleContextParameterRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }

    public async Task WhenClassInitializeReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static int {|#0:ClassInitialize0|}(TestContext context)
                {
                    return 0;
                }

                [ClassInitialize]
                public static string {|#1:ClassInitialize1|}(TestContext context)
                {
                    return "0";
                }

                [ClassInitialize]
                public static Task<int> {|#2:ClassInitialize2|}(TestContext context)
                {
                    return Task.FromResult(0);
                }

                [ClassInitialize]
                public static ValueTask<int> {|#3:ClassInitialize3|}(TestContext context)
                {
                    return ValueTask.FromResult(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize0"),
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("ClassInitialize1"),
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("ClassInitialize2"),
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("ClassInitialize3"));
    }

    public async Task WhenClassInitializeReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInitialize0(TestContext context)
                {
                }

                [ClassInitialize]
                public static Task ClassInitialize1(TestContext context)
                {
                    return Task.CompletedTask;
                }

                [ClassInitialize]
                public static ValueTask ClassInitialize2(TestContext context)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassInitializeIsAsyncVoid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static async void {|#0:ClassInitialize|}(TestContext context)
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassInitializeShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("ClassInitialize"));
    }
}
