// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions;

internal static class TestResultCaptureHelper
{
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;
    internal const int MaxIdentityFieldLength = 4 * 1024;
    internal const int MaxTraitFieldLength = 1024;

    internal static string ClassifyOutcome(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => "passed",
            SkippedTestNodeStateProperty => "skipped",
            TimeoutTestNodeStateProperty => "timedOut",
            ErrorTestNodeStateProperty => "errored",
            FailedTestNodeStateProperty => "failed",
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0
                => "failed",
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    internal static (string? ClassName, string? MethodName) GetClassAndMethodName(TestMethodIdentifierProperty? identifier)
    {
        if (identifier is null)
        {
            return (null, null);
        }

        string className = RoslynString.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : $"{identifier.Namespace}.{identifier.TypeName}";

        return (className, identifier.MethodName);
    }

    internal static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        // Don't split a surrogate pair when truncating: drop the high surrogate too.
        // The early return above guarantees cut < value.Length here, but we keep the
        // explicit guard so the invariant is locally self-documenting and survives
        // any future refactor that might call this block with cut == value.Length.
        int cut = maxLength;
        if (cut > 0 && cut < value.Length && char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return value.Substring(0, cut)
            + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";
    }
}
