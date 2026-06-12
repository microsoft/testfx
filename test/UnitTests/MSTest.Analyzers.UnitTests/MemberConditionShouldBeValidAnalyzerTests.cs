// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.MemberConditionShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class MemberConditionShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenMemberIsValidPublicStaticBoolProperty_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), nameof(Conditions.IsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberIsValidPublicStaticBoolField_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static readonly bool IsTrue = true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), nameof(Conditions.IsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberIsValidPublicStaticBoolMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue() => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), nameof(Conditions.IsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAttributeIsOnTestClass_StillValidated()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [{|#0:MemberCondition(typeof(Conditions), "DoesNotExist")|}]
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "DoesNotExist"));
    }

    [TestMethod]
    public async Task WhenMemberDoesNotExist_MemberNotFound()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), "DoesNotExist")|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "DoesNotExist"));
    }

    [TestMethod]
    public async Task WhenMemberIsInternal_MemberNotPublic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                internal static bool InternalIsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.InternalIsTrue))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotPublicRule)
                .WithLocation(0)
                .WithArguments("Conditions", "InternalIsTrue"));
    }

    [TestMethod]
    public async Task WhenPropertyIsInstance_MemberNotStatic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Conditions
            {
                public bool InstanceIsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.InstanceIsTrue))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotStaticRule)
                .WithLocation(0)
                .WithArguments("Conditions", "InstanceIsTrue"));
    }

    [TestMethod]
    public async Task WhenPropertyReturnsNonBool_MemberWrongReturnType()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static int NotBool => 42;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.NotBool))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberWrongReturnTypeRule)
                .WithLocation(0)
                .WithArguments("Conditions", "NotBool"));
    }

    [TestMethod]
    public async Task WhenMethodHasParameters_MethodHasParameters()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool WithParam(int x) => x > 0;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.WithParam))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MethodHasParametersRule)
                .WithLocation(0)
                .WithArguments("Conditions", "WithParam"));
    }

    [TestMethod]
    public async Task WhenMethodReturnsNonBool_MemberWrongReturnType()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static int NotBoolMethod() => 0;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.NotBoolMethod))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberWrongReturnTypeRule)
                .WithLocation(0)
                .WithArguments("Conditions", "NotBoolMethod"));
    }

    [TestMethod]
    public async Task WhenPropertyIsWriteOnly_PropertyNotReadable()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool WriteOnly { set { } }
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.WriteOnly))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.PropertyNotReadableRule)
                .WithLocation(0)
                .WithArguments("Conditions", "WriteOnly"));
    }

    [TestMethod]
    public async Task WhenFieldIsNonBool_MemberWrongReturnType()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static readonly string NotBoolField = "x";
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.NotBoolField))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberWrongReturnTypeRule)
                .WithLocation(0)
                .WithArguments("Conditions", "NotBoolField"));
    }

    [TestMethod]
    public async Task WhenAdditionalMembersAreInvalid_AllReported()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
                public static int NotBool => 0;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.IsTrue), "Missing", nameof(Conditions.NotBool))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "Missing"),
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberWrongReturnTypeRule)
                .WithLocation(0)
                .WithArguments("Conditions", "NotBool"));
    }

    [TestMethod]
    public async Task WhenExplicitConditionMode_StillValidated()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(ConditionMode.Exclude, typeof(Conditions), "DoesNotExist")|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "DoesNotExist"));
    }

    [TestMethod]
    public async Task WhenInheritedPublicStaticMember_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class BaseConditions
            {
                public static bool InheritedIsTrue => true;
            }

            public class DerivedConditions : BaseConditions
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(DerivedConditions), nameof(DerivedConditions.InheritedIsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberIsEvent_MemberWrongKind()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static event EventHandler SomeEvent;

                public static void Raise() => SomeEvent?.Invoke(null, EventArgs.Empty);
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.SomeEvent))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberWrongKindRule)
                .WithLocation(0)
                .WithArguments("Conditions", "SomeEvent"));
    }

    [TestMethod]
    public async Task WhenStaticFieldIsInstance_MemberNotStatic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Conditions
            {
                public bool InstanceField = true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.InstanceField))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotStaticRule)
                .WithLocation(0)
                .WithArguments("Conditions", "InstanceField"));
    }

    [TestMethod]
    public async Task WhenMultipleConditionAttributes_EachValidated()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), "First")|}]
                [{|#1:MemberCondition(typeof(Conditions), "Second")|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "First"),
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(1)
                .WithArguments("Conditions", "Second"));
    }

    [TestMethod]
    public async Task WhenMethodHasParameterlessAndParameterizedOverloads_PicksParameterless_NoDiagnostic()
    {
        // Runtime binding uses Type.GetMethod(name, ..., types: Type.EmptyTypes), which selects
        // the parameterless overload. The analyzer must not falsely flag this just because the
        // parameterized overload comes first in declaration order.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue(int unused) => true;
                public static bool IsTrue() => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), nameof(Conditions.IsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedHasInstanceMemberShadowingBaseStatic_PicksBaseStatic_NoDiagnostic()
    {
        // Runtime FlattenHierarchy + Public + Static binds to Base.IsTrue (the static one),
        // not Derived.IsTrue (the instance one). The analyzer must mirror that.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Base
            {
                public static bool IsTrue => true;
            }

            public class Derived : Base
            {
                public new bool IsTrue => false; // instance, shadows the static base member
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Derived), nameof(Derived.IsTrue))]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
