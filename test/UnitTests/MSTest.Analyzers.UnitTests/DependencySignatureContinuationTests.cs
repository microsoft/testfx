// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DependsOnShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DependencySignatureContinuationTests
{
    [TestMethod]
    [DataRow("T first, T second", "U first, U second")]
    [DataRow("System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<T[,]>> value", "System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<U[,]>> value")]
    [DataRow("T[] first, int[,] second", "U[] first, int[,] second")]
    [DataRow("dynamic first, T second", "object first, U second")]
    [DataRow("int* first, T second", "int* first, U second")]
    public async Task StructurallyEqualHiddenSignatures_DoNotInventCycles(string baseParameters, string derivedParameters)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public unsafe class Base
            {
                [TestMethod, DependsOn(nameof(Other))]
                public void Run<T>({{baseParameters}}) { }
                [TestMethod]
                public void Other() { }
            }
            [TestClass]
            public unsafe class Tests : Base
            {
                [TestMethod]
                public new void Run<U>({{derivedParameters}}) { }
                [TestMethod, DependsOn(nameof(Run))]
                public new void Other() { }
            }
            """;
        var test = new VerifyCS.Test { TestCode = source };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectCompilationOptions(
            projectId, ((Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        await test.RunAsync();
    }

    [TestMethod]
    [DataRow("T[] value", "U[,] value")]
    [DataRow("System.Collections.Generic.List<T> first, int second", "System.Collections.Generic.List<U> first, string second")]
    [DataRow("int* first, T second", "long* first, U second")]
    public async Task StructurallyDifferentOverloads_PreserveRealCycle(string baseParameters, string derivedParameters)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public unsafe class Base
            {
                [TestMethod, {|#0:DependsOn("Other")|}]
                public void Run<T>({{baseParameters}}) { }
            }
            [TestClass]
            public unsafe class Tests : Base
            {
                [TestMethod]
                public void Run<U>({{derivedParameters}}) { }
                [TestMethod, {|#1:DependsOn(nameof(Run))|}]
                public void Other() { }
            }
            """;
        var test = new VerifyCS.Test { TestCode = source };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectCompilationOptions(
            projectId, ((Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
            .WithLocation(0).WithArguments("Tests.Run > Tests.Other > Tests.Run"));
        test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DependsOnShouldBeValidAnalyzer.CycleRule)
            .WithLocation(1).WithArguments("Tests.Other > Tests.Run > Tests.Other"));
        await test.RunAsync();
    }
}
