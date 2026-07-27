// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0077: <inheritdoc cref="Resources.SharedFileSystemPathInTestTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class SharedFileSystemPathInTestAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.SharedFileSystemPathInTestTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.SharedFileSystemPathInTestMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.SharedFileSystemPathInTestDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.SharedFileSystemPathInTestRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? fileSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOFile);
            INamedTypeSymbol? directorySymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIODirectory);
            if (fileSymbol is null && directorySymbol is null)
            {
                return;
            }

            INamedTypeSymbol? parallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingParallelizeAttribute);
            if (!ParallelSafetyHelper.IsParallelizationInEffect(compilation, context.Options, parallelizeAttributeSymbol))
            {
                return;
            }

            INamedTypeSymbol? testMethodAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols = ParallelSafetyHelper.GetFixtureAttributeSymbols(compilation);
            if (testMethodAttributeSymbol is null && fixtureAttributeSymbols.IsEmpty)
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, fileSymbol, directorySymbol, testMethodAttributeSymbol, fixtureAttributeSymbols),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? fileSymbol,
        INamedTypeSymbol? directorySymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;
        if (!targetMethod.IsStatic)
        {
            return;
        }

        INamedTypeSymbol containingType = targetMethod.ContainingType;
        bool isFile = SymbolEqualityComparer.Default.Equals(containingType, fileSymbol);
        bool isDirectory = SymbolEqualityComparer.Default.Equals(containingType, directorySymbol);
        if (!isFile && !isDirectory)
        {
            return;
        }

        // Precision: fire ONLY on APIs that create, write, move, or delete a filesystem entry. Reads
        // (File.ReadAllText, File.Exists, Directory.EnumerateFiles, ...) are intentionally excluded - reading a
        // shared fixture at a fixed path is common and safe, and flagging it would be a false positive. Dogfooding
        // proved that flagging path *construction* (e.g. Path.Combine(Path.GetTempPath(), "a.trx")) is a noise
        // generator, because the constructed string is overwhelmingly used as a hash input, a "does-not-exist"
        // sentinel, or mock data rather than for colliding I/O. The analyzer can only see the call, not the
        // resource, so it stays within the statically certain subset: a constant path passed directly to a
        // mutating File.*/Directory.* API. Tracing paths through helpers is left to the parallel-safety-audit skill.
        if (!IsMutatingFileSystemMethod(isFile, targetMethod.Name))
        {
            return;
        }

        if (!TryGetConstantPathArgument(invocation, out string? offendingPath) || offendingPath is null)
        {
            return;
        }

        IMethodSymbol? testMethod = ParallelSafetyHelper.GetEnclosingTestMethod(context.ContainingSymbol, fixtureAttributeSymbols, testMethodAttributeSymbol);
        if (testMethod is null)
        {
            return;
        }

        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, offendingPath));
    }

    /// <summary>
    /// Recognizes the <c>File.*</c>/<c>Directory.*</c> methods that create, write to, move, replace, or delete a
    /// filesystem entry. Read-only members (and pure path helpers) are excluded so that reading a shared fixture at
    /// a fixed path never triggers a diagnostic.
    /// </summary>
    private static bool IsMutatingFileSystemMethod(bool isFile, string methodName)
        => isFile
            ? methodName is "WriteAllText" or "WriteAllTextAsync"
                or "WriteAllBytes" or "WriteAllBytesAsync"
                or "WriteAllLines" or "WriteAllLinesAsync"
                or "AppendAllText" or "AppendAllTextAsync"
                or "AppendAllLines" or "AppendAllLinesAsync"
                or "AppendText"
                or "Create" or "CreateText"
                or "Copy" or "Move" or "Replace" or "Delete"
                or "OpenWrite" or "Open"
            : methodName is "CreateDirectory" or "CreateSymbolicLink"
                or "Delete" or "Move";

    /// <summary>
    /// Returns the constant string bound to the path parameter of a <c>File.*</c>/<c>Directory.*</c> call, when
    /// that argument is a compile-time constant (literal or <c>const</c>). Variable paths return <see langword="false"/>:
    /// the analyzer cannot know whether two tests collide, so it must stay silent on them.
    /// </summary>
    private static bool TryGetConstantPathArgument(IInvocationOperation invocation, out string? path)
    {
        path = null;
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter
                || parameter.Type.SpecialType != SpecialType.System_String)
            {
                continue;
            }

            if (parameter.Name is not ("path" or "sourceFileName" or "destFileName" or "sourceDirName" or "destDirName"))
            {
                continue;
            }

            if (argument.Value.ConstantValue is { HasValue: true, Value: string value }
                && value.Length > 0)
            {
                path = value;
                return true;
            }
        }

        return false;
    }
}
