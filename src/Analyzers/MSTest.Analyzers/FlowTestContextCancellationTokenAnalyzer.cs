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
/// MSTEST0049: <inheritdoc cref="Resources.FlowTestContextCancellationTokenTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class FlowTestContextCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.FlowTestContextCancellationTokenTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.FlowTestContextCancellationTokenDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.FlowTestContextCancellationTokenMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal const string TestContextMemberNamePropertyKey = nameof(TestContextMemberNamePropertyKey);
    internal const string CancellationTokenParameterNamePropertyKey = nameof(CancellationTokenParameterNamePropertyKey);

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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out INamedTypeSymbol? cancellationTokenSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out INamedTypeSymbol? classCleanupAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute, out INamedTypeSymbol? assemblyCleanupAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, cancellationTokenSymbol, testContextSymbol, classCleanupAttributeSymbol, assemblyCleanupAttributeSymbol, testMethodAttributeSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol cancellationTokenSymbol,
        INamedTypeSymbol testContextSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol,
        INamedTypeSymbol testMethodAttributeSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocationOperation.TargetMethod;

        // Check if we're in a context where a TestContext is already available or could be made available.
        if (!HasOrCouldHaveTestContextInScope(context.ContainingSymbol, testContextSymbol, classCleanupAttributeSymbol, assemblyCleanupAttributeSymbol, testMethodAttributeSymbol, out string? testContextMemberNameInScope, out TestContextState? testContextState))
        {
            return;
        }

        IParameterSymbol? cancellationTokenParameter = method.Parameters.LastOrDefault(p => SymbolEqualityComparer.Default.Equals(p.Type, cancellationTokenSymbol));
        bool parameterHasDefaultValue = cancellationTokenParameter is not null && cancellationTokenParameter.HasExplicitDefaultValue;
        if (cancellationTokenParameter is not null && !parameterHasDefaultValue)
        {
            // The called method has a required CancellationToken parameter.
            // No need to report diagnostic, even if user is explicitly passing CancellationToken.None or default(CancellationToken).
            // We consider it "intentional" if the user is passing it explicitly.
            return;
        }

        if (parameterHasDefaultValue &&
            invocationOperation.Arguments.FirstOrDefault(arg => SymbolEqualityComparer.Default.Equals(arg.Parameter, cancellationTokenParameter))?.ArgumentKind != ArgumentKind.Explicit)
        {
            // The called method has an optional CancellationToken parameter, but it was not explicitly provided. So we report a diagnostic.
            // We also pass non-null cancellationTokenParameterName if the codefix should use named argument.
            string? cancellationTokenParameterName = null;
            int indexOfParameterCorrespondingToLastExplicitArgument = invocationOperation.Arguments.LastOrDefault(arg => arg.ArgumentKind == ArgumentKind.Explicit)?.Parameter?.Ordinal ?? -1;
            if (cancellationTokenParameter!.Ordinal != indexOfParameterCorrespondingToLastExplicitArgument + 1)
            {
                cancellationTokenParameterName = cancellationTokenParameter.Name;
            }

            context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(FlowTestContextCancellationTokenRule, properties: GetPropertiesBag(testContextMemberNameInScope, testContextState, cancellationTokenParameterName)));
            return;
        }

        // At this point, we want to only continue analysis if and only if the called method didn't have a CancellationToken parameter.
        // In this case, we look for other overloads that might accept a CancellationToken.
        if (cancellationTokenParameter is null &&
            GetCancellationTokenParameterOfOverloadWithCancellationToken(method, cancellationTokenSymbol) is { } cancellationTokenParameterFromDifferentOverload)
        {
            string? cancellationTokenParameterName = null;
            int indexOfParameterCorrespondingToLastExplicitArgument = invocationOperation.Arguments.LastOrDefault(arg => arg.ArgumentKind == ArgumentKind.Explicit)?.Parameter?.Ordinal ?? -1;

            if (cancellationTokenParameterFromDifferentOverload.Ordinal != indexOfParameterCorrespondingToLastExplicitArgument + 1)
            {
                cancellationTokenParameterName = cancellationTokenParameterFromDifferentOverload.Name;
            }

            context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(FlowTestContextCancellationTokenRule, properties: GetPropertiesBag(testContextMemberNameInScope, testContextState, cancellationTokenParameterName)));
        }

        static ImmutableDictionary<string, string?> GetPropertiesBag(string? testContextMemberNameInScope, TestContextState? testContextState, string? cancellationTokenParameterName)
        {
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty;
            properties = testContextMemberNameInScope is not null
                ? properties.Add(TestContextMemberNamePropertyKey, testContextMemberNameInScope)
                : properties.Add(nameof(TestContextState), testContextState.ToString());

            if (cancellationTokenParameterName is not null)
            {
                properties = properties.Add(CancellationTokenParameterNamePropertyKey, cancellationTokenParameterName);
            }

            return properties;
        }
    }

    private static IParameterSymbol? GetCancellationTokenParameterOfOverloadWithCancellationToken(IMethodSymbol method, INamedTypeSymbol cancellationTokenSymbol)
    {
        // Look for overloads of the same method that accept CancellationToken
        INamedTypeSymbol containingType = method.ContainingType;

        foreach (ISymbol member in containingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol candidateMethod &&
                candidateMethod.MethodKind == method.MethodKind &&
                candidateMethod.IsStatic == method.IsStatic)
            {
                // Check if this method has the same parameters plus a CancellationToken
                if (GetCancellationTokenParameterIfCandidateIsValid(method, candidateMethod, cancellationTokenSymbol) is { } parameter)
                {
                    return parameter;
                }
            }
        }

        return null;
    }

    private static bool HasOrCouldHaveTestContextInScope(
        ISymbol containingSymbol,
        INamedTypeSymbol testContextSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol,
        INamedTypeSymbol testMethodAttributeSymbol,
        out string? testContextMemberNameInScope,
        [NotNullWhen(true)] out TestContextState? testContextState)
    {
        testContextMemberNameInScope = null;
        testContextState = null;

        if (containingSymbol is not IMethodSymbol method)
        {
            return false;
        }

        // We have a TestContext in scope (as a parameter)
        if (method.Parameters.FirstOrDefault(p => testContextSymbol.Equals(p.Type, SymbolEqualityComparer.Default)) is { } testContextParameter)
        {
            testContextMemberNameInScope = testContextParameter.Name;
            testContextState = TestContextState.InScope;
            return true;
        }

        // We have a TestContext in scope (as a field or property)
        if (!method.IsStatic &&
            method.ContainingType.GetMembers().FirstOrDefault(
                m => !m.IsStatic && m.Kind is SymbolKind.Field or SymbolKind.Property && testContextSymbol.Equals(m.GetMemberType(), SymbolEqualityComparer.Default)) is { } testContextMember)
        {
            testContextMember = (testContextMember as IFieldSymbol)?.AssociatedSymbol ?? testContextMember;
            // Workaround https://github.com/dotnet/roslyn/issues/70208
            // https://github.com/dotnet/roslyn/blob/f25ae8e02a91169f45060951a168b233ad588ed3/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNameKind.cs#L47
            testContextMemberNameInScope = testContextMember.Name.StartsWith('<') && testContextMember.Name.EndsWith(">P", StringComparison.Ordinal)
                ? testContextMember.Name.Substring(1, testContextMember.Name.Length - 3)
                : testContextMember.Name;
            testContextState = TestContextState.InScope;
            return true;
        }

        // If we have AssemblyCleanup or ClassCleanup with no parameters, then we *could* have a TestContext in scope.
        // Note that assembly/class cleanup method can optionally have a TestContext parameter, but it is not required.
        // Also, for test methods (parameterized or not), we *could* have a TestContext in scope by adding a property to the test class (or injecting it via constructor).
        ImmutableArray<AttributeData> attributes = method.GetAttributes();
        foreach (AttributeData attribute in attributes)
        {
            if (method.Parameters.IsEmpty &&
                (classCleanupAttributeSymbol.Equals(attribute.AttributeClass, SymbolEqualityComparer.Default) ||
                assemblyCleanupAttributeSymbol.Equals(attribute.AttributeClass, SymbolEqualityComparer.Default)))
            {
                testContextState = TestContextState.CouldBeInScopeAsParameter;
                return true;
            }

            if (attribute.AttributeClass?.Inherits(testMethodAttributeSymbol) == true)
            {
                testContextState = TestContextState.CouldBeInScopeAsProperty;
                return true;
            }
        }

        return false;
    }

    private static IParameterSymbol? GetCancellationTokenParameterIfCandidateIsValid(IMethodSymbol originalMethod, IMethodSymbol candidateMethod, INamedTypeSymbol cancellationTokenSymbol)
    {
        // Check if the candidate method has all the same parameters as the original method plus a CancellationToken
        ImmutableArray<IParameterSymbol> originalParams = originalMethod.Parameters;
        ImmutableArray<IParameterSymbol> candidateParams = candidateMethod.Parameters;

        // The candidate should have one more parameter (the CancellationToken)
        if (candidateParams.Length != originalParams.Length + 1)
        {
            return null;
        }

        // Check if all original parameters match the first N parameters of the candidate
        for (int i = 0; i < originalParams.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(originalParams[i].Type, candidateParams[i].Type))
            {
                return null;
            }
        }

        // Check if the last parameter is CancellationToken
        IParameterSymbol lastParam = candidateParams[candidateParams.Length - 1];
        return SymbolEqualityComparer.Default.Equals(lastParam.Type, cancellationTokenSymbol) ? lastParam : null;
    }

    internal enum TestContextState
    {
        InScope,
        CouldBeInScopeAsParameter,
        CouldBeInScopeAsProperty,
    }
}
