// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class RedundantTestMethodAttributeAnalyzer
{
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
}
