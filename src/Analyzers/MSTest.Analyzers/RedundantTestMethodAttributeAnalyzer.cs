// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0086: <inheritdoc cref="Resources.RedundantTestMethodAttributeTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class RedundantTestMethodAttributeAnalyzer : DiagnosticAnalyzer
{
    private const int AllOperatingSystems = (1 << 4) - 1;
    private const int AllArchitectures = (1 << 10) - 1;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.RedundantTestMethodAttributeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.RedundantTestMethodAttributeMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.RedundantTestMethodAttributeDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.RedundantTestMethodAttributeRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                    out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute,
                    out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            var symbols = new KnownAttributeSymbols(
                context.Compilation,
                testMethodAttributeSymbol,
                testClassAttributeSymbol);

            context.RegisterSymbolAction(
                context => AnalyzeMethod(context, symbols),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, KnownAttributeSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        ImmutableArray<AttributeData> classAttributes = methodSymbol.ContainingType.GetAttributes();
        if (!methodAttributes.Any(attribute => attribute.AttributeClass.Inherits(symbols.TestMethod))
            || !classAttributes.Any(attribute => attribute.AttributeClass.Inherits(symbols.TestClass)))
        {
            return;
        }

        ImmutableArray<AttributeData> inheritedClassAttributes = GetClassAttributesIncludingBaseTypes(methodSymbol.ContainingType);
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            if (methodAttribute.ApplicationSyntaxReference is not { } syntaxReference
                || !IsRedundant(
                    methodAttribute,
                    classAttributes,
                    inheritedClassAttributes,
                    methodSymbol,
                    symbols))
            {
                continue;
            }

            string attributeName = FormatAttributeName(methodAttribute.AttributeClass);
            context.ReportDiagnostic(syntaxReference.CreateDiagnostic(Rule, context.CancellationToken, attributeName, methodSymbol.Name));
        }
    }

    private static ImmutableArray<AttributeData> GetClassAttributesIncludingBaseTypes(INamedTypeSymbol type)
    {
        ImmutableArray<AttributeData>.Builder attributes = ImmutableArray.CreateBuilder<AttributeData>();
        for (INamedTypeSymbol? currentType = type; currentType is not null; currentType = currentType.BaseType)
        {
            attributes.AddRange(currentType.GetAttributes());
        }

        return attributes.ToImmutable();
    }

    private static string FormatAttributeName(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return "Attribute";
        }

        const string attributeSuffix = "Attribute";
        string name = attributeClass.Name;
        return name.EndsWith(attributeSuffix, StringComparison.Ordinal)
            ? $"[{name.Substring(0, name.Length - attributeSuffix.Length)}]"
            : $"[{name}]";
    }

    private static bool IsRedundant(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        ImmutableArray<AttributeData> inheritedClassAttributes,
        IMethodSymbol methodSymbol,
        KnownAttributeSymbols symbols)
    {
        INamedTypeSymbol? attributeClass = methodAttribute.AttributeClass;
        bool declaringClassIsSealed = methodSymbol.ContainingType.IsSealed;
        return attributeClass switch
        {
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.OSCondition)
                => declaringClassIsSealed
                && IsFlagConditionRedundant(
                methodAttribute,
                FindAttribute(classAttributes, symbols.OSCondition),
                AllOperatingSystems,
                excludeAllowsUnknownValues: true),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.ArchitectureCondition)
                => declaringClassIsSealed
                && IsFlagConditionRedundant(
                methodAttribute,
                FindAttribute(classAttributes, symbols.ArchitectureCondition),
                AllArchitectures,
                excludeAllowsUnknownValues: false),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.CICondition)
                => declaringClassIsSealed
                && IsCIConditionRedundant(methodAttribute, FindAttribute(classAttributes, symbols.CICondition)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DoNotParallelize)
                => FindAttribute(inheritedClassAttributes, symbols.DoNotParallelize) is not null,
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.ResourceLock)
                => IsResourceLockRedundant(methodAttribute, inheritedClassAttributes, symbols.ResourceLock),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.Retry)
                => declaringClassIsSealed
                && IsRetryRedundant(methodAttribute, FindAttribute(classAttributes, symbols.Retry)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.Ignore)
                => declaringClassIsSealed
                && IsIgnoreRedundant(methodAttribute, FindAttribute(classAttributes, symbols.Ignore)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.TestCategory)
                => HasEquivalentAttribute(methodAttribute, inheritedClassAttributes, symbols.TestCategory),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.TestProperty)
                => HasEquivalentAttribute(methodAttribute, inheritedClassAttributes, symbols.TestProperty),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DeploymentItem)
                => IsDeploymentItemRedundant(methodAttribute, inheritedClassAttributes, symbols.DeploymentItem),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DependsOn)
                => declaringClassIsSealed
                && IsDependsOnRedundant(methodAttribute, classAttributes, methodSymbol, symbols.DependsOn),
            _ => false,
        };
    }

    private static AttributeData? FindAttribute(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? attributeSymbol)
        => attributeSymbol is null
            ? null
            : attributes.FirstOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));

    private static bool IsFlagConditionRedundant(
        AttributeData methodAttribute,
        AttributeData? classAttribute,
        int allValues,
        bool excludeAllowsUnknownValues)
        => TryGetAllowedValues(
                methodAttribute,
                allValues,
                excludeAllowsUnknownValues,
                out int methodAllowedValues,
                out bool methodAllowsUnknownValues)
            && TryGetAllowedValues(
                classAttribute,
                allValues,
                excludeAllowsUnknownValues,
                out int classAllowedValues,
                out bool classAllowsUnknownValues)
            && (classAllowedValues & ~methodAllowedValues) == 0
            && (!classAllowsUnknownValues || methodAllowsUnknownValues)
            && IsConditionIgnoreMessageRedundant(methodAttribute, classAttribute);

    private static bool TryGetAllowedValues(
        AttributeData? attribute,
        int allValues,
        bool excludeAllowsUnknownValues,
        out int allowedOperatingSystems,
        out bool allowsUnknownOperatingSystems)
    {
        if (attribute is null)
        {
            allowedOperatingSystems = 0;
            allowsUnknownOperatingSystems = false;
            return false;
        }

        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        switch (arguments)
        {
            case [{ Value: int operatingSystems }]:
                allowedOperatingSystems = operatingSystems & allValues;
                allowsUnknownOperatingSystems = false;
                return true;

            case [{ Value: int mode }, { Value: int operatingSystems }]:
                allowedOperatingSystems = mode == 0
                    ? operatingSystems & allValues
                    : allValues & ~operatingSystems;
                allowsUnknownOperatingSystems = mode != 0 && excludeAllowsUnknownValues;
                return true;

            default:
                allowedOperatingSystems = 0;
                allowsUnknownOperatingSystems = false;
                return false;
        }
    }

    private static bool IsCIConditionRedundant(AttributeData methodAttribute, AttributeData? classAttribute)
        => TryGetConditionMode(methodAttribute, out bool methodIncludesCI)
            && TryGetConditionMode(classAttribute, out bool classIncludesCI)
            && methodIncludesCI == classIncludesCI
            && IsConditionIgnoreMessageRedundant(methodAttribute, classAttribute);

    private static bool IsConditionIgnoreMessageRedundant(
        AttributeData methodAttribute,
        AttributeData? classAttribute)
        => classAttribute is not null
            && (HasNonEmptyConditionIgnoreMessage(classAttribute)
                || !HasNonEmptyConditionIgnoreMessage(methodAttribute));

    private static bool HasNonEmptyConditionIgnoreMessage(AttributeData attribute)
        => !TryGetNamedArgument(attribute, "IgnoreMessage", out TypedConstant constant)
            || constant.Value is string { Length: > 0 };

    private static bool TryGetConditionMode(AttributeData? attribute, out bool includeMode)
    {
        if (attribute?.ConstructorArguments is [{ Value: int mode }])
        {
            includeMode = mode == 0;
            return true;
        }

        includeMode = false;
        return false;
    }

    private static bool IsRetryRedundant(AttributeData methodAttribute, AttributeData? classAttribute)
        => TryGetRetrySettings(
                methodAttribute,
                out int methodMaxRetryAttempts,
                out int methodDelay,
                out int methodBackoffType)
            && TryGetRetrySettings(
                classAttribute,
                out int classMaxRetryAttempts,
                out int classDelay,
                out int classBackoffType)
            && methodMaxRetryAttempts == classMaxRetryAttempts
            && methodDelay == classDelay
            && methodBackoffType == classBackoffType;

    private static bool TryGetRetrySettings(
        AttributeData? attribute,
        out int maxRetryAttempts,
        out int delay,
        out int backoffType)
    {
        if (attribute?.ConstructorArguments is not [{ Value: int retries }])
        {
            maxRetryAttempts = 0;
            delay = 0;
            backoffType = 0;
            return false;
        }

        maxRetryAttempts = retries;
        delay = GetNamedIntArgument(attribute, "MillisecondsDelayBetweenRetries");
        backoffType = GetNamedIntArgument(attribute, "BackoffType");
        return true;
    }

    private static int GetNamedIntArgument(AttributeData attribute, string name)
        => TryGetNamedArgument(
                attribute,
                name,
                out TypedConstant constant,
                valuePredicate: static constant => constant.Value is int)
            && constant.Value is int value
                ? value
                : 0;

    private static bool TryGetNamedArgument(
        AttributeData attribute,
        string name,
        out TypedConstant constant,
        bool returnLastMatch = false,
        Func<TypedConstant, bool>? valuePredicate = null)
    {
        constant = default;
        bool found = false;

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name
                && (valuePredicate is null || valuePredicate(argument.Value)))
            {
                constant = argument.Value;
                found = true;

                if (!returnLastMatch)
                {
                    return true;
                }
            }
        }

        return found;
    }

    private static bool IsIgnoreRedundant(AttributeData methodAttribute, AttributeData? classAttribute)
        => TryGetIgnoreMessage(methodAttribute, out string? methodMessage)
            && TryGetIgnoreMessage(classAttribute, out string? classMessage)
            && (!string.IsNullOrEmpty(classMessage)
                || string.Equals(methodMessage, classMessage, StringComparison.Ordinal));

    private static bool TryGetIgnoreMessage(AttributeData? attribute, out string? message)
    {
        if (attribute is null)
        {
            message = null;
            return false;
        }

        message = attribute.ConstructorArguments switch
        {
            [] => string.Empty,
            [{ Value: string value }] => value,
            [{ IsNull: true }] => null,
            _ => null,
        };

        if (TryGetNamedArgument(attribute, "IgnoreMessage", out TypedConstant constant, returnLastMatch: true))
        {
            message = constant.Value as string;
        }

        return true;
    }

    private static bool HasEquivalentAttribute(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        INamedTypeSymbol? attributeSymbol)
        => classAttributes.Any(classAttribute =>
            SymbolEqualityComparer.Default.Equals(classAttribute.AttributeClass, attributeSymbol)
            && AreArgumentsEquivalent(methodAttribute, classAttribute));

    private static bool AreArgumentsEquivalent(AttributeData left, AttributeData right)
    {
        if (left.ConstructorArguments.Length != right.ConstructorArguments.Length
            || left.NamedArguments.Length != right.NamedArguments.Length)
        {
            return false;
        }

        for (int i = 0; i < left.ConstructorArguments.Length; i++)
        {
            if (!AreConstantsEquivalent(left.ConstructorArguments[i], right.ConstructorArguments[i]))
            {
                return false;
            }
        }

        foreach (KeyValuePair<string, TypedConstant> leftArgument in left.NamedArguments)
        {
            KeyValuePair<string, TypedConstant>? rightArgument = right.NamedArguments.FirstOrDefault(
                argument => argument.Key == leftArgument.Key);
            if (rightArgument is not { } argument
                || !AreConstantsEquivalent(leftArgument.Value, argument.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreConstantsEquivalent(TypedConstant left, TypedConstant right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        if (left.Kind == TypedConstantKind.Type)
        {
            return SymbolEqualityComparer.Default.Equals(left.Value as ITypeSymbol, right.Value as ITypeSymbol);
        }

        if (left.Kind == TypedConstantKind.Array)
        {
            if (left.Values.Length != right.Values.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Values.Length; i++)
            {
                if (!AreConstantsEquivalent(left.Values[i], right.Values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return Equals(left.Value, right.Value);
    }

    private static bool IsDeploymentItemRedundant(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        INamedTypeSymbol? deploymentItemAttributeSymbol)
        => TryGetDeploymentItem(methodAttribute, out string? methodPath, out string? methodOutputDirectory)
            && classAttributes.Any(classAttribute =>
                SymbolEqualityComparer.Default.Equals(classAttribute.AttributeClass, deploymentItemAttributeSymbol)
                && TryGetDeploymentItem(classAttribute, out string? classPath, out string? classOutputDirectory)
                && string.Equals(methodPath, classPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(methodOutputDirectory, classOutputDirectory, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetDeploymentItem(
        AttributeData attribute,
        [NotNullWhen(true)] out string? path,
        [NotNullWhen(true)] out string? outputDirectory)
    {
        path = attribute.ConstructorArguments switch
        {
            [{ Value: string value }, ..] when !string.IsNullOrWhiteSpace(value) => value.TrimEnd('/', '\\'),
            _ => null,
        };
        outputDirectory = attribute.ConstructorArguments switch
        {
            [_] => string.Empty,
            [_, { Value: string value }] when value is not null => value,
            _ => null,
        };
        return path is not null && outputDirectory is not null;
    }

    private static bool IsDependsOnRedundant(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol? dependsOnAttributeSymbol)
    {
        if (!TryGetDependency(methodAttribute, out ITypeSymbol? methodTargetType, out string? methodTargetMethod, out bool methodProceedOnFailure))
        {
            return false;
        }

        foreach (AttributeData classAttribute in classAttributes)
        {
            if (!SymbolEqualityComparer.Default.Equals(classAttribute.AttributeClass, dependsOnAttributeSymbol)
                || !TryGetDependency(classAttribute, out ITypeSymbol? classTargetType, out string? classTargetMethod, out bool classProceedOnFailure)
                || IsSelfDependency(classTargetType, classTargetMethod, methodSymbol)
                || !SymbolEqualityComparer.Default.Equals(methodTargetType, classTargetType)
                || !string.Equals(methodTargetMethod, classTargetMethod, StringComparison.Ordinal)
                || (classProceedOnFailure && !methodProceedOnFailure))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryGetDependency(
        AttributeData attribute,
        out ITypeSymbol? targetType,
        out string? targetMethod,
        out bool proceedOnFailure)
    {
        targetType = null;
        targetMethod = null;
        switch (attribute.ConstructorArguments)
        {
            case [{ Value: string methodName }]:
                targetMethod = methodName;
                break;

            case [{ Value: ITypeSymbol type }]:
                targetType = type;
                break;

            case [{ Value: ITypeSymbol type }, { Value: string methodName }]:
                targetType = type;
                targetMethod = methodName;
                break;

            default:
                proceedOnFailure = false;
                return false;
        }

        proceedOnFailure = false;
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == "ProceedOnFailure" && argument.Value.Value is bool value)
            {
                proceedOnFailure = value;
            }
        }

        return true;
    }

    private static bool IsSelfDependency(ITypeSymbol? targetType, string? targetMethod, IMethodSymbol methodSymbol)
        => string.Equals(targetMethod, methodSymbol.Name, StringComparison.Ordinal)
            && (targetType is null || SymbolEqualityComparer.Default.Equals(targetType, methodSymbol.ContainingType));

    private static bool IsResourceLockRedundant(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        if (!TryGetResourceLock(methodAttribute, out string? methodResource, out int methodMode))
        {
            return false;
        }

        foreach (AttributeData classAttribute in classAttributes)
        {
            if (SymbolEqualityComparer.Default.Equals(classAttribute.AttributeClass, resourceLockAttributeSymbol)
                && TryGetResourceLock(classAttribute, out string? classResource, out int classMode)
                && string.Equals(methodResource, classResource, StringComparison.Ordinal)
                && (classMode == 0 || classMode == methodMode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetResourceLock(AttributeData attribute, [NotNullWhen(true)] out string? resource, out int mode)
    {
        resource = attribute.ConstructorArguments is [{ Value: string value }] ? value : null;
        mode = GetNamedIntArgument(attribute, "Mode");
        return resource is not null && mode is 0 or 1;
    }

    private sealed class KnownAttributeSymbols
    {
        public KnownAttributeSymbols(
            Compilation compilation,
            INamedTypeSymbol testMethodAttributeSymbol,
            INamedTypeSymbol testClassAttributeSymbol)
        {
            TestMethod = testMethodAttributeSymbol;
            TestClass = testClassAttributeSymbol;
            ArchitectureCondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingArchitectureConditionAttribute);
            CICondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCIConditionAttribute);
            DeploymentItem = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDeploymentItemAttribute);
            DependsOn = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDependsOnAttribute);
            DoNotParallelize = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);
            Ignore = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute);
            OSCondition = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOSConditionAttribute);
            ResourceLock = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute);
            Retry = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingRetryAttribute);
            TestCategory = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCategoryAttribute);
            TestProperty = compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute);
        }

        public INamedTypeSymbol? ArchitectureCondition { get; }

        public INamedTypeSymbol? CICondition { get; }

        public INamedTypeSymbol? DeploymentItem { get; }

        public INamedTypeSymbol? DependsOn { get; }

        public INamedTypeSymbol? DoNotParallelize { get; }

        public INamedTypeSymbol? Ignore { get; }

        public INamedTypeSymbol? OSCondition { get; }

        public INamedTypeSymbol? ResourceLock { get; }

        public INamedTypeSymbol? Retry { get; }

        public INamedTypeSymbol TestClass { get; }

        public INamedTypeSymbol? TestCategory { get; }

        public INamedTypeSymbol TestMethod { get; }

        public INamedTypeSymbol? TestProperty { get; }
    }
}
