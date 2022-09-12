﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MSTest.ForTesting.TestAdapter;

public static class Verify
{
    public static void IsTrue(bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        if (!condition)
            Throw(expression, caller, filePath, lineNumber);
    }

    public static void IsFalse(bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
        => IsTrue(!condition, expression, caller, filePath, lineNumber);

    public static Exception ThrowsException(Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        Throw(expression, caller, filePath, lineNumber);
        return null;
    }

    [DoesNotReturn]
    private static void Throw(string? expression, string? caller, string? filePath, int lineNumber)
        => throw new Exception($"Condition expression failed: {expression ?? "<expression>"} at line {lineNumber} of {caller ?? "<caller>"} in file {filePath ?? "<file-path>"}.");
}
