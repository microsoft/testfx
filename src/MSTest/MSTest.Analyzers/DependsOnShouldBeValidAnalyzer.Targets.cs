// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class DependsOnShouldBeValidAnalyzer
{
    /// <summary>
    /// Validates one <c>[DependsOn]</c> application against the members it names.
    /// </summary>
    private static void AnalyzeTarget(
        SymbolAnalysisContext context,
        AttributeData attribute,
        AnalysisSymbols symbols,
        INamedTypeSymbol declaringClass,
        IMethodSymbol? declaringMethod)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } attributeSyntax)
        {
            return;
        }

        (ITypeSymbol? explicitTarget, string? targetMethodName, bool isMalformed) = ReadTarget(attribute);
        if (isMalformed)
        {
            return;
        }

        // An implicit target ("a method of my own class") is resolved against the class the test runs under.
        // For a test method declared on a class discovery does not run directly - an unannotated base, or an
        // abstract '[TestClass]' - that class is each concrete derived test class, which is not knowable from
        // this declaration, so only explicit 'typeof' targets are validated there.
        INamedTypeSymbol targetClass;
        if (explicitTarget is null)
        {
            if (!IsRunnableTestClass(declaringClass, symbols))
            {
                return;
            }

            targetClass = declaringClass;
        }
        else if (explicitTarget is INamedTypeSymbol namedTarget)
        {
            targetClass = namedTarget;
        }
        else
        {
            // typeof(int[]), typeof(int*), ... - the attribute accepts them, but an array or pointer can
            // never carry a '[TestClass]', so the reference is decidably dead. Reported before the
            // assembly check below, which has no answer for a type with no containing assembly.
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestClassRule, DescribeType(explicitTarget)));
            return;
        }

        if (targetClass.TypeKind == TypeKind.Error)
        {
            return;
        }

        // Discovery resolves a dependency against the tests of one test source, and stores a 'typeof' target
        // as its CLR full name. A type from another assembly therefore matches nothing, however well-formed
        // the reference is - so this is reported before the "is it a test class" question, which is moot.
        if (!IsFromCurrentAssembly(context, targetClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(OtherAssemblyRule, targetClass.Name));
            return;
        }

        if (!IsRunnableTestClass(targetClass, symbols))
        {
            // An abstract '[TestClass]' is skipped by discovery - its tests are enumerated under each
            // concrete derived class - so the reference matches nothing even though the attribute is there.
            // That needs its own message: "add [TestClass]" is not the fix. A static class is rejected by
            // discovery too (reflection sees it as abstract), but nothing derives from it, so it gets the
            // plain message instead.
            DiagnosticDescriptor rule = targetClass is { IsAbstract: true, IsStatic: false } && targetClass.IsTestClass(symbols.TestClassAttribute)
                ? AbstractTargetRule
                : NotATestClassRule;
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(rule, targetClass.Name));
            return;
        }

        if (targetMethodName is null)
        {
            // A whole-class target on a test class always matches something (or the class has no test at
            // all, which MSTEST0016 already reports).
            return;
        }

        if (string.IsNullOrWhiteSpace(targetMethodName))
        {
            // The attribute constructor throws for these, so there is nothing useful to add.
            return;
        }

        MethodLookup lookup = LookupMethods(targetClass, targetMethodName, symbols);
        if (!lookup.Exists)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MethodNotFoundRule, targetClass.Name, targetMethodName));
            return;
        }

        if (!lookup.IsTestMethod)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(NotATestMethodRule, targetClass.Name, targetMethodName));
            return;
        }

        // Only a reference written directly on the method is a self reference. The same reference written on
        // the class means "every *other* test waits for this one", and discovery drops the generated
        // self-edge.
        if (declaringMethod is not null
            && string.Equals(targetMethodName, declaringMethod.Name, StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(targetClass, declaringClass))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SelfReferenceRule, declaringMethod.Name));
        }
    }

    /// <summary>
    /// Reads the target of one <c>[DependsOn]</c> application: the three constructors are
    /// <c>(string)</c>, <c>(Type)</c> and <c>(Type, string)</c>, so the target class and method name can be
    /// told apart by argument kind.
    /// </summary>
    private static (ITypeSymbol? TargetClass, string? TargetMethodName, bool IsMalformed) ReadTarget(AttributeData attribute)
    {
        ITypeSymbol? targetClass = null;
        string? targetMethodName = null;
        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            // A null argument means the constructor throws and no dependency is ever recorded. Reading the
            // remaining arguments would misread '[DependsOn((Type)null!, nameof(A))]' as an implicit
            // same-class target and could report a self reference against code that never builds a graph.
            if (argument.IsNull)
            {
                return (null, null, true);
            }

            switch (argument)
            {
                case { Kind: TypedConstantKind.Type, Value: ITypeSymbol type }:
                    targetClass = type;
                    break;

                case { Kind: TypedConstantKind.Primitive, Value: string name }:
                    targetMethodName = name;
                    break;
            }
        }

        return (targetClass, targetMethodName, targetClass is null && targetMethodName is null);
    }

    private static List<AttributeData> GetDependsOnAttributes(ISymbol symbol, AnalysisSymbols symbols)
    {
        List<AttributeData> attributes = [];
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, symbols.DependsOnAttribute))
            {
                attributes.Add(attribute);
            }
        }

        return attributes;
    }
}
