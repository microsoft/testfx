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
/// MSTEST0058: <inheritdoc cref="Resources.AvoidAssertsInCatchBlocksTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidAssertsInCatchBlocksAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidAssertsInCatchBlocksTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidAssertsInCatchBlocksMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidAssertsInCatchBlocksDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertsInCatchBlocksRuleId,
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
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            INamedTypeSymbol? stringAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert);
            INamedTypeSymbol? collectionAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert);
            INamedTypeSymbol? assertFailedExceptionSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssertFailedException);
            INamedTypeSymbol? assertInconclusiveExceptionSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssertInconclusiveException);

            bool hasAnyAssertType = assertSymbol is not null || stringAssertSymbol is not null || collectionAssertSymbol is not null;
            bool hasAnyExceptionType = assertFailedExceptionSymbol is not null || assertInconclusiveExceptionSymbol is not null;

            if (hasAnyAssertType)
            {
                context.RegisterOperationAction(
                    context => AnalyzeInvocation(context, assertSymbol, stringAssertSymbol, collectionAssertSymbol, assertInconclusiveExceptionSymbol),
                    OperationKind.Invocation);
            }

            if (hasAnyExceptionType)
            {
                context.RegisterOperationAction(
                    context => AnalyzeThrow(context, assertFailedExceptionSymbol, assertInconclusiveExceptionSymbol),
                    OperationKind.Throw);
            }
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? assertSymbol,
        INamedTypeSymbol? stringAssertSymbol,
        INamedTypeSymbol? collectionAssertSymbol,
        INamedTypeSymbol? assertInconclusiveExceptionSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        INamedTypeSymbol targetType = targetMethod.ContainingType;
        bool isAssertType =
            targetType.Equals(assertSymbol, SymbolEqualityComparer.Default) ||
            targetType.Equals(stringAssertSymbol, SymbolEqualityComparer.Default) ||
            targetType.Equals(collectionAssertSymbol, SymbolEqualityComparer.Default);

        if (!isAssertType)
        {
            return;
        }

        if (!IsInsideCatchClause(operation, out ICatchClauseOperation? enclosingCatch))
        {
            return;
        }

        // Exemption: Assert.Inconclusive inside a filtered catch (catch (...) when (...)) is the only
        // non-replicable use case in a catch block — it demotes a Failed outcome to Inconclusive based
        // on a detected runtime condition. Keep flagging it in unfiltered catches.
        if (IsAssertInconclusiveCall(targetMethod, assertSymbol) && enclosingCatch.Filter is not null)
        {
            return;
        }

        context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
    }

    private static void AnalyzeThrow(
        OperationAnalysisContext context,
        INamedTypeSymbol? assertFailedExceptionSymbol,
        INamedTypeSymbol? assertInconclusiveExceptionSymbol)
    {
        var throwOperation = (IThrowOperation)context.Operation;

        // Only flag explicit constructions ("throw new AssertFailedException(...)") so that bare
        // "throw;" rethrows and "throw ex;" (which propagate the caught exception unchanged) are not
        // flagged — they don't introduce a new MSTest assertion outcome at the catch site.
        IOperation? exception = throwOperation.Exception;

        // The thrown expression is often wrapped in an implicit conversion to System.Exception; unwrap it.
        while (exception is IConversionOperation conversion)
        {
            exception = conversion.Operand;
        }

        if (exception is not IObjectCreationOperation objectCreation ||
            objectCreation.Type is not INamedTypeSymbol thrownType)
        {
            return;
        }

        bool isAssertFailed = assertFailedExceptionSymbol is not null && thrownType.DerivesFrom(assertFailedExceptionSymbol);
        bool isAssertInconclusive = assertInconclusiveExceptionSymbol is not null && thrownType.DerivesFrom(assertInconclusiveExceptionSymbol);

        if (!isAssertFailed && !isAssertInconclusive)
        {
            return;
        }

        if (!IsInsideCatchClause(throwOperation, out ICatchClauseOperation? enclosingCatch))
        {
            return;
        }

        // Same exemption as the invocation path: a filtered catch expressing the demotion condition
        // makes "throw new AssertInconclusiveException(...)" the only way to produce an Inconclusive
        // outcome from a caught exception.
        if (isAssertInconclusive && enclosingCatch.Filter is not null)
        {
            return;
        }

        context.ReportDiagnostic(throwOperation.CreateDiagnostic(Rule));
    }

    private static bool IsAssertInconclusiveCall(IMethodSymbol method, INamedTypeSymbol? assertSymbol)
        => assertSymbol is not null
            && method.Name == "Inconclusive"
            && method.ContainingType.Equals(assertSymbol, SymbolEqualityComparer.Default);

    private static bool IsInsideCatchClause(IOperation operation, [NotNullWhen(true)] out ICatchClauseOperation? enclosingCatch)
    {
        IOperation? current = operation.Parent;
        while (current is not null)
        {
            if (current is ICatchClauseOperation catchClause)
            {
                enclosingCatch = catchClause;
                return true;
            }

            current = current.Parent;
        }

        enclosingCatch = null;
        return false;
    }
}
