// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.RedundantTestMethodAttributeAnalyzer,
    MSTest.Analyzers.RedundantTestMethodAttributeFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.RedundantTestMethodAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class RedundantTestMethodAttributeAnalyzerTests
{
    [TestMethod]
    public async Task WhenMethodConditionMatchesClassCondition_RemovesMethodCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:OSCondition(OperatingSystems.Windows)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodConditionIsBroaderThanClassCondition_RemovesMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodConditionExcludesOSAlreadyExcludedByClass_RemovesMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodConditionFurtherRestrictsClassCondition_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodConditionRestrictsUnknownOperatingSystems_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassOSConditionHasEmptyMessageAndMethodHasMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows, IgnoreMessage = "")]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux, IgnoreMessage = "Method reason")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassAndMethodOSConditionsHaveEmptyMessages_RemovesMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows, IgnoreMessage = "")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:OSCondition(OperatingSystems.Windows | OperatingSystems.Linux, IgnoreMessage = "")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows, IgnoreMessage = "")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodHasNoClassCondition_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenConditionsShareAttributeList_RemovesOnlyMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod, {|#0:OSCondition(OperatingSystems.Windows)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"),
            fixedCode);
    }

#if NET
    [TestMethod]
    public async Task WhenArchitectureConditionIsBroaderThanClassCondition_RemovesMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ArchitectureCondition(TestArchitectures.X64)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:ArchitectureCondition(TestArchitectures.X64 | TestArchitectures.Arm64)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ArchitectureCondition(TestArchitectures.X64)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[ArchitectureCondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassArchitectureConditionHasEmptyMessageAndMethodHasMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ArchitectureCondition(TestArchitectures.X64, IgnoreMessage = "")]
            public class MyTestClass
            {
                [TestMethod]
                [ArchitectureCondition(TestArchitectures.X64 | TestArchitectures.Arm64, IgnoreMessage = "Method reason")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif

    [TestMethod]
    public async Task WhenCIConditionMatchesClassCondition_RemovesMethodCondition()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [CICondition(ConditionMode.Exclude)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:CICondition(ConditionMode.Exclude)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [CICondition(ConditionMode.Exclude)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[CICondition]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassCIConditionHasEmptyMessageAndMethodHasMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [CICondition(ConditionMode.Exclude, IgnoreMessage = "")]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Exclude, IgnoreMessage = "Method reason")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassAndMethodDisableParallelization_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DoNotParallelize|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[DoNotParallelize]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenRetrySettingsMatchClassSettings_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Retry(3, MillisecondsDelayBetweenRetries = 100, BackoffType = DelayBackoffType.Exponential)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Retry(3, BackoffType = DelayBackoffType.Exponential, MillisecondsDelayBetweenRetries = 100)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Retry(3, MillisecondsDelayBetweenRetries = 100, BackoffType = DelayBackoffType.Exponential)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[Retry]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenRetrySettingsDifferFromClassSettings_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Retry(3)]
            public class MyTestClass
            {
                [TestMethod]
                [Retry(2)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassResourceLockIsStronger_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ResourceLock("Database")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:ResourceLock("Database", Mode = ResourceAccessMode.Read)|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ResourceLock("Database")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[ResourceLock]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodResourceLockIsStrongerThanClassLock_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ResourceLock("Database", Mode = ResourceAccessMode.Read)]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock("Database")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassIgnoreMessageTakesPrecedence_RemovesMethodIgnore()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore("Class reason")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore("Method reason")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore("Class reason")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[Ignore]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIgnoreProvidesOnlyMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore("Method reason")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestCategoryMatchesClassCategory_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [TestCategory("Integration")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:TestCategory("Integration")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [TestCategory("Integration")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[TestCategory]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestPropertyMatchesClassProperty_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [TestProperty("Feature", "Search")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:TestProperty("Feature", "Search")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [TestProperty("Feature", "Search")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[TestProperty]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestPropertyValueDiffersFromClassProperty_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [TestProperty("Feature", "Search")]
            public class MyTestClass
            {
                [TestMethod]
                [TestProperty("Feature", "Checkout")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDeploymentItemMatchesClassItem_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DeploymentItem("TestData/", "Data")]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DeploymentItem("testdata", "data")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DeploymentItem("TestData/", "Data")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[DeploymentItem]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenDependencyMatchesClassDependency_RemovesMethodAttribute()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(Setup))]
            public class MyTestClass
            {
                [TestMethod]
                public void Setup()
                {
                }

                [TestMethod]
                [{|#0:DependsOn(nameof(Setup))|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(Setup))]
            public class MyTestClass
            {
                [TestMethod]
                public void Setup()
                {
                }

                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("[DependsOn]", "TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodDependencyStrengthensClassDependency_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(Setup), ProceedOnFailure = true)]
            public class MyTestClass
            {
                [TestMethod]
                public void Setup()
                {
                }

                [TestMethod]
                [DependsOn(nameof(Setup))]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenClassDependencyIsDroppedAsSelfReference_MethodDependencyIsNotRedundant()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [DependsOn(nameof(TestMethod))]
            public class MyTestClass
            {
                [TestMethod]
                [DependsOn(nameof(TestMethod))]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMemberConditionMatchesClassCondition_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [MemberCondition(typeof(Conditions), nameof(Conditions.IsSupported))]
            public class MyTestClass
            {
                [TestMethod]
                [MemberCondition(typeof(Conditions), nameof(Conditions.IsSupported))]
                public void TestMethod()
                {
                }
            }

            public static class Conditions
            {
                public static bool IsSupported => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenExecutableConditionMatchesClassCondition_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [ExecutableCondition("dotnet")]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("dotnet")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodConditionMatchesClassConditionInVisualBasic_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            <OSCondition(OperatingSystems.Windows)>
            Public Class MyTestClass
                <TestMethod>
                <{|#0:OSCondition(OperatingSystems.Windows)|}>
                Public Sub TestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("[OSCondition]", "TestMethod"));
    }
}
