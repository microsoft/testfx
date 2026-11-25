// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.CombinatorialDataShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CombinatorialDataShouldBeValidAnalyzerTests
{
    [TestMethod]
    [DataRow("string", DisplayName = "String")]
    [DataRow("long", DisplayName = "Long")]
    [DataRow("int[]", DisplayName = "Array")]
    [DataRow("uint?", DisplayName = "NullableUnsignedInteger")]
    public async Task UnsupportedUnprovidedParameter_ReportsItsNameAndType(string type)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check({{type}} {|#0:value|}) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic($"Cannot infer combinatorial values for parameter 'value' of type '{type}'"));
    }

    [TestMethod]
    public async Task InferredValuesAndTrailingCancellationToken_AreSupported()
    {
        const string source = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            public enum Color { Red, Blue }
            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check(bool flag, int number, Color color, bool? optionalFlag, int? optionalNumber, Color? optionalColor, CancellationToken token) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task NonTrailingCancellationToken_IsNotExcludedFromData()
    {
        const string source = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check(CancellationToken {|#0:token|}, bool value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("Cannot infer combinatorial values for parameter 'token' of type 'CancellationToken'"));
    }

    [TestMethod]
    public async Task ByReferenceParameter_IsNotInferredAsItsElementType()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check(ref bool {|#0:value|}) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("Cannot infer combinatorial values for parameter 'value' of type 'bool&'"));
    }

    [TestMethod]
    public async Task CustomInheritedProviderAndExplicitTokenProvider_AreSupported()
    {
        const string source = """
            using System;
            using System.Reflection;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            public class ProviderAttribute : Attribute, ICombinatorialValuesProvider
            {
                public object[] GetValues(ParameterInfo parameter) => new object[] { "value" };
            }
            public sealed class DerivedProviderAttribute : ProviderAttribute { }
            public sealed class TokenProviderAttribute : Attribute, ICombinatorialValuesProvider
            {
                public object[] GetValues(ParameterInfo parameter) => new object[] { default(CancellationToken) };
            }
            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check([DerivedProvider] string value, [TokenProvider] CancellationToken token) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MultipleProviders_ReportsTheParameter()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check([CombinatorialValues(1), CombinatorialRange(0, 2)] int {|#0:value|}) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("Parameter 'value' has multiple combinatorial value providers"));
    }

    [TestMethod]
    public async Task GenericParameter_DoesNotHideConflictingProviders()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check<T>([CombinatorialValues(1), CombinatorialRange(0, 2)] T {|#0:value|}) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("Parameter 'value' has multiple combinatorial value providers"));
    }

    [TestMethod]
    public async Task ClassGenericAndUnresolvedParameterTypes_AreNotGuessed()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            public abstract class Base<T> where T : struct
            {
                [TestMethod, CombinatorialData]
                public void Generic(T value, T? optional) { }
            }

            [TestClass]
            public class Tests : Base<bool>
            {

                [TestMethod, CombinatorialData]
                public void Missing(MissingType value) { }
            }
            """;

        await new VerifyCS.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync();
    }

    [TestMethod]
    public async Task MethodGenericParameterWithoutProvider_IsUnsupported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check<T>(T {|#0:value|}) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("Cannot infer combinatorial values for parameter 'value' of type 'T'"));
    }

    [TestMethod]
    [DataRow("0, 0", "int", DisplayName = "SignedZeroCount")]
    [DataRow("0, -1", "int", DisplayName = "SignedNegativeCount")]
    [DataRow("int.MaxValue, 2", "int", DisplayName = "SignedOverflow")]
    [DataRow("0u, 0u", "uint", DisplayName = "UnsignedZeroCount")]
    [DataRow("uint.MaxValue, 2u", "uint", DisplayName = "UnsignedOverflow")]
    [DataRow("0u, 2147483648u", "uint", DisplayName = "UnsignedExcessiveCount")]
    [DataRow("0, 5, 0", "int", DisplayName = "SignedZeroStep")]
    [DataRow("5, 0, 1", "int", DisplayName = "SignedWrongAscendingDirection")]
    [DataRow("0, 5, -1", "int", DisplayName = "SignedWrongDescendingDirection")]
    [DataRow("int.MinValue, int.MaxValue, 1", "int", DisplayName = "SignedExcessiveCount")]
    [DataRow("0u, 5u, 0u", "uint", DisplayName = "UnsignedZeroStep")]
    [DataRow("0u, uint.MaxValue, 1u", "uint", DisplayName = "UnsignedExcessiveRange")]
    public async Task InvalidRangeConstants_ReportTheProvider(string arguments, string parameterType)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check([{|#0:CombinatorialRange({{arguments}})|}] {{parameterType}} value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("CombinatorialRange arguments must define a valid non-empty range"));
    }

    [TestMethod]
    public async Task ValidRangeBoundariesAndUnsignedDescendingRange_AreNotReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Signed([CombinatorialRange(int.MaxValue, 1)] int maximum,
                    [CombinatorialRange(int.MaxValue, int.MinValue, int.MinValue)] int descending) { }

                [TestMethod, CombinatorialData]
                public void Unsigned([CombinatorialRange(uint.MaxValue, 1u)] uint maximum,
                    [CombinatorialRange(5u, 0u, 2u)] uint descending) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    [DataRow("Count = 0", DisplayName = "ZeroCount")]
    [DataRow("Count = -1", DisplayName = "NegativeCount")]
    [DataRow("Minimum = 5, Maximum = 4", DisplayName = "ReversedBounds")]
    [DataRow("Count = 3, Minimum = 2, Maximum = 3", DisplayName = "CountExceedsRange")]
    public async Task InvalidRandomConstants_ReportTheProvider(string arguments)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check([{|#0:CombinatorialRandomData({{arguments}})|}] int value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, Diagnostic("CombinatorialRandomData requires a positive Count and ordered bounds containing at least Count values"));
    }

    [TestMethod]
    public async Task RandomBoundariesAndUnspecifiedDefaults_AreNotGuessed()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            [TestClass]
            public class Tests
            {
                [TestMethod, CombinatorialData]
                public void Check([CombinatorialRandomData(Count = 2, Minimum = 2, Maximum = 3)] int bounded,
                    [CombinatorialRandomData(Count = int.MaxValue, Minimum = int.MinValue, Maximum = int.MaxValue)] int wide,
                    [CombinatorialRandomData(Count = 1, Maximum = -1)] int unknownDefault) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task AttributeOnNonTestMethod_IsNotReported()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

            public class Helper
            {
                [CombinatorialData]
                public void Check(string value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task FrameworkWithoutCombinatorialSymbols_DoesNotReport()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source, useMSTestV4: true);
    }

    private static DiagnosticResult Diagnostic(string message)
        => new DiagnosticResult("MSTEST0092", DiagnosticSeverity.Warning).WithLocation(0).WithMessage(message);
}
