// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class RedundantTestMethodAttributeAnalyzer
{
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

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments.Where(argument => argument.Key == name))
        {
            if (valuePredicate is null || valuePredicate(argument.Value))
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
}
