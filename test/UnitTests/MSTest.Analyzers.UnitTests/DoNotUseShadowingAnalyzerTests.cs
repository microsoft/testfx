﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotUseShadowingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DoNotUseShadowingAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestClassHaveShadowingMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public void Method() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public new void [|Method|]() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_ButDifferentParameters_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public void Method(int x) { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public void Method() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_ButBothArePrivate_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                private void Method() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                private void Method() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_ButBaseIsPrivate_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                private void Method() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public void [|Method|]() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_ButDerivedClassIsPrivate_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public void Method() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                private void [|Method|]() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_ButOneIsGeneric_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            [TestClass]
            public class BaseTest
            {
                protected TObject CreateObject<TObject>()
                {
                    throw new NotImplementedException();
                }
            }

            [TestClass]
            public class ExtendedTest : BaseTest
            {
                private SomeType CreateObject()
                {
                    throw new NotImplementedException();
                }
            }

            public class SomeType
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveSameMethodAsBaseClassMethod_WithParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public void Method(int x) { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public void [|Method|](int y) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveOverrideProperty_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public abstract class BaseClass
            {
                public abstract int Method { get; }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public override int Method { get; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassOverrideMethodFromBaseClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public abstract class BaseClass
            {
                public abstract void Method();
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public override void Method() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveShadowingProperty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public TestContext TestContext { get; } = null;
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public new TestContext [|TestContext|] { get; } = null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveShadowingMethod_WithoutNewModifier_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public void Method() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public void [|Method|]() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHaveShadowingProperty_WithoutNewModifier_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                public TestContext TestContext { get; set; } = null;
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                public TestContext [|TestContext|] { get; set; } = null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassAndBaseClassHaveStaticCtor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class BaseClass
            {
                static BaseClass() { }
            }

            [TestClass]
            public class DerivedClass : BaseClass
            {
                static DerivedClass() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
