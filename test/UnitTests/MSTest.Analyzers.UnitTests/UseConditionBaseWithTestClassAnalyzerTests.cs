// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseConditionBaseWithTestClassAnalyzer,
    MSTest.Analyzers.AddTestClassFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseConditionBaseWithTestClassAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasOSConditionAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasCIConditionAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [CICondition(ConditionMode.Include)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassHasOnlyTestClassAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenPlainClassHasNoAttributes_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasOSConditionAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasFullyQualifiedOSConditionAttributeWithoutUsing_FixAddsFullyQualifiedTestClass()
    {
        string code = """
            [Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Windows)]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            [Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Windows)]
            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasCIConditionAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [CICondition(ConditionMode.Include)]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [CICondition(ConditionMode.Include)]
            [TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("CIConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenInheritedTestClassAttributeHasOSConditionAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClassAttribute : TestClassAttribute { }

            [MyTestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasCustomConditionAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyConditionAttribute : ConditionBaseAttribute
            {
                public MyConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(MyConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [MyCondition]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyConditionAttribute : ConditionBaseAttribute
            {
                public MyConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(MyConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [MyCondition]
            [TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("MyConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasCustomConditionAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyConditionAttribute : ConditionBaseAttribute
            {
                public MyConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(MyConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [TestClass]
            [MyCondition]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenEnumHasCustomConditionAttribute_DiagnosticHasNoCodeFix()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Enum)]
            public sealed class EnumConditionAttribute : ConditionBaseAttribute
            {
                public EnumConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(EnumConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [EnumCondition]
            public enum {|#0:MyEnum|}
            {
                Value,
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("EnumConditionAttribute"),
            code);
    }

    [TestMethod]
    public async Task WhenInterfaceHasCustomConditionAttribute_DiagnosticHasNoCodeFix()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Interface)]
            public sealed class InterfaceConditionAttribute : ConditionBaseAttribute
            {
                public InterfaceConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(InterfaceConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [InterfaceCondition]
            public interface {|#0:IMyInterface|}
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("InterfaceConditionAttribute"),
            code);
    }

    [TestMethod]
    public async Task WhenStructHasStructOnlyCustomConditionAttribute_DiagnosticHasNoCodeFix()
    {
        // Converting the struct to a class would move the condition attribute onto a target its own
        // AttributeUsage does not allow, producing CS0592, so no fix should be offered here.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Struct)]
            public sealed class StructConditionAttribute : ConditionBaseAttribute
            {
                public StructConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(StructConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [StructCondition]
            public struct {|#0:MyStruct|}
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("StructConditionAttribute"),
            code);
    }

    [TestMethod]
    public async Task WhenRecordStructHasStructOnlyCustomConditionAttribute_DiagnosticHasNoCodeFix()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [AttributeUsage(AttributeTargets.Struct)]
            public sealed class StructConditionAttribute : ConditionBaseAttribute
            {
                public StructConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(StructConditionAttribute);
                public override bool IsConditionMet => true;
            }

            [StructCondition]
            public record struct {|#0:MyRecordStruct|}
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("StructConditionAttribute"),
            code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasMultipleConditionAttributes_SingleDiagnostic()
    {
        // The analyzer uses FirstOrDefault, so only one diagnostic fires (for the first found
        // ConditionBase attribute), regardless of how many condition attributes are present.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [CICondition(ConditionMode.Include)]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [CICondition(ConditionMode.Include)]
            [TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAbstractNonTestClassHasConditionAttribute_Diagnostic()
    {
        // This analyzer has no abstract-class exemption (unlike some other analyzers);
        // an abstract class that is not a TestClass should still fire the diagnostic.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            public abstract class {|#0:MyAbstractClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [TestClass]
            public abstract class MyAbstractClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTwoLevelDerivedConditionAttributeOnNonTestClass_Diagnostic()
    {
        // The Inherits() check is recursive: an attribute that is a 2nd-level subclass of
        // ConditionBaseAttribute should still trigger the diagnostic.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Level1ConditionAttribute : ConditionBaseAttribute
            {
                public Level1ConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(Level1ConditionAttribute);
                public override bool IsConditionMet => true;
            }

            public class Level2ConditionAttribute : Level1ConditionAttribute
            {
                public override string GroupName => nameof(Level2ConditionAttribute);
            }

            [Level2Condition]
            public class {|#0:MyClass|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Level1ConditionAttribute : ConditionBaseAttribute
            {
                public Level1ConditionAttribute() : base(ConditionMode.Include) { }
                public override string GroupName => nameof(Level1ConditionAttribute);
                public override bool IsConditionMet => true;
            }

            public class Level2ConditionAttribute : Level1ConditionAttribute
            {
                public override string GroupName => nameof(Level2ConditionAttribute);
            }

            [Level2Condition]
            [TestClass]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Level2ConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestRecordClassHasConditionAttribute_FixAddsTestClass()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            public record class {|#0:MyRecord|}
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [TestClass]
            public record class MyRecord
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNestedNonTestClassHasConditionAttribute_FixAddsTestClassToNestedTypeOnly()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Outer
            {
                [OSCondition(OperatingSystems.Windows)]
                public class {|#0:Inner|}
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Outer
            {
                [OSCondition(OperatingSystems.Windows)]
                [TestClass]
                public class Inner
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenGenericNonTestClassHasConditionAttribute_FixAddsTestClass()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            public class {|#0:MyClass|}<T> where T : class
            {
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [OSCondition(OperatingSystems.Windows)]
            [TestClass]
            public class MyClass<T> where T : class
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"),
            fixedCode);
    }
}
