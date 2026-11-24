// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class RedundantTestMethodAttributeAnalyzer
{
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
}
