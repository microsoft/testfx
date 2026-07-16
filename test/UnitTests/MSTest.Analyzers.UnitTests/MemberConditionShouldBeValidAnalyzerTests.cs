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
    public async Task WhenFieldIsInstance_MemberNotStatic()
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

    [TestMethod]
    public async Task WhenConditionTypeIsArrayType_MemberNotFound()
    {
        // typeof(int[]) is an IArrayTypeSymbol, not INamedTypeSymbol. The runtime would still throw
        // InvalidOperationException because int[] has no user-declared static bool members; the
        // analyzer must surface MSTEST0070 rather than silently skipping the attribute.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(int[]), "AnyName")|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("int[]", "AnyName"));
    }

    [TestMethod]
    public async Task WhenMethodIsInstance_MemberNotStatic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Conditions
            {
                public bool InstanceMethod() => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.InstanceMethod))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotStaticRule)
                .WithLocation(0)
                .WithArguments("Conditions", "InstanceMethod"));
    }

    [TestMethod]
    public async Task WhenMethodIsInternalStatic_MemberNotPublic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                internal static bool InternalMethod() => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.InternalMethod))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotPublicRule)
                .WithLocation(0)
                .WithArguments("Conditions", "InternalMethod"));
    }

    [TestMethod]
    public async Task WhenPropertyHasPrivateGetter_PropertyNotReadable()
    {
        // Property is public static but the getter is private — the runtime cannot invoke the getter.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool PrivateGet { private get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(typeof(Conditions), nameof(Conditions.PrivateGet))|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.PropertyNotReadableRule)
                .WithLocation(0)
                .WithArguments("Conditions", "PrivateGet"));
    }

    [TestMethod]
    public async Task WhenParamsArrayWithConditionMode_MultipleInvalidMembers_AllReported()
    {
        // Tests the 4-argument constructor overload: (ConditionMode, Type, string, params string[])
        // All member names (the fixed name + each params element) must be validated.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsTrue => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [{|#0:MemberCondition(ConditionMode.Exclude, typeof(Conditions), "Missing1", "Missing2")|}]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "Missing1"),
            VerifyCS.Diagnostic(MemberConditionShouldBeValidAnalyzer.MemberNotFoundRule)
                .WithLocation(0)
                .WithArguments("Conditions", "Missing2"));
    }

    [TestMethod]
    public async Task WhenMemberNameIsEmptyString_NoDiagnostic()
    {
        // The analyzer's ValidateMember calls string.IsNullOrWhiteSpace and returns early
        // for empty/whitespace member names without reporting a diagnostic; the runtime
        // constructor already throws ArgumentException for such values.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsReady => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), "")]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberNameIsWhitespace_NoDiagnostic()
    {
        // Same early-return path as empty string: whitespace-only names are skipped.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsReady => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), "   ")]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberNameIsEmptyInParamsArray_NoDiagnostic()
    {
        // Empty strings in the params array portion are also skipped; only the valid member
        // name "IsReady" is resolved (and it resolves correctly → no diagnostic).
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class Conditions
            {
                public static bool IsReady => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [MemberCondition(typeof(Conditions), "IsReady", "")]
                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
