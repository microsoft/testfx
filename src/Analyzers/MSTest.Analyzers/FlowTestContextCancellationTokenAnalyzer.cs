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
/// MSTEST0047: <inheritdoc cref="Resources.FlowTestContextCancellationTokenTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class FlowTestContextCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.FlowTestContextCancellationTokenTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.FlowTestContextCancellationTokenDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.FlowTestContextCancellationTokenMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor FlowTestContextCancellationTokenRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.FlowTestContextCancellationTokenRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(FlowTestContextCancellationTokenRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // Get the required symbols
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out INamedTypeSymbol? cancellationTokenSymbol))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol))
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, cancellationTokenSymbol, testContextSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol cancellationTokenSymbol, INamedTypeSymbol testContextSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocationOperation.TargetMethod;

        // Check if we're in a test method or test-related method context
        if (!IsInTestContext(context.ContainingSymbol))
        {
            return;
        }

        // Check if this method already has a CancellationToken parameter being passed
        bool hasCancellationTokenParameter = method.Parameters.Any(p => SymbolEqualityComparer.Default.Equals(p.Type, cancellationTokenSymbol));
        
        if (hasCancellationTokenParameter)
        {
            // Check if CancellationToken.None or default is being passed
            if (HasProblematicCancellationTokenArgument(invocationOperation, cancellationTokenSymbol))
            {
                context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(FlowTestContextCancellationTokenRule));
            }
            return;
        }

        // Look for an overload that accepts CancellationToken for common async patterns
        if (HasOverloadWithCancellationToken(method, cancellationTokenSymbol))
        {
            context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(FlowTestContextCancellationTokenRule));
        }
    }

    private static bool HasOverloadWithCancellationToken(IMethodSymbol method, INamedTypeSymbol cancellationTokenSymbol)
    {
        // Check for common patterns that we know have CancellationToken overloads
        if (IsCommonAsyncMethod(method))
        {
            return true;
        }

        // Look for overloads of the same method that accept CancellationToken
        INamedTypeSymbol containingType = method.ContainingType;
        
        foreach (ISymbol member in containingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol candidateMethod &&
                candidateMethod.MethodKind == method.MethodKind &&
                candidateMethod.IsStatic == method.IsStatic &&
                candidateMethod != method)
            {
                // Check if this method has the same parameters plus a CancellationToken
                if (IsCompatibleOverloadWithCancellationToken(method, candidateMethod, cancellationTokenSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCommonAsyncMethod(IMethodSymbol method)
    {
        // Common async methods that typically have CancellationToken overloads
        string typeName = method.ContainingType.ToDisplayString();
        string methodName = method.Name;

        return (typeName, methodName) switch
        {
            ("System.Threading.Tasks.Task", "Delay") => true,
            ("System.Threading.Tasks.Task", "Run") => true,
            ("System.Threading.Tasks.Task", "FromResult") => false, // This one doesn't have CT overload typically
            ("System.Net.Http.HttpClient", _) when methodName.EndsWith("Async") => true,
            ("System.IO.Stream", _) when methodName.EndsWith("Async") => true,
            ("System.IO.File", _) when methodName.EndsWith("Async") => true,
            ("System.Data.Common.DbCommand", "ExecuteReaderAsync") => true,
            ("System.Data.Common.DbCommand", "ExecuteNonQueryAsync") => true,
            ("System.Data.Common.DbCommand", "ExecuteScalarAsync") => true,
            _ => false
        };
    }

    private static bool HasProblematicCancellationTokenArgument(IInvocationOperation invocationOperation, INamedTypeSymbol cancellationTokenSymbol)
    {
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (SymbolEqualityComparer.Default.Equals(argument.Parameter?.Type, cancellationTokenSymbol))
            {
                // Check if it's CancellationToken.None or default(CancellationToken)
                if (argument.Value is IMemberReferenceOperation memberRef &&
                    memberRef.Member.Name == "None" &&
                    memberRef.Member.ContainingType?.Name == "CancellationToken")
                {
                    return true;
                }

                if (argument.Value is IDefaultValueOperation)
                {
                    return true;
                }

                // Check if it's not TestContext.CancellationTokenSource.Token
                if (!IsTestContextCancellationToken(argument.Value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTestContextCancellationToken(IOperation operation)
    {
        // Check if the operation represents TestContext.CancellationTokenSource.Token
        if (operation is IMemberReferenceOperation memberRef &&
            memberRef.Member.Name == "Token" &&
            memberRef.Instance is IMemberReferenceOperation sourceRef &&
            sourceRef.Member.Name == "CancellationTokenSource" &&
            sourceRef.Instance is IMemberReferenceOperation contextRef &&
            contextRef.Member.Name == "TestContext")
        {
            return true;
        }

        return false;
    }

    private static bool IsInTestContext(ISymbol containingSymbol)
    {
        // Check if we're in a test method, test class, or test fixture method
        var method = containingSymbol as IMethodSymbol;
        if (method is null)
        {
            return false;
        }

        // Check if the method is a test method or test fixture method
        foreach (AttributeData attribute in method.GetAttributes())
        {
            string? attributeName = attribute.AttributeClass?.Name;
            if (attributeName is "TestMethodAttribute" or "DataTestMethodAttribute" or
                "TestInitializeAttribute" or "TestCleanupAttribute" or
                "ClassInitializeAttribute" or "ClassCleanupAttribute" or
                "AssemblyInitializeAttribute" or "AssemblyCleanupAttribute")
            {
                return true;
            }
        }

        // Check if we're in a test class
        INamedTypeSymbol? containingType = method.ContainingType;
        if (containingType is not null)
        {
            foreach (AttributeData attribute in containingType.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == "TestClassAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCompatibleOverloadWithCancellationToken(IMethodSymbol originalMethod, IMethodSymbol candidateMethod, INamedTypeSymbol cancellationTokenSymbol)
    {
        // Check if the candidate method has all the same parameters as the original method plus a CancellationToken
        ImmutableArray<IParameterSymbol> originalParams = originalMethod.Parameters;
        ImmutableArray<IParameterSymbol> candidateParams = candidateMethod.Parameters;

        // The candidate should have one more parameter (the CancellationToken)
        if (candidateParams.Length != originalParams.Length + 1)
        {
            return false;
        }

        // Check if all original parameters match the first N parameters of the candidate
        for (int i = 0; i < originalParams.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(originalParams[i].Type, candidateParams[i].Type))
            {
                return false;
            }
        }

        // Check if the last parameter is CancellationToken
        IParameterSymbol lastParam = candidateParams[candidateParams.Length - 1];
        return SymbolEqualityComparer.Default.Equals(lastParam.Type, cancellationTokenSymbol);
    }
}