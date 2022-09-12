// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MSTest.TestHarness.Framework;

public static class Verify
{
    public static void IsTrue(bool condition,
        [CallerArgumentExpression(nameof(condition))] string expression = default,
        [CallerMemberName] string caller = default,
        [CallerFilePath] string filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        if (!condition)
            Throw(expression, caller, filePath, lineNumber);
    }

    public static void IsFalse(bool condition,
        [CallerArgumentExpression(nameof(condition))] string expression = default,
        [CallerMemberName] string caller = default,
        [CallerFilePath] string filePath = default,
        [CallerLineNumber] int lineNumber = default)
        => IsTrue(!condition, expression, caller, filePath, lineNumber);

    [DoesNotReturn]
    private static void Throw(string expression, string caller, string filePath, int lineNumber)
        => throw new Exception($"Condition expression failed: {expression} at line {lineNumber} of {caller} in file {filePath}.");
}