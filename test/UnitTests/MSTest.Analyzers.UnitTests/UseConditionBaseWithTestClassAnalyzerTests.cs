// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseConditionBaseWithTestClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"));
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("CIConditionAttribute"));
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("MyConditionAttribute"));
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"));
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("OSConditionAttribute"));
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Level2ConditionAttribute"));
    }
}
